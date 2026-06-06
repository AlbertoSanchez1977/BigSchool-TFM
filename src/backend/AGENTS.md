# AGENTS.md — Backend API

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

### Testing
- **Unitarios**: Domain y Application (mocking de Infrastructure)
- **Integración**: WebApi con TestServer + BD MySQL en Docker
- Framework: xUnit + FluentAssertions + Moq + Testcontainers
- Cobertura mínima: 80% en Domain y Application

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
