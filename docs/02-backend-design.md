# BigSchool-TFM — Diseño del Backend

**Fecha**: 2026-06-06
**Estado**: Diseño aprobado

---

## Convenciones de Datos

### Nomenclatura de IDs
- PK: `Id{NombreTabla}` → `IdUser`, `IdTransaction`, `IdCompany`
- FK: Mismo nombre que la PK referenciada
- Tipo: **INT AUTO_INCREMENT** en todas las tablas

### Borrado Lógico
Todas las tablas con datos tienen `IdStatus` (SMALLINT). **Status es un Enum en C#** (no tabla en BD):

```csharp
public enum EntityStatus : short
{
    Pending = 1,
    Active = 2,
    Processing = 3,
    Deleted = 4
}
```

| IdStatus | Nombre | Descripción |
|----------|--------|-------------|
| 1 | Pending | Pendiente de activación/procesamiento |
| 2 | Active | Activo y visible |
| 3 | Processing | En proceso |
| 4 | Deleted | Borrado lógico |

### Auditoría
- Tablas modificables por usuario (Transactions, Companies, Portfolios, Holdings, Valuations): `CreatedAt` + `UpdatedAt`
- SubCategories y RagDocuments: solo `CreatedAt` (o `UploadedAt`)

### Seguridad
- Contraseñas: **Argon2** con Hash + Salt separados

---

## Modelo de Datos

### Diagrama de Entidades

```
┌──────────────────┐
│      Users       │ (AR)
├──────────────────┤
│ IdUser (INT) PK  │
│ Email            │
│ PasswordHash     │
│ PasswordSalt     │
│ FullName         │
│ BaseCurrency     │ ◄── moneda base del usuario (CHAR(3), DEFAULT 'EUR')
│ LastLoginDate    │
│ IdStatus         │
│ CreatedAt        │
│ UpdatedAt        │
└────────┬─────────┘
         │ 1:N (entidad hija, colección navegable)
         ▼
┌──────────────┐
│SubCategories │
├──────────────┤
│IdSubCategory │
│IdMainCategory│
│IdUser (null) │
│Name          │
│IsDefault     │
│IdStatus      │
│CreatedAt     │
└──────────────┘

    Referencia por ID (FK, sin navigation desde User):
    ┌────────────────────────────────────────────────┐
    │                       │                        │
    ▼                       ▼                        ▼
┌────────────────────┐  ┌───────────────┐    ┌──────────────────┐
│  Transactions      │  │  Portfolios   │    │  RagDocuments    │
│  (AR)              │  │  (AR)         │    │  (AR)            │
├────────────────────┤  ├───────────────┤    ├──────────────────┤
│ IdTransaction      │  │ IdPortfolio   │    │ IdRagDocument    │
│ IdUser (FK)        │  │ IdUser (FK)   │    │ IdUser (FK)      │
│ Type (I/E)         │  │ Name          │    │ FileName         │
│ IdMainCategory     │  │ IdStatus      │    │ IdStatus         │
│ IdSubCategory      │  │ CreatedAt     │    │ UploadedAt       │
│ OriginalAmount   ┐ │  │ UpdatedAt     │    └──────────────────┘
│ OriginalCurrency │ │  └───────┬───────┘
│ ExchangeRate     ├─┼── MoneyConversion (snapshot)
│ BaseAmount       │ │          │ 1:N (entidad hija)
│ BaseCurrency     │ │          ▼
│ RateDate         ┘ │  ┌──────────────────┐
│ Date               │  │    Holdings      │
│ IdStatus           │  ├──────────────────┤
│ CreatedAt          │  │ IdHolding        │
│ UpdatedAt          │  │ IdPortfolio      │
└────────────────────┘  │ IdCompany        │
                        │ Shares           │
                        │ AvgBuyPrice  ⁺   │
                        │ BuyDate          │
                        │ IdStatus         │
                        │ CreatedAt        │
                        │ UpdatedAt        │
                        └────────┬─────────┘
                                 │ N:1
                                 ▼
                     ┌──────────────────┐    ┌──────────────────┐
                     │    Companies     │─1:N│   Valuations     │
                     │    (AR)          │    │   (entidad hija) │
                     ├──────────────────┤    ├──────────────────┤
                     │ IdCompany        │    │ IdValuation      │
                     │ Name             │    │ IdCompany        │
                     │ Ticker           │    │ Price        ⁺   │
                     │ Sector           │    │ Date             │
                     │ Market           │    │ Source           │
                     │ Currency         │    │ IdStatus         │
                     │ IdStatus         │    │ CreatedAt        │
                     │ CreatedAt        │    │ UpdatedAt        │
                     │ UpdatedAt        │    └──────────────────┘
                     └──────────────────┘

┌──────────────────────────────┐
│        ExchangeRates         │  Referencia/cache — NO es AR ni BaseEntity.
├──────────────────────────────┤  Sin navigation. La gestiona ExchangeRateApiClient
│ IdExchangeRate PK            │  (HttpClient API tipos + UPSERT Dapper, fuera del UoW).
│ FromCurrency                 │
│ ToCurrency                   │
│ Rate                         │
│ RateDate                     │
│ Source                       │
│ FetchedAt                    │
│ UNIQUE(From, To, RateDate)   │
└──────────────────────────────┘

⁺ Plan 3 (multimoneda): AvgBuyPrice (Holdings) y Price (Valuations) se modelarán como
  MoneyConversion — precio en moneda de la empresa + tipo + importe en base + RateDate.
```

