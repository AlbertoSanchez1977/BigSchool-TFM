# Diseño: BC Finanzas Personales + Soporte Multimoneda (Plan 2)

**Fecha**: 2026-06-17
**Autor**: Alberto Sánchez
**Estado**: Diseño aprobado (pendiente de plan de implementación)
**Módulo**: backend

---

## 1. Contexto y motivación

El Plan 1 (autenticación crosscutting: register / login / refresh) está cerrado. El siguiente
paso es el **Bounded Context de Finanzas Personales** (entidad `Transaction` con lógica de
negocio completa, CQRS y endpoints).

Al revisar el modelo se detecta una **falla de diseño**: el sistema es **monomoneda implícita**.

- `Transactions.Amount` no tiene moneda asociada.
- `Companies.Currency` existe (`DEFAULT 'EUR'`) pero `Holdings.AvgBuyPrice` y `Valuations.Price`
  no llevan moneda ni mecanismo de conversión.
- No existe el concepto de **moneda base** del usuario ni de **tipo de cambio**.

En el Plan 3 (Inversiones) las empresas pueden cotizar en distintas monedas y cada `Valuation`
es un punto temporal con su propio tipo de cambio. Sin resolver esto ahora, `Transaction`
nacería monomoneda y habría que reescribir el modelo en Plan 3.

**Decisión**: resolver el soporte multimoneda **ahora**, en Plan 2, con un modelo `Money`
transversal y reutilizable, de forma que Plan 3 lo consuma sin cambios de modelo.

---

## 2. Decisiones de diseño (acordadas)

| Decisión | Elección | Implicación |
|---|---|---|
| Moneda base | **Por usuario** (`Users.BaseCurrency`) | Cada usuario consolida balances/carteras en su moneda |
| Alcance multimoneda | **Ambos BCs** (Finanzas + Inversiones) | Modelo `Money` uniforme y reutilizable |
| Modelo de conversión | **Snapshot**: original + tipo + convertido | Preserva historia, auditable, balance histórico estable |
| Fuente de tipos | **API externa + cache** | Anti-corruption layer + tabla cache para determinismo/resiliencia |

---

## 3. Modelo de dominio multimoneda (transversal)

Tres piezas en `BigSchool.Domain`, creadas en Plan 2 y reutilizadas tal cual en Plan 3.

### 3.1 `Currency` (enum en código, NO tabla)

