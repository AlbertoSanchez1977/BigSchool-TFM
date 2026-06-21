# BC Inversiones — Sector & Market como enums (Company)

> **Para el ejecutor:** plan a aplicar en la **misma rama** `feature/backend-inversiones-plan-3a-task13` (no abre rama nueva). Pasos con checkbox (`- [ ]`) para tracking. Sub-skill recomendada: superpowers:test-driven-development (ajustar tests antes que implementación).

**Goal:** Estandarizar `Company.Sector` y `Company.Market` como enums fuertemente tipados (al estilo de `Currency`), sin tabla diccionario. Se almacenan en BD como `string` (nombre del miembro) vía `HasConversion<string>()`, por lo que **el esquema y los datos sembrados no cambian**.

**Decisión BME:** se añade `BME` (Bolsa de Madrid) al enum `Market` porque el seed tiene SAN/ITX/IBE cotizando ahí.

**Clave de compatibilidad:** los nombres de miembro de los enums coinciden *exactamente* (case-sensitive) con los valores ya presentes en `init.sql` y `seed.sql`. No tocar BD ni seeds.

---

## Enums a crear

### `Sector` (11 miembros)
`Technology, Financials, Energy, Retail, Automotive, Healthcare, RealEstate, Utilities, ConsumerGoods, Industrials, Other`

> Cubre los valores ya sembrados: `Technology`, `Financials`, `Energy`, `Retail`, `Automotive`.

### `Market` (14 miembros)
`NYSE, NASDAQ, TSX, CSE, TSXV, LSE, AquisExchange, CboeUK, Frankfurt, Xetra, BorseStuttgart, BorseMunchen, EuronextParis, BME`

> Cubre los valores ya sembrados: `NASDAQ`, `LSE`, `BME`. Nombres de miembro normalizados a PascalCase sin espacios/acentos (`AquisExchange`, `CboeUK`, `BorseStuttgart`, `BorseMunchen`, `EuronextParis`).

---

## File Structure

**Domain** (`src/backend/src/BigSchool.Domain/`)
- Crear `Enums/Sector.cs`, `Enums/Market.cs`.
- Modificar `Entities/Company.cs` (propiedades + firma de `Create`).

**Infrastructure** (`src/backend/src/BigSchool.Infrastructure/`)
- Modificar `Persistence/Configurations/CompanyConfiguration.cs` (`HasConversion<string>()`).
- Generar migración `AddSectorMarketEnums` (Up vacío / no-op).

**Application** (`src/backend/src/BigSchool.Application/`)
- Modificar `DTOs/Investments/CompanyDto.cs` (tipos fuertes).
- Modificar `Commands/Investments/CreateCompany/CreateCompanyCommand.cs`, `CreateCompanyCommandValidator.cs`, `CreateCompanyCommandHandler.cs`.
- Modificar `Queries/Investments/GetCompanies/GetCompaniesQuery.cs` + handler.
- **NO tocar** `CompanyListItemDto` (sigue `string` por convención Dapper).

**WebApi** (`src/backend/src/BigSchool.WebApi/`)
- Modificar `Controllers/CompaniesController.cs` (`CreateCompanyRequest` + binding del filtro GET).

**Tests**
- Modificar `tests/BigSchool.Domain.Tests/Entities/CompanyTests.cs`.
- Modificar `tests/BigSchool.Application.Tests/Commands/Investments/CreateCompanyCommandHandlerTests.cs`.
- Los E2E (`CreateCompanyTests`, `GetCompaniesTests`, `GetCompanyByIdTests`) **no cambian**: el JSON sigue enviando strings y `JsonStringEnumConverter` los deserializa.

---

### Task 1: Crear los enums `Sector` y `Market`

- [x] **Step 1:** Crear `src/backend/src/BigSchool.Domain/Enums/Sector.cs`:

```csharp
namespace BigSchool.Domain.Enums;

public enum Sector
{
    Technology,
    Financials,
    Energy,
    Retail,
    Automotive,
    Healthcare,
    RealEstate,
    Utilities,
    ConsumerGoods,
    Industrials,
    Other
}
```

- [x] **Step 2:** Crear `src/backend/src/BigSchool.Domain/Enums/Market.cs`:

```csharp
namespace BigSchool.Domain.Enums;

public enum Market
{
    // Estados Unidos
    NYSE,
    NASDAQ,
    // Canadá
    TSX,
    CSE,
    TSXV,
    // Reino Unido
    LSE,
    AquisExchange,
    CboeUK,
    // Alemania
    Frankfurt,
    Xetra,
    BorseStuttgart,
    BorseMunchen,
    // Francia
    EuronextParis,
    // España
    BME
}
```

> **Nota:** el valor por defecto del enum (ordinal 0) es `Sector.Technology` / `Market.NYSE`. Como ambas propiedades son **nullable** en `Company`, el default no se aplica a empresas sin sector/market.

---

### Task 2: Ajustar tests de dominio (TDD — fallan al compilar)

- [x] **Step 1:** En `tests/BigSchool.Domain.Tests/Entities/CompanyTests.cs`, actualizar el helper y las aserciones:

```csharp
private static Company NewCompany()
    => Company.Create("Apple Inc.", "aapl", Sector.Technology, Market.NASDAQ, Currency.USD);
```

```csharp
// en Create_NormalizesTicker_AndSetsActive:
company.Sector.Should().Be(Sector.Technology);
company.Market.Should().Be(Market.NASDAQ);
```

El test `Create_WithEmptyNameOrTicker_Throws` ya pasa `null, null` para sector/market → compatible con `Sector?`/`Market?` (no requiere cambios salvo que el compilador infiera el tipo; usar `Company.Create(name, ticker, (Sector?)null, (Market?)null, Currency.USD)` si hay ambigüedad).

- [x] **Step 2:** Ejecutar para confirmar fallo de compilación:
`dotnet test src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj --filter "FullyQualifiedName~CompanyTests"`

---

### Task 3: Modificar la entidad `Company`

- [x] **Step 1:** En `src/backend/src/BigSchool.Domain/Entities/Company.cs`:
  - Cambiar propiedades: `public string? Sector` → `public Sector? Sector { get; private set; }` y `public string? Market` → `public Market? Market { get; private set; }`.
  - Cambiar firma: `public static Company Create(string name, string ticker, Sector? sector, Market? market, Currency currency)`.
  - En el constructor privado, cambiar los tipos de los parámetros `sector`/`market`.
  - En `Create`, eliminar la normalización de string (`IsNullOrWhiteSpace ? null : .Trim()`) y asignar `sector`/`market` directamente.

```csharp
public int IdCompany { get; private set; }
public string Name { get; private set; } = string.Empty;
public string Ticker { get; private set; } = string.Empty;
public Sector? Sector { get; private set; }
public Market? Market { get; private set; }
public Currency Currency { get; private set; }
// ...

public static Company Create(string name, string ticker, Sector? sector, Market? market, Currency currency)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("El nombre es obligatorio.", nameof(name));
    if (string.IsNullOrWhiteSpace(ticker))
        throw new ArgumentException("El ticker es obligatorio.", nameof(ticker));

    return new Company(
        name.Trim(),
        ticker.Trim().ToUpperInvariant(),
        sector,
        market,
        currency,
        EntityStatus.Active,
        DateTime.UtcNow);
}
```

- [x] **Step 2:** Compilar Domain y correr `CompanyTests` → PASS.

---

### Task 4: EF — `HasConversion<string>()` + migración no-op

- [x] **Step 1:** En `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/CompanyConfiguration.cs`, dentro de `ConfigureProperties`, sustituir:

```csharp
builder.Property(c => c.Sector).HasMaxLength(100);
builder.Property(c => c.Market).HasMaxLength(50);
```

por:

```csharp
builder.Property(c => c.Sector).HasConversion<string>().HasMaxLength(100);
builder.Property(c => c.Market).HasConversion<string>().HasMaxLength(50);
```

> Las columnas siguen siendo `varchar(100)`/`varchar(50)` nullable. EF persistirá el nombre del miembro (`"Technology"`, `"NASDAQ"`, `"BME"`…), idéntico a lo ya sembrado.

- [x] **Step 2:** Generar la migración:
```bash
dotnet ef migrations add AddSectorMarketEnums --project src/backend/src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj --startup-project src/backend/src/BigSchool.WebApi/BigSchool.WebApi.csproj --output-dir Persistence/Migrations
```

- [x] **Step 3:** **Verificar el `Up()` de la migración generada.** Lo esperado es que esté **vacío** (solo cambia el snapshot). Si EF emite `AlterColumn` para `Sector`/`Market` sin cambio real de tipo, es un no-op inofensivo y puede quedarse. Si reescribe los `InsertData` del seed (no debería, los valores string no cambian), **revisar que los valores siguen siendo idénticos**.

- [x] **Step 4:** Registrar la migración en `infra/docker/mysql/init.sql` → añadir su `MigrationId` al `INSERT IGNORE INTO __EFMigrationsHistory` (junto a `20260621101518_CreateCompanies`). Usar el timestamp real que genere EF.

- [x] **Step 5:** Compilar Infrastructure → BUILD SUCCEEDED.

---

### Task 5: Application — DTO, command, validator, handler, query

- [x] **Step 1:** `DTOs/Investments/CompanyDto.cs` (DTO de comando, tipos fuertes):

```csharp
using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Investments;

public record CompanyDto(int IdCompany, string Name, string Ticker, Sector? Sector, Market? Market, Currency Currency);
```

- [x] **Step 2:** `Commands/Investments/CreateCompany/CreateCompanyCommand.cs`:

```csharp
public record CreateCompanyCommand(string Name, string Ticker, Sector? Sector, Market? Market, Currency Currency)
    : IRequest<CompanyDto>;
```

- [x] **Step 3:** `CreateCompanyCommandValidator.cs` — sustituir las reglas de `MaximumLength` de Sector/Market por validación de enum:

```csharp
RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
RuleFor(x => x.Ticker).NotEmpty().MaximumLength(10);
RuleFor(x => x.Currency).IsInEnum();
RuleFor(x => x.Sector).IsInEnum();   // IsInEnum admite null en enum nullable
RuleFor(x => x.Market).IsInEnum();
```

- [x] **Step 4:** `CreateCompanyCommandHandler.cs` — el `Company.Create(...)` y el `new CompanyDto(...)` ahora pasan/devuelven `request.Sector`/`request.Market` (enums). No cambia la lógica, solo los tipos fluyen.

- [x] **Step 5:** `Queries/Investments/GetCompanies/GetCompaniesQuery.cs`:

```csharp
public record GetCompaniesQuery(Sector? Sector, Market? Market) : IRequest<IReadOnlyList<CompanyListItemDto>>;
```

En `GetCompaniesQueryHandler.cs`, pasar el filtro a Dapper como string (la columna es VARCHAR):

```csharp
parameters.Add("@Sector", request.Sector?.ToString());
parameters.Add("@Market", request.Market?.ToString());
```

El SQL no cambia (`@Sector IS NULL OR c.Sector = @Sector`).

> **`CompanyListItemDto` NO se toca:** sigue con `string Currency`, `string? Sector`/`Market`? — convención Dapper (no convierte VARCHAR→enum).

- [x] **Step 6:** Compilar Application → BUILD SUCCEEDED.

---

### Task 6: WebApi — controller

- [x] **Step 1:** En `Controllers/CompaniesController.cs`:
  - `CreateCompanyRequest`: `public record CreateCompanyRequest(string Name, string Ticker, Sector? Sector, Market? Market, Currency Currency);`
  - El `GET` de lista: cambiar el binding del filtro a enums nullable:
    ```csharp
    public async Task<IActionResult> Get([FromQuery] Sector? sector, [FromQuery] Market? market)
    {
        var result = await _mediator.Send(new GetCompaniesQuery(sector, market));
        // ...
    }
    ```
  - Añadir `using BigSchool.Domain.Enums;` si falta.

> Con `[FromQuery] Sector?`, `?market=NASDAQ` se enlaza a `Market.NASDAQ`. Un valor inválido (`?market=FOO`) produce 400 por fallo de model binding — comportamiento aceptable.

- [x] **Step 2:** Compilar WebApi → BUILD SUCCEEDED.

---

### Task 7: Ajustar tests de Application (handler)

- [x] **Step 1:** En `tests/BigSchool.Application.Tests/Commands/Investments/CreateCompanyCommandHandlerTests.cs`, sustituir los strings por enums:

```csharp
// Handle_NewTicker_CreatesCompany:
var command = new CreateCompanyCommand("Apple Inc.", "AAPL", Sector.Technology, Market.NASDAQ, Currency.USD);

// Handle_DuplicateTicker_ThrowsConflict (setup + command):
.ReturnsAsync(Company.Create("Apple Inc.", "AAPL", null, null, Currency.USD));
var command = new CreateCompanyCommand("Apple Inc.", "AAPL", null, null, Currency.USD);
```

Añadir `using BigSchool.Domain.Enums;` si falta. Para los `null` usar cast explícito si el compilador lo exige: `(Sector?)null, (Market?)null`.

- [x] **Step 2:** Ejecutar los dos tests → PASS.

---

### Task 8: Verificación global y commit

- [x] **Step 1:** Suites unitarias:
```bash
dotnet test src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj -c Release
dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release
```
Expected: PASS (incluidos `CompanyTests` y `CreateCompanyCommandHandlerTests`).

- [x] **Step 2:** Suite de integración completa (verifica que los E2E siguen verdes sin cambios, que el seed con `'BME'`/`'NASDAQ'` deserializa, y que el filtro `?market=NASDAQ` funciona):
```bash
dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Release
```
Expected: 57/57 PASS.

- [x] **Step 3:** Commit en la rama actual:
```bash
git add -A
git commit -m "refactor: Sector y Market como enums en Company (almacenados como string)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

> Si la convención del workflow lo requiere, abrir PR contra `develop`. Por ser un cambio cohesivo y pequeño, va en un único commit/PR sobre esta misma rama.

---

## Self-Review

- **BD/seed sin cambios:** confirmado — nombres de miembro == valores sembrados (`Technology`, `Financials`, `Energy`, `Retail`, `Automotive`; `NASDAQ`, `LSE`, `BME`). ✅
- **Migración:** necesaria por el snapshot de EF, pero `Up()` vacío/no-op; registrarla en `init.sql`. ✅
- **Consistencia de tipos:** command/DTO de comando con enums; `CompanyListItemDto` (query Dapper) permanece `string`. ✅
- **E2E intactos:** el JSON envía strings; `JsonStringEnumConverter` (ya registrado) deserializa Sector/Market igual que Currency. ✅
- **Riesgo:** valor de filtro GET inválido → 400 por model binding (aceptable). Valor de Sector/Market fuera de enum en un POST → 400 por `JsonStringEnumConverter` + `IsInEnum`. ✅