---

### Tablas Detalladas

#### Enums en código (NO son tablas en BD)

**EntityStatus** (SMALLINT en BD):
```csharp
public enum EntityStatus : short { Pending = 1, Active = 2, Processing = 3, Deleted = 4 }
```

**MainCategory** (INT en BD):
```csharp
public enum MainCategory
{
    // EXPENSE
    GastosNecesarios = 1, Inversion = 2, Ahorro = 3, Donaciones = 4,
    Lujos = 5, Educacion = 6, Amortizaciones = 7,
    // INCOME
    Nomina = 10, Alquileres = 11, Dividendos = 12, Otros = 13
}
```

**Currency** (CHAR(3) en BD — se persiste el nombre ISO 4217 alpha-3 vía `ValueConverter<Currency,string>`):
```csharp
public enum Currency : short { EUR = 978, USD = 840, GBP = 826, CHF = 756, JPY = 392 }
// El nombre del enum es el código ISO alpha-3; el valor es el ISO numérico (informativo).
```

#### Value Objects (Domain)

**Money** — importe + moneda, con invariantes en la factory:
```csharp
public sealed record Money(decimal Amount, Currency Currency);
```

**MoneyConversion** — snapshot de conversión (EF owned type). Reutilizable en `Transaction`, `Holding` y `Valuation`:
```csharp
public sealed record MoneyConversion(
    Money    Original,   // OriginalAmount + OriginalCurrency
    decimal  Rate,       // tipo Original -> Base aplicado
    Money    Base,       // BaseAmount + BaseCurrency (snapshot)
    DateOnly RateDate);  // fecha del tipo usado
```
La entidad calcula `Base.Amount = Original.Amount * Rate` en su factory. El **dominio no hace llamadas externas**: recibe el `Rate` ya resuelto desde Application. Si `Original.Currency == BaseCurrency` → `Rate = 1`.

#### Users
| Campo | Tipo | Restricciones |
|-------|------|--------------|
| IdUser | INT | PK, AUTO_INCREMENT |
| Email | VARCHAR(255) | UNIQUE, NOT NULL |
| PasswordHash | VARCHAR(512) | NOT NULL (Argon2) |
| PasswordSalt | VARCHAR(256) | NOT NULL (Argon2) |
| FullName | VARCHAR(200) | NOT NULL |
| BaseCurrency | CHAR(3) | NOT NULL, DEFAULT 'EUR' (enum Currency — moneda base del usuario) |
| LastLoginDate | DATETIME | NULL |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 |
| CreatedAt | DATETIME | NOT NULL |
| UpdatedAt | DATETIME | NULL |

#### SubCategories
| Campo | Tipo | Restricciones |
|-------|------|--------------|
| IdSubCategory | INT | PK, AUTO_INCREMENT |
| IdMainCategory | INT | NOT NULL (enum MainCategory) |
| IdUser | INT | FK → Users, NULL (NULL = predefinida global) |
| Name | VARCHAR(100) | NOT NULL |
| IsDefault | BOOLEAN | DEFAULT FALSE |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 (enum EntityStatus) |
| CreatedAt | DATETIME | NOT NULL |

Subcategorías predefinidas (ejemplos):
- Gastos Necesarios → Supermercado, Farmacia, Facturas, Seguros, Transporte
- Inversión → Bolsa, Fondos, Crypto
- Ahorro → Cuenta ahorro, Depósitos
- Lujos → Restaurantes, Ocio, Viajes, Ropa, Tecnología
- Educación → Cursos, Libros, Máster
- Nómina → Empresa principal
- Alquileres → Vivienda, Local, Garaje
- Dividendos → Acciones nacionales, Acciones internacionales