Coherente con la convención del proyecto (`EntityStatus`, `MainCategory` son enums en C#).

```csharp
public enum Currency : short { EUR = 978, USD = 840, GBP = 826, CHF = 756, JPY = 392 }
// El NOMBRE del enum es el código ISO 4217 alpha-3. Se persiste como CHAR(3) vía ValueConverter.
```

Set reducido para la demo; ampliable. Los valores numéricos son los códigos ISO 4217 numéricos
(informativo; en BD se persiste el alpha-3 por legibilidad).

### 3.2 `Money` (Value Object)

```csharp
public sealed record Money(decimal Amount, Currency Currency);
```

Con invariantes en la factory (importe finito, no negativo donde aplique, redondeo a la escala
de la columna).

### 3.3 `MoneyConversion` (Value Object — EF owned type)

El **snapshot** de conversión. Reutilizable en `Transaction`, `Holding` y `Valuation`.

```csharp
public sealed record MoneyConversion(
    Money    Original,   // OriginalAmount + OriginalCurrency
    decimal  Rate,       // tipo Original -> Base aplicado
    Money    Base,       // BaseAmount + BaseCurrency (snapshot de la base del usuario)
    DateOnly RateDate);  // fecha del tipo usado
```

Mapea a columnas: `OriginalAmount, OriginalCurrency, ExchangeRate, BaseAmount, BaseCurrency, RateDate`.

**Regla clave**: la entidad calcula `Base.Amount = Original.Amount * Rate` en su factory. El
**dominio no realiza llamadas externas**: recibe el `Rate` ya resuelto desde la capa Application.
Si `Original.Currency == BaseCurrency` → `Rate = 1`.

---

## 4. Moneda base por usuario

Añadir a la entidad/tabla `Users`:

| Campo | Tipo | Restricciones |
|---|---|---|
| BaseCurrency | CHAR(3) | NOT NULL, DEFAULT 'EUR' |

- Se fija en el registro (EUR por defecto; opcionalmente parametrizable en `RegisterCommand`).
- Migración: `AlterUsersAddBaseCurrency`.
- **Simplificación asumida**: si un usuario cambia su moneda base, el histórico **mantiene su
  snapshot** (no hay reconversión retroactiva).

---

## 5. Tipos de cambio: proveedor externo + cache

### 5.1 Componentes

- **`IExchangeRateProvider`** (Application):
  ```csharp
  Task<decimal> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken ct);
  ```
  Atajo: `from == to` → `1m` sin llamada.

- **`ExchangeRateApiClient`** (Infrastructure, `HttpClient`, anti-corruption layer — mismo patrón
  que `RagServiceClient`): lee cache → si *miss*, llama API externa → guarda en cache → devuelve.
  API candidata: **Frankfurter (ECB)** — gratis, sin API key, soporta histórico por fecha, ideal
  con base EUR. Fallback si la API está caída y hay *miss*: último tipo conocido o error controlado.

- **Tabla `ExchangeRates`** — dato de referencia/cache. **NO es Aggregate Root**, **NO es
  `BaseEntity`** (sin `IdStatus` ni `DomainEvents`), **NO tiene repositorio**. Solo la toca el
  `ExchangeRateApiClient`. Queda fuera del Global Query Filter automáticamente.

| Campo | Tipo | Restricciones |
|---|---|---|
| IdExchangeRate | INT | PK, AUTO_INCREMENT |
| FromCurrency | CHAR(3) | NOT NULL |
| ToCurrency | CHAR(3) | NOT NULL |
| Rate | DECIMAL(18,6) | NOT NULL |
| RateDate | DATE | NOT NULL |
| Source | VARCHAR(100) | NULL |
| FetchedAt | DATETIME | NOT NULL |
| | | UNIQUE(FromCurrency, ToCurrency, RateDate) |

### 5.2 Persistencia: EF posee el esquema, el cliente lee/escribe con Dapper

**Motivo**: la escritura del cache **no debe ir en el `SaveChanges` del comando de negocio**. Si
el cliente insertara en el mismo `DbContext`/UoW que `CreateTransactionHandler`, el cacheo se
mezclaría con la transacción de negocio y dispararía el dispatch de Domain Events. El tipo
cacheado es reference data que debe persistir **aunque la creación de la transacción falle**, de
forma idempotente.

- **EF** mapea la entidad **solo para generar la tabla** (migración) con la `ExchangeRateConfiguration`.
- El **`ExchangeRateApiClient`** lee y escribe el cache con **Dapper** (`IDbConnectionFactory`),
  desacoplado del UoW de negocio, con UPSERT atómico race-safe sobre la clave única:

```csharp
const string sql = """
    INSERT INTO ExchangeRates (FromCurrency, ToCurrency, Rate, RateDate, Source, FetchedAt)
    VALUES (@From, @To, @Rate, @Date, @Source, @Now)
    ON DUPLICATE KEY UPDATE Rate = @Rate, Source = @Source, FetchedAt = @Now;
    """;
```

### 5.3 Configuración EF (esquema) — mapeo de enum y DateOnly

```csharp
public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> b)
    {
        b.ToTable("ExchangeRates");
        b.HasKey(e => e.IdExchangeRate);

        // Currency (enum) -> CHAR(3): el nombre del enum es el ISO alpha-3
        var currency = new ValueConverter<Currency, string>(
            v => v.ToString(),
            v => Enum.Parse<Currency>(v));

        b.Property(e => e.FromCurrency).HasConversion(currency).HasColumnType("char(3)");
        b.Property(e => e.ToCurrency)  .HasConversion(currency).HasColumnType("char(3)");
        b.Property(e => e.Rate).HasColumnType("decimal(18,6)");
        b.Property(e => e.RateDate).HasColumnType("date");   // DateOnly nativo en Pomelo 8 / EF 8
        b.Property(e => e.Source).HasMaxLength(100);

        b.HasIndex(e => new { e.FromCurrency, e.ToCurrency, e.RateDate }).IsUnique();
    }
}
```

El mismo `ValueConverter<Currency,string>` se reutiliza en el owned type `Money` de `Transaction`.
Si alguna versión de Pomelo diera problemas con `DateOnly`, fallback a `ValueConverter<DateOnly, DateTime>`.

Seed de la migración: unos pocos pares fijos (p.ej. USD→EUR, GBP→EUR, CHF→EUR) para que la demo
sea determinista sin depender de la red.

---

## 6. Flujo de conversión (`CreateTransaction`)

1. El handler lee `BaseCurrency` del usuario.
2. `rate = (currency == base) ? 1 : await _rates.GetRateAsync(currency, base, date, ct)`.
3. `Transaction.Create(userId, type, new Money(amount, currency), baseCurrency, rate, date, ...)`
   → la entidad calcula `BaseAmount` y construye el `MoneyConversion`.
4. `SaveChanges` + `TransactionCreatedEvent`.

La llamada externa vive **solo en Application**; el Domain permanece puro y testeable con `rate`
inyectado.

---

## 7. Alcance de implementación — BC Finanzas Personales (Plan 2)

### 7.1 Dominio

- **`Transaction`** (AR independiente, ya decidido en `02-backend-design`): factory `Create`,
  `Update`, soft-delete, `MoneyConversion`, validaciones (importe > 0, moneda válida, fecha,
  categoría), `TransactionCreatedEvent`.
- **Transversales** (reutilizados en Plan 3): `Currency`, `Money`, `MoneyConversion`.
- **`Users.BaseCurrency`** + migración.

### 7.2 Infraestructura

- `IExchangeRateProvider` + `ExchangeRateApiClient` + entidad `ExchangeRate` + `ExchangeRateConfiguration`.
- Migración de `ExchangeRates` + seed de tipos fijos.
- Owned type `MoneyConversion` en la configuración de `Transaction`.

### 7.3 Categorías

- `MainCategory` (enum, existe) y `SubCategory` (entidad hija de User, existe).
- Falta: query de categorías y, si procede, commands de subcategoría (`AddSubCategory` ya existe).

### 7.4 CQRS

- **Commands (EF Core)**: `CreateTransaction`, `UpdateTransaction`, `DeleteTransaction` (soft).
- **Queries (Dapper)** — **todas consolidan sobre `BaseAmount`** (moneda base del usuario):
  `GetTransactions` (filtros + paginación), `GetTransactionById`, `GetTransactionSummary`,
  `GetMonthlyChart`, `GetCategories`. Opcional: desglose por moneda original.
- **Validators (FluentValidation)**: Create/Update transaction.

### 7.5 Endpoints

Los ya listados en `02-backend-design.md` (`/api/v1/transactions`, `/api/v1/categories`), sin
cambios de contrato salvo **añadir `currency` al body de creación** de transacción (con default
a la moneda base del usuario si se omite).

---

## 8. Cambios documentales (parte de la implementación)

- **`docs/01-arquitectura.md`** → nuevo **ADR-006: Soporte multimoneda** (moneda base por usuario,
  snapshot de conversión, proveedor externo + cache).
- **`docs/02-backend-design.md`** →
  - `Users.BaseCurrency`.
  - `Transactions` con `MoneyConversion` (columnas del snapshot).
  - Nueva tabla `ExchangeRates`.
  - Prever `Currency` + snapshot en `Holdings`/`Valuations` (Plan 3).
  - `Currency` / `Money` / `MoneyConversion` en Enums / Value Objects.
  - `IExchangeRateProvider` / `ExchangeRateApiClient` en interfaces / services.
  - Nota CQRS de consolidación en moneda base.

---

## 9. Testing

- **Domain**: `Transaction.Create` con conversión (rate = 1 mismo símbolo; rate ≠ 1 con
  `BaseAmount` correcto), invariantes de `Money` / `MoneyConversion`.
- **Application**: handlers con `IExchangeRateProvider` mockeado; validators (Create/Update).
- **Integration**: `ExchangeRateApiClient` cache *hit/miss* (WireMock para la API externa);
  migración `ExchangeRates`; query de summary consolidada en base.

---

## 10. Simplificaciones asumidas (TFM)

- `DECIMAL(18,2)` para importes de transacciones y `DECIMAL(18,4)` para precios de inversión, sin
  contemplar monedas con distinto número de decimales (p.ej. JPY usa 0).
- Set reducido de divisas en el enum `Currency`.
- Sin reconversión retroactiva si el usuario cambia su moneda base (el histórico conserva su snapshot).
- Tipos de cambio cacheados por día (`RateDate` granularidad DATE), no intradía.

---

## 11. Resumen de artefactos nuevos

| Artefacto | Capa | Tipo |
|---|---|---|
| `Currency` | Domain | enum |
| `Money` | Domain | Value Object |
| `MoneyConversion` | Domain | Value Object (owned) |
| `Users.BaseCurrency` | Domain/Infra | campo + migración |
| `Transaction` (completa) | Domain | Aggregate Root |
| `ExchangeRate` | Domain/Infra | entidad plana (no AR) |
| `IExchangeRateProvider` | Application | interfaz |
| `ExchangeRateApiClient` | Infrastructure | service (anti-corruption + Dapper) |
| `ExchangeRateConfiguration` | Infrastructure | EF config (esquema) |
| Commands/Queries/Validators de Transactions | Application | CQRS |
| ADR-006 | docs | decisión de arquitectura |
