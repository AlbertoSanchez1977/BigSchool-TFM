# AGENTS.md — Backend API

## Documentación de Diseño (LEER ANTES DE IMPLEMENTAR)
- **Modelo de datos y convenciones BD**: `docs/02-backend-design.md`
- **Visión y alcance funcional**: `docs/00-vision.md`
- **Decisiones de arquitectura**: `docs/01-arquitectura.md`

## Tecnología
- .NET 8 C#
- Clean Architecture (4 capas)
- CQRS con MediatR
- DDD (Domain-Driven Design)
- Entity Framework Core + MySQL 8
- FluentValidation para validaciones
- AutoMapper o Mapster para mapeos
- JWT para autenticación

## Estructura del Proyecto

```
backend/
├── src/
│   ├── Domain/                → Entidades, Value Objects, Interfaces de repositorio
│   ├── Application/           → Casos de uso (Commands/Queries), DTOs, Interfaces
│   ├── Infrastructure/        → EF Core, Repositorios, Servicios externos
│   └── WebApi/               → Controllers, Middleware, Configuración
├── tests/
│   ├── Domain.Tests/
│   ├── Application.Tests/
│   └── Integration.Tests/
└── Backend.sln
```

## Bounded Contexts

### Finanzas Personales
- **Gastos**: Entidad con categoría, importe, fecha, descripción, recurrencia
- **Ingresos**: Entidad con fuente, importe, fecha, descripción, recurrencia
- **Balance**: Servicio de dominio que calcula balances por periodo

### Inversiones
- **Empresa**: Entidad con nombre, ticker, sector, mercado
- **Acción**: Value Object (cantidad, precio compra, fecha)
- **Valoración**: Entidad con precio actual, fecha, fuente
- **Cartera**: Aggregate Root que contiene las acciones del usuario

### IA/RAG (Anti-corruption Layer)
- Interfaz para comunicarse con el RAG Service (Python)
- DTOs de petición/respuesta
- No lógica de IA aquí, solo proxy HTTP

## Convenciones de Código

### Nombrado
- Clases y métodos: PascalCase (`ExpenseCommand`, `GetMonthlyBalance`)
- Variables y parámetros: camelCase (`expenseId`, `totalAmount`)
- Interfaces: Prefijo I (`IExpenseRepository`, `IPortfolioService`)
- Archivos: Mismo nombre que la clase principal

### Dominio (DDD) — Entidades y Eventos

**Constructores de entidades/agregados** (referencia canónica: `Domain/Entities/User.cs`):
- `protected EntityName() { }` — solo para EF Core (materialización), nunca para lógica de negocio.
- `private EntityName(...)` con **todos los campos** como parámetros: asigna y no valida.
- `public static EntityName Create(...)` — factory que **valida** las invariantes (lanzando `ArgumentException`/excepciones de dominio) y llama al constructor privado.
- **No** usar object initializer (`new Entity { ... }`) en las factories: construir siempre vía el constructor privado para mantener encapsulado el estado.

**Eventos de dominio** (`Domain/Events/`, implementan `IDomainEvent`):
- El record del evento recibe **la entidad de dominio completa** (es una referencia), no campos sueltos: `record TransactionCreatedEvent(Transaction Transaction) : IDomainEvent`. El handler extrae del agregado lo que necesite.
- Solo si un dato necesario **no** estuviera en la entidad se añade como parámetro extra del record.
- Esto desacopla el evento de la forma concreta del consumidor y evita re-firmar el evento cada vez que un handler necesita un campo distinto.

### CQRS
- Commands en `Application/Commands/{Feature}/`
- Queries en `Application/Queries/{Feature}/`
- Cada Command/Query tiene: Request, Handler, Validator
- Commands devuelven el ID creado o void
- Queries devuelven DTOs, nunca entidades de dominio

### Ejemplo de estructura de un feature:
```
Application/
├── Commands/
│   └── CreateExpense/
│       ├── CreateExpenseCommand.cs
│       ├── CreateExpenseCommandHandler.cs
│       └── CreateExpenseCommandValidator.cs
├── Queries/
│   └── GetMonthlyExpenses/
│       ├── GetMonthlyExpensesQuery.cs
│       ├── GetMonthlyExpensesQueryHandler.cs
│       └── MonthlyExpensesDto.cs
```

### Dapper y SQL (Queries / acceso con `IDbConnectionFactory`)
- **SQL como `private const string` a nivel de clase, en UPPERCASE terminado en `_QUERY`** (p. ej. `GETTRANSACTIONSUMMARY_QUERY`). Coherente con la convención de constantes UPPERCASE ya consolidada en el proyecto (p. ej. `MEMORY_COST`, `PURPOSE`). **No** incrustar SQL en línea dentro del método: la consulta vive como constante legible, reutilizable y localizable por nombre.
- **Parámetros con `DynamicParameters`**, no objetos anónimos: hace explícito cada parámetro (`parameters.Add("@Id", id)`), permite construirlos condicionalmente y deja la query como cadena pura.
- **No incrustar valores de enums persistidos como números mágicos** (p. ej. `IdStatus <> 4`): pasarlos como parámetro desde el propio enum — `parameters.Add("@StatusDeleted", EntityStatus.Deleted)` con `... WHERE IdStatus <> @StatusDeleted`. El SQL queda autoexplicativo y resistente a cambios del enum.
- El método solo orquesta: arma los parámetros, abre la conexión y ejecuta la query nombrada.