#### Transactions
| Campo | Tipo | Restricciones |
|-------|------|--------------|
| IdTransaction | INT | PK, AUTO_INCREMENT |
| IdUser | INT | FK → Users, NOT NULL |
| Type | ENUM('INCOME','EXPENSE') | NOT NULL |
| IdMainCategory | INT | NOT NULL (enum MainCategory) |
| IdSubCategory | INT | FK → SubCategories, NULL |
| OriginalAmount | DECIMAL(18,2) | NOT NULL, > 0 (importe en moneda original) |
| OriginalCurrency | CHAR(3) | NOT NULL (enum Currency) |
| ExchangeRate | DECIMAL(18,6) | NOT NULL (tipo Original→Base; 1 si misma moneda) |
| BaseAmount | DECIMAL(18,2) | NOT NULL (= OriginalAmount × ExchangeRate) |
| BaseCurrency | CHAR(3) | NOT NULL (snapshot moneda base del usuario) |
| RateDate | DATE | NOT NULL (fecha del tipo aplicado) |
| Description | VARCHAR(500) | NULL |
| | | _Las 6 columnas de importe/moneda son el VO `MoneyConversion` (owned type): `Original` (OriginalAmount+OriginalCurrency) + `Rate` + `Base` (BaseAmount+BaseCurrency) + `RateDate`._ |
| Date | DATE | NOT NULL |
| IsRecurrent | BOOLEAN | DEFAULT FALSE |
| RecurrencePeriod | ENUM('MONTHLY','QUARTERLY','YEARLY') | NULL |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 (enum EntityStatus) |
| CreatedAt | DATETIME | NOT NULL |
| UpdatedAt | DATETIME | NULL |

#### Companies
| Campo | Tipo | Restricciones |
|-------|------|--------------|
| IdCompany | INT | PK, AUTO_INCREMENT |
| Name | VARCHAR(200) | NOT NULL |
| Ticker | VARCHAR(10) | UNIQUE, NOT NULL |
| Sector | VARCHAR(100) | NULL |
| Market | VARCHAR(50) | NULL (NASDAQ, BME, NYSE) |
| Currency | CHAR(3) | DEFAULT 'EUR' |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 (enum EntityStatus) |
| CreatedAt | DATETIME | NOT NULL |
| UpdatedAt | DATETIME | NULL |

#### Portfolios
| Campo | Tipo | Restricciones |
|-------|------|--------------|
| IdPortfolio | INT | PK, AUTO_INCREMENT |
| IdUser | INT | FK → Users, NOT NULL |
| Name | VARCHAR(100) | NOT NULL |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 (enum EntityStatus) |
| CreatedAt | DATETIME | NOT NULL |
| UpdatedAt | DATETIME | NULL |

#### Holdings
| Campo | Tipo | Restricciones |
|-------|------|--------------|
| IdHolding | INT | PK, AUTO_INCREMENT |
| IdPortfolio | INT | FK → Portfolios, NOT NULL |
| IdCompany | INT | FK → Companies, NOT NULL |
| Shares | DECIMAL(18,4) | NOT NULL, > 0 |
| AvgBuyPrice | DECIMAL(18,4) | NOT NULL |
| BuyDate | DATE | NOT NULL |
| Notes | VARCHAR(500) | NULL |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 (enum EntityStatus) |
| CreatedAt | DATETIME | NOT NULL |
| UpdatedAt | DATETIME | NULL |

> **Plan 3 (multimoneda)**: `AvgBuyPrice` se modelará como `MoneyConversion` (precio en moneda de la empresa + tipo + importe en base del usuario + `RateDate`), reutilizando el VO definido en Plan 2.

#### Valuations
| Campo | Tipo | Restricciones |
|-------|------|--------------|
| IdValuation | INT | PK, AUTO_INCREMENT |
| IdCompany | INT | FK → Companies, NOT NULL |
| Price | DECIMAL(18,4) | NOT NULL |
| Date | DATE | NOT NULL |
| Source | VARCHAR(100) | NULL |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 (enum EntityStatus) |
| CreatedAt | DATETIME | NOT NULL |
| UpdatedAt | DATETIME | NULL |
| | | UNIQUE(IdCompany, Date) |

> **Plan 3 (multimoneda)**: cada `Valuation` snapshotea su propia conversión a la moneda base con el tipo de su `Date` (`MoneyConversion`): el `Price` está en la moneda de la empresa y se convierte en el momento de la valoración.

#### ExchangeRates

Dato de referencia / cache de tipos de cambio. **No es Aggregate Root ni `BaseEntity`** (sin `IdStatus` ni `DomainEvents`) y **no tiene repositorio**: solo lo gestiona `ExchangeRateApiClient`. Queda fuera del Global Query Filter. EF posee el esquema (migración); el cliente lee/escribe con Dapper (UPSERT `ON DUPLICATE KEY`) desacoplado del `UnitOfWork`.

| Campo | Tipo | Restricciones |
|-------|------|--------------|
| IdExchangeRate | INT | PK, AUTO_INCREMENT |
| FromCurrency | CHAR(3) | NOT NULL (enum Currency) |
| ToCurrency | CHAR(3) | NOT NULL (enum Currency) |
| Rate | DECIMAL(18,6) | NOT NULL |
| RateDate | DATE | NOT NULL |
| Source | VARCHAR(100) | NULL (p.ej. "Frankfurter/ECB") |
| FetchedAt | DATETIME | NOT NULL |
| | | UNIQUE(FromCurrency, ToCurrency, RateDate) |

#### RagDocuments
| Campo | Tipo | Restricciones |
|-------|------|--------------|
| IdRagDocument | INT | PK, AUTO_INCREMENT |
| IdUser | INT | FK → Users, NOT NULL |
| FileName | VARCHAR(255) | NOT NULL |
| FileType | VARCHAR(10) | NOT NULL (pdf, txt, md) |
| FileSize | INT | NOT NULL |
| ChunkCount | INT | DEFAULT 0 |
| IdStatus | SMALLINT | FK → Status, DEFAULT 1 (Pending) |
| QdrantCollectionId | VARCHAR(100) | NULL |
| UploadedAt | DATETIME | NOT NULL |
| IndexedAt | DATETIME | NULL |

---

## Agregados DDD

### Reglas de Diseño

1. **Solo los Aggregate Roots implementan `IAggregateRoot`** y tienen repositorio propio
2. **Solo los AR disparan Domain Events** — las entidades hijas notifican al AR que algo cambió
3. **Las entidades hijas no exponen métodos públicos de mutación** — se acceden a través de su AR
4. **Las Queries (lectura) usan Dapper** y acceden directamente a tablas sin pasar por el AR
5. **Los Commands (escritura) cargan el AR** con sus hijos y mutan a través de métodos del AR

### Criterio de Diseño de Agregados

Para decidir si una entidad es hija de un AR o un AR independiente se aplica:

| Criterio | → Entidad hija del AR | → AR independiente |
|---|---|---|
| **Invariantes cruzadas** | Requiere validación contra estado del padre (ej: unicidad de nombre por usuario) | No requiere estado del padre para validarse |
| **Volumen** | Colección pequeña y acotada (< 50 elementos) | Colección potencialmente grande (miles) |
| **Ciclo de vida** | No tiene sentido sin el padre | Tiene sentido por sí sola (solo referencia al usuario por ID) |
| **Concurrencia** | Modificaciones infrecuentes | Modificaciones frecuentes e independientes |

### Mapa de Agregados

| Aggregate Root | Entidades hijas | Repositorio |
|---|---|---|
| **User** | SubCategory | `IUserRepository` |
| **Transaction** | _(ninguna)_ | `ITransactionRepository` |
| **Portfolio** | Holding | `IPortfolioRepository` |
| **Company** | Valuation | `ICompanyRepository` |
| **RagDocument** | _(ninguna)_ | `IRagDocumentRepository` |

**Justificación del cambio (ADR):**
- **SubCategory** permanece como hija de User: colección pequeña (~20-50), invariante de unicidad de nombre por usuario+MainCategory, sin sentido fuera del usuario.
- **Transaction** pasa a AR independiente: volumen alto (miles por usuario), no requiere invariantes cruzadas con User, alta frecuencia de escritura. Referencia a User solo por `IdUser` (int).
- **RagDocument** pasa a AR independiente: volumen medio, ciclo de vida propio (upload → indexing → ready), no comparte invariantes con User.

### Ejemplo: Entidad hija accedida a través del AR (SubCategory)

```csharp
// Command: AddSubCategory — se accede via User (AR padre)
var user = await _userRepository.GetByIdWithSubCategoriesAsync(userId);
user.AddSubCategory(MainCategory.Luxuries, "Conciertos");
await _userRepository.UnitOfWork.SaveChangesAsync();
// Internamente: _subCategories.Add(SubCategory.Create(mainCategory, name))
// El AR puede disparar SubCategoryCreatedEvent si es necesario
```

### Ejemplo: AR independiente (Transaction)