```csharp
public sealed class ExchangeRateReader
{
    private const string READRATEBYPAIRANDDATE_QUERY = @"SELECT Rate
                                                         FROM ExchangeRates
                                                         WHERE FromCurrency = @From
                                                           AND ToCurrency   = @To
                                                           AND RateDate     = @Date
                                                         LIMIT 1;";

    public async Task<decimal?> ReadRateAsync(Currency from, Currency to, DateOnly date)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@From", from.ToString());
        parameters.Add("@To", to.ToString());
        parameters.Add("@Date", date.ToDateTime(TimeOnly.MinValue).Date);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<decimal?>(READRATEBYPAIRANDDATE_QUERY, parameters);
    }
}
```

### Testing
- **Unitarios**: Domain y Application (mocking de Infrastructure)
- **Integración (E2E)**: BD MySQL real de docker-compose (BD dedicada `bigschool_test` migrada por EF, fixture `ICollectionFixture`) + `WebApplicationFactory<Program>` ejercitando el **pipeline completo** (JwtBearer, MediatR, FluentValidation, EF commands, Dapper queries, middleware de excepciones); WireMock.Net para fakear servidores HTTP externos.
- Framework: xUnit + FluentAssertions + Moq + WireMock.Net + MySqlConnector/Dapper + Microsoft.AspNetCore.Mvc.Testing
- Cobertura mínima: 80% en Domain y Application
- **Un fichero de test por clase bajo prueba**, nombrado `{ClaseBajoPrueba}Tests.cs`. No agrupar varias clases/handlers en un mismo fichero.

#### Reglas obligatorias de tests de integración E2E (Definition of Done por endpoint)
Todo endpoint nuevo o modificado DEBE acompañarse de tests E2E que cumplan:
1. **Un fichero por endpoint**, nombrado `{Verbo|Acción}{Recurso}Tests.cs` (p. ej. `RegisterTests`, `PostTransactionTests`, `GetTransactionsTests`), bajo carpeta por feature (`Auth/`, `Transactions/`). El plumbing común (ciclo de vida del factory, reset de BD, tipos de deserialización del envelope) vive en `IntegrationTestBase`; los helpers de cada feature en una base específica (`AuthEndpointTestBase`, `TransactionEndpointTestBase`).
2. **Al menos un test EXHAUSTIVO por feature** sobre la operación principal (POST/register), que valide:
   - **Request real serializado** (no se invoca el handler directamente): caza fallos de binding/serialización (enums string vía `JsonStringEnumConverter`, `DateOnly`, conversores Dapper como `DateOnlyTypeHandler`).
   - **Cada campo de la response** (contrato del frontend), incluida la forma serializada de enums y fechas.
   - **Persistencia física en BD** (consulta Dapper directa) verificando las columnas escritas.
3. **Foco en seguridad/auth (crítico)**: para flujos de credenciales, ejercitar el **hashing real** (round-trip register→login con la misma password) y el **JWT real** (token emitido usable contra un endpoint `[Authorize]`); nunca mockear `IPasswordHasher`/`IJwtService` en E2E. Verificar que el password se persiste hasheado (nunca en claro) y que usuario inexistente y password incorrecta devuelven el **mismo** error (no filtrar existencia).
4. **Tests de validación HTTP** de todos los códigos que el endpoint puede devolver: `401` (sin token en `[Authorize]`), `400` (`VALIDATION_ERROR` con `Field`; `INVALID_CREDENTIALS`), `404` (`ENTITY_NOT_FOUND`), `409` (`EMAIL_ALREADY_EXISTS` / `ConflictException`) cuando aplique.
5. **Operaciones de borrado**: verificar el **soft-delete** (la fila persiste con `IdStatus=Deleted`) y que las lecturas posteriores la ocultan (404).
6. **Deterministas**: para conversión multimoneda, sembrar la tasa en `ExchangeRates` con `RateDate = transactionDate` (nunca depender de la red); para servicios HTTP externos, WireMock.Net.

### API REST
- Versionado: `/api/v1/`
- Respuestas: Envelope pattern `{ data: T, errors: [], meta: {} }`
- HTTP Status codes estándar
- Swagger/OpenAPI obligatorio
- Paginación en listados: `?page=1&pageSize=20`

## Instrucciones para el Agente

1. Nunca poner lógica de negocio en los Controllers
2. Los Controllers solo orquestan: reciben request → envían al Mediator → devuelven response
3. Las entidades de Domain no tienen dependencias de Infrastructure
4. No usar Entity Framework en la capa de Domain ni Application
5. Cada cambio debe incluir test unitario mínimo
6. Usar records para DTOs y Value Objects cuando sea posible
7. No exponer IDs internos de BD, usar GUIDs públicos si es necesario