```csharp
// Command: CreateTransaction — AR con su propio repositorio
var transaction = Transaction.Create(userId, type, amount, date, mainCategory, subCategoryId, description);
await _transactionRepository.AddAsync(transaction, cancellationToken);
await _transactionRepository.UnitOfWork.SaveChangesAsync();
// Transaction dispara TransactionCreatedEvent
```

### Ejemplo de lectura directa (Dapper)

```csharp
// Query: GetMonthlyExpenses — NO pasa por el AR
var sql = "SELECT * FROM Transactions WHERE IdUser = @UserId AND MONTH(Date) = @Month";
var transactions = await connection.QueryAsync<TransactionDto>(sql, new { UserId = userId, Month = month });
```

---

## Arquitectura CQRS

### Separación de escritura y lectura

| Aspecto | Commands (escritura) | Queries (lectura) |
|---------|---------------------|-------------------|
| ORM | **EF Core** | **Dapper** |
| Tracking | Sí (change tracking) | No |
| Validación | FluentValidation | N/A |
| Eventos | Domain Events post-SaveChanges | N/A |
| Transacciones | UnitOfWork (DbContext) | Conexión directa MySQL |
| Rendimiento | Correcto para escritura | Óptimo (SQL puro) |

**Consolidación multimoneda**: las queries de agregación (summary, gráficas, balance) operan sobre `BaseAmount` (moneda base del usuario) para que los totales sean coherentes. Opcionalmente pueden devolver desglose por `OriginalCurrency`.

### Domain Events (patrón DDD puro)

**Flujo completo:**
1. Entity llama `RaiseDomainEvent(new TransactionCreatedEvent(...))`
2. CommandHandler llama `await _unitOfWork.SaveChangesAsync()`
3. DbContext.SaveChangesAsync(dispatchEvents): primero persiste, luego despacha eventos si `dispatchEvents = true`
4. Se crea `DomainEventNotification<T>` wrapper para cada evento
5. MediatR publica → Handlers en Application lo procesan

**Nota sobre la firma de IUnitOfWork:**
```csharp
Task<int> SaveChangesAsync(bool dispatchEvents = true);
```
No incluye `CancellationToken` para evitar sobreescrituras de segundo grado con EF Core:
`SaveChangesAsync(dispatchEvents, token)` → `base.SaveChangesAsync(token)` → ambigüedad con
`DbContext.SaveChangesAsync(bool acceptAllChanges, CancellationToken)` que podría causar recursión infinita.
El parámetro `dispatchEvents = false` se usa para seeding en tests de integración.

**Conversión IDomainEvent → INotification:**

```csharp
// Application/Events/DomainEventNotification.cs
public class DomainEventNotification<T> : INotification where T : IDomainEvent
{
    public T DomainEvent { get; }
    public DomainEventNotification(T domainEvent) => DomainEvent = domainEvent;
}
```

**Dispatch dinámico en Infrastructure:**

```csharp
// Infrastructure/Persistence/BigSchoolDbContext.cs (extracto)
public async Task<int> SaveChangesAsync(bool dispatchEvents = true)
{
    var result = await base.SaveChangesAsync(CancellationToken.None);
    if (dispatchEvents) await DispatchDomainEvents();
    return result;
}

private async Task DispatchDomainEvents()
{
    var entities = ChangeTracker.Entries<BaseEntity>()
        .Where(e => e.Entity.DomainEvents.Any())
        .Select(e => e.Entity)
        .ToList();

    var domainEvents = entities.SelectMany(e => e.DomainEvents).ToList();
    entities.ForEach(e => e.ClearDomainEvents());

    foreach (var domainEvent in domainEvents)
    {
        var notificationType = typeof(DomainEventNotification<>)
            .MakeGenericType(domainEvent.GetType());
        var notification = Activator.CreateInstance(notificationType, domainEvent);
        await _mediator.Publish(notification!);
    }
}
```

**MediatR solo en**: Application + Infrastructure (nunca en Domain)

---

## Inyección de Dependencias — Autofac Modular

### Registro por Assemblies

Cada capa tiene su módulo Autofac que registra automáticamente sus tipos:

```csharp
// Application/DependencyInjection/ApplicationModule.cs
public class ApplicationModule : Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // MediatR Handlers (Commands + Queries)
        builder.RegisterAssemblyTypes(ThisAssembly)
            .AsClosedTypesOf(typeof(IRequestHandler<,>))
            .AsImplementedInterfaces();

        // Event Handlers (INotificationHandler)
        builder.RegisterAssemblyTypes(ThisAssembly)
            .AsClosedTypesOf(typeof(INotificationHandler<>))
            .AsImplementedInterfaces();

        // AutoMapper Profiles
        builder.RegisterAssemblyTypes(ThisAssembly)
            .AssignableTo<Profile>()
            .As<Profile>();

        // FluentValidation Validators
        builder.RegisterAssemblyTypes(ThisAssembly)
            .AsClosedTypesOf(typeof(IValidator<>))
            .AsImplementedInterfaces();
    }
}

// Infrastructure/DependencyInjection/InfrastructureModule.cs
public class InfrastructureModule : Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Repositories
        builder.RegisterAssemblyTypes(ThisAssembly)
            .Where(t => t.Name.EndsWith("Repository"))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        // Services (JwtService, Argon2PasswordHasher, RagServiceClient)
        builder.RegisterAssemblyTypes(ThisAssembly)
            .Where(t => t.Name.EndsWith("Service") || t.Name.EndsWith("Hasher") || t.Name.EndsWith("Client"))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        // DbContext
        builder.RegisterType<BigSchoolDbContext>()
            .AsSelf()
            .As<IUnitOfWork>()
            .InstancePerLifetimeScope();

        // Dapper connection factory
        builder.RegisterType<DbConnectionFactory>()
            .As<IDbConnectionFactory>()
            .InstancePerLifetimeScope();
    }
}
```

### Mapeo de Entidades — AutoMapper

Profiles organizados por feature/bounded context:

```csharp
// Application/Mappings/TransactionProfile.cs
public class TransactionProfile : Profile
{
    public TransactionProfile()
    {
        CreateMap<Transaction, TransactionDto>();
        CreateMap<CreateTransactionCommand, Transaction>();
    }
}

// Application/Mappings/PortfolioProfile.cs
public class PortfolioProfile : Profile
{
    public PortfolioProfile()
    {
        CreateMap<Portfolio, PortfolioDto>();
        CreateMap<Holding, HoldingDto>();
        CreateMap<Company, CompanyDto>();
    }
}
```

---

## API Endpoints

### Auth
| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | /api/v1/auth/register | Registro |
| POST | /api/v1/auth/login | Login → JWT |
| POST | /api/v1/auth/refresh | Refrescar token |

### Transactions (Gastos/Ingresos)
| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | /api/v1/transactions | Listar (filtros, paginación) — Dapper |
| GET | /api/v1/transactions/{id} | Detalle — Dapper |
| POST | /api/v1/transactions | Crear — EF Core |
| PUT | /api/v1/transactions/{id} | Actualizar — EF Core |
| DELETE | /api/v1/transactions/{id} | Borrado lógico — EF Core |
| GET | /api/v1/transactions/summary | Resumen por periodo — Dapper |
| GET | /api/v1/transactions/chart/monthly | Datos gráfica — Dapper |

> El body de `POST`/`PUT` incluye `currency` (enum `Currency`); si se omite, se asume la `BaseCurrency` del usuario. La respuesta expone importe original y `BaseAmount` consolidado.

### SubCategories
| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | /api/v1/categories | Principales + sub — Dapper |
| POST | /api/v1/categories/sub | Crear subcategoría — EF Core |
| DELETE | /api/v1/categories/sub/{id} | Borrado lógico — EF Core |

### Companies
| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | /api/v1/companies | Listar — Dapper |
| GET | /api/v1/companies/{id} | Detalle + valoraciones — Dapper |
| POST | /api/v1/companies | Crear — EF Core |
| GET | /api/v1/companies/{id}/valuations | Histórico — Dapper |
| POST | /api/v1/companies/{id}/valuations | Añadir valoración — EF Core |

### Portfolio
| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | /api/v1/portfolios | Listar carteras — Dapper |
| POST | /api/v1/portfolios | Crear — EF Core |
| GET | /api/v1/portfolios/{id} | Detalle + holdings — Dapper |
| POST | /api/v1/portfolios/{id}/holdings | Añadir posición — EF Core |
| PUT | /api/v1/portfolios/{id}/holdings/{hId} | Actualizar — EF Core |
| DELETE | /api/v1/portfolios/{id}/holdings/{hId} | Borrado lógico — EF Core |
| GET | /api/v1/portfolios/{id}/performance | Rentabilidad — Dapper |

### RAG (proxy al RAG Service Python)
| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | /api/v1/rag/chat | Mensaje al LLM |
| GET | /api/v1/rag/documents | Listar documentos — Dapper |
| POST | /api/v1/rag/documents | Subir documento — EF Core |
| DELETE | /api/v1/rag/documents/{id} | Eliminar — EF Core |

---

## Estructura Clean Architecture

```
Backend.sln
├── src/
│   ├── BigSchool.Domain/
│   │   ├── Entities/
│   │   │   ├── BaseEntity.cs           → DomainEvents list + RaiseDomainEvent()
│   │   │   ├── IAggregateRoot.cs       → Marcador para Aggregate Roots
│   │   │   ├── User.cs                 → AR (posee SubCategories como entidad hija)
│   │   │   ├── SubCategory.cs          → Entidad hija de User (constructor private)
│   │   │   ├── Transaction.cs          → AR independiente (referencia User por IdUser)
│   │   │   ├── Company.cs              → AR (posee Valuations como entidad hija)
│   │   │   ├── Valuation.cs            → Entidad hija de Company
│   │   │   ├── Portfolio.cs            → AR (posee Holdings como entidad hija)
│   │   │   ├── Holding.cs              → Entidad hija de Portfolio
│   │   │   ├── RagDocument.cs          → AR independiente (referencia User por IdUser)
│   │   │   └── ExchangeRate.cs         → Dato de referencia/cache (NO es AR ni BaseEntity)
│   │   ├── ValueObjects/
│   │   │   ├── Money.cs                 → Importe + Currency
│   │   │   └── MoneyConversion.cs       → Snapshot conversión (owned type) reutilizable
│   │   ├── Enums/
│   │   │   ├── TransactionType.cs
│   │   │   ├── RecurrencePeriod.cs
│   │   │   ├── Currency.cs
│   │   │   └── EntityStatus.cs
│   │   ├── Events/
│   │   │   ├── IDomainEvent.cs          → Interfaz propia (sin MediatR)
│   │   │   ├── TransactionCreatedEvent.cs
│   │   │   ├── HoldingAddedEvent.cs
│   │   │   └── DocumentUploadedEvent.cs
│   │   └── Interfaces/
│   │       └── IUnitOfWork.cs           → SaveChangesAsync(dispatchEvents = true)
│   │
│   ├── BigSchool.Application/
│   │   ├── Interfaces/
│   │   │   ├── IRepository.cs          → IRepository<T, Y> where T : IAggregateRoot
│   │   │   ├── IUserRepository.cs      → AR User + SubCategories (entidad hija)
│   │   │   ├── ITransactionRepository.cs → AR independiente
│   │   │   ├── ICompanyRepository.cs   → AR Company + Valuations
│   │   │   ├── IPortfolioRepository.cs → AR Portfolio + Holdings
│   │   │   ├── IRagDocumentRepository.cs → AR independiente
│   │   │   ├── IRagServiceClient.cs
│   │   │   ├── IExchangeRateProvider.cs → Tipos de cambio (atajo from==to → 1)
│   │   │   └── IDbConnectionFactory.cs → Para Dapper
│   │   ├── Events/
│   │   │   └── DomainEventNotification.cs → Wrapper IDomainEvent → INotification
│   │   ├── EventHandlers/
│   │   │   └── ...
│   │   ├── Commands/
│   │   │   ├── Auth/
│   │   │   │   ├── Register/
│   │   │   │   └── Login/
│   │   │   ├── Transactions/
│   │   │   │   ├── CreateTransaction/
│   │   │   │   ├── UpdateTransaction/
│   │   │   │   └── DeleteTransaction/
│   │   │   ├── SubCategories/
│   │   │   │   └── AddSubCategory/      → Accede via User AR
│   │   │   ├── Companies/
│   │   │   │   └── CreateCompany/
│   │   │   ├── Portfolios/
│   │   │   │   ├── AddHolding/
│   │   │   │   └── AddValuation/
│   │   │   └── Rag/
│   │   │       └── UploadRagDocument/
│   │   ├── Queries/
│   │   │   ├── GetTransactions/         → Usa Dapper via IDbConnectionFactory
│   │   │   ├── GetTransactionSummary/
│   │   │   ├── GetMonthlyChart/
│   │   │   ├── GetCompanies/
│   │   │   ├── GetPortfolioPerformance/
│   │   │   └── GetRagDocuments/
│   │   ├── DTOs/
│   │   ├── Mappings/                    → AutoMapper Profiles
│   │   │   ├── TransactionProfile.cs
│   │   │   ├── PortfolioProfile.cs
│   │   │   └── CompanyProfile.cs
│   │   └── Validators/                  → FluentValidation
│   │
│   ├── BigSchool.Infrastructure/
│   │   ├── DependencyInjection/
│   │   │   └── InfrastructureModule.cs  → Autofac module
│   │   ├── Persistence/
│   │   │   ├── BigSchoolDbContext.cs    → SaveChangesAsync + DispatchEvents
│   │   │   ├── DbConnectionFactory.cs  → IDbConnection para Dapper
│   │   │   ├── Configurations/         → EF Core Fluent API
│   │   │   ├── Repositories/
│   │   │   └── Migrations/
│   │   └── Services/
│   │       ├── JwtService.cs
│   │       ├── Argon2PasswordHasher.cs
│   │       ├── RagServiceClient.cs     → HttpClient al RAG Python
│   │       └── ExchangeRateApiClient.cs → HttpClient API tipos (Frankfurter) + cache Dapper
│   │
│   └── BigSchool.WebApi/
│       ├── Controllers/
│       │   ├── AuthController.cs
│       │   ├── TransactionsController.cs
│       │   ├── CategoriesController.cs
│       │   ├── CompaniesController.cs
│       │   ├── PortfoliosController.cs
│       │   └── RagController.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Program.cs                   → Autofac como DI container
│       └── appsettings.json
│
└── tests/
    ├── BigSchool.Domain.Tests/
    ├── BigSchool.Application.Tests/
    └── BigSchool.Integration.Tests/
```

---

## Patrones y Librerías

| Patrón/Librería | Uso |
|-----------------|-----|
| CQRS | Commands (EF Core) / Queries (Dapper) via MediatR |
| Repository | Interfaces en Application, implementación en Infrastructure |
| Unit of Work | EF Core DbContext con dispatch de eventos post-save |
| Domain Events | IDomainEvent propia → DomainEventNotification<T> → INotificationHandler |
| Mediator | MediatR (solo en Application + Infrastructure) |
| DI Container | **Autofac** con registro modular por Assemblies |
| Object Mapping | **AutoMapper** con Profiles por feature |
| Validation | **FluentValidation** en Commands |
| Anti-corruption Layer | RagServiceClient → RAG Service Python; ExchangeRateApiClient → API de tipos de cambio |
| Envelope | Respuestas: `{ data, errors, meta }` |
| Soft Delete | IdStatus=4, Global Query Filter en EF Core |

---

## NuGet Packages (principales)

### BigSchool.Domain
- (sin dependencias externas)

### BigSchool.Application
- MediatR
- AutoMapper
- FluentValidation
- Dapper

### BigSchool.Infrastructure
- Microsoft.EntityFrameworkCore
- Pomelo.EntityFrameworkCore.MySql
- Autofac
- Isopoh.Cryptography.Argon2
- Dapper

### BigSchool.WebApi
- Autofac.Extensions.DependencyInjection
- Swashbuckle.AspNetCore
- Microsoft.AspNetCore.Authentication.JwtBearer

### Tests
- xUnit
- FluentAssertions
- Moq
- Testcontainers
- Microsoft.AspNetCore.Mvc.Testing

---

## Notas de Implementación

- **Argon2** para hashing de contraseñas (Hash + Salt)
- **JWT** con claims: IdUser (encriptado con DPAPI), Email
- **EF Core Fluent API** (no Data Annotations) para configuración de entidades
- **Dapper** con `IDbConnectionFactory` para todas las queries de lectura
- **Autofac** registra automáticamente: Handlers, Repositories, Services, Profiles, Validators
- **AutoMapper** mapea entidades ↔ DTOs en cada Profile
- **Migraciones** con `dotnet ef migrations`
- **Seed** en migración inicial: SubCategories predefinidas (Status y MainCategories son enums, no se seedean en BD)
- **Multimoneda**: `Money`/`MoneyConversion` se mapean como **EF owned types**; `Currency` con `ValueConverter<Currency,string>` → CHAR(3). `Users.BaseCurrency` con `DEFAULT 'EUR'`.
- **Tipos de cambio**: `ExchangeRateApiClient` (HttpClient a Frankfurter/ECB) cachea en `ExchangeRates` vía Dapper (UPSERT), desacoplado del `UnitOfWork`. Seed de tipos fijos para una demo determinista. El dominio recibe el `Rate` ya resuelto (atajo `from==to → 1`).
- **Swagger/OpenAPI** con Swashbuckle
- **Health check**: `/health`
- **Global Query Filter** en EF Core: `entity.IdStatus != 4` (soft delete transparente)
- **Rate limit** en endpoints del RAG (proteger consumo Azure OpenAI)
- **Agregados pequeños** (Vaughn Vernon): Transaction y RagDocument son ARs independientes para evitar fat aggregate en User. SubCategory sí es hija de User por tener invariante de unicidad y volumen bajo.
- **Entidades hijas con constructor private**: se acceden solo a través de métodos del AR padre (ej: `user.AddSubCategory(...)`). No necesitan `InternalsVisibleTo` — se testean a través del AR.
