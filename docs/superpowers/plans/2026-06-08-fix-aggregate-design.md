# Corrección Diseño de Agregados DDD — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Corregir el diseño de agregados en el scaffolding existente para cumplir con DDD estricto: solo Aggregate Roots implementan `IAggregateRoot`, disparan eventos, y exponen métodos de mutación para sus entidades hijas.

**Architecture:** 
- **User** [AR] → posee Transaction, SubCategory, RagDocument
- **Portfolio** [AR] → posee Holding  
- **Company** [AR] → posee Valuation
- Las entidades hijas no tienen repositorio propio ni exponen mutación pública
- Solo los AR disparan Domain Events
- Las Queries (lectura) usan Dapper y no pasan por el AR

**Tech Stack:** .NET 8, xUnit + FluentAssertions

---

## Resumen de Tareas

| # | Tarea | Descripción |
|---|-------|-------------|
| 1 | Quitar IAggregateRoot de entidades hijas | Transaction, Holding, RagDocument |
| 2 | Eliminar repositorios de entidades hijas | ITransactionRepository, IHoldingRepository, IRagDocumentRepository |
| 3 | Actualizar IRepository constraint | Actualizar diseño del doc |
| 4 | Verificar build + tests | Confirmar que no se rompe nada |
| 5 | Actualizar docs/02-backend-design.md | Reflejar el modelo correcto de agregados |

---

### Task 1: Quitar IAggregateRoot de entidades hijas

**Files:**
- Modify: `src/backend/src/BigSchool.Domain/Entities/Transaction.cs`
- Modify: `src/backend/src/BigSchool.Domain/Entities/Holding.cs`
- Modify: `src/backend/src/BigSchool.Domain/Entities/RagDocument.cs`

- [ ] **Step 1: Modificar Transaction — quitar IAggregateRoot**

Contenido final de `Transaction.cs`:

```csharp
namespace BigSchool.Domain.Entities;

public class Transaction : BaseEntity
{
}
```

- [ ] **Step 2: Modificar Holding — quitar IAggregateRoot**

Contenido final de `Holding.cs`:

```csharp
namespace BigSchool.Domain.Entities;

public class Holding : BaseEntity
{
}
```

- [ ] **Step 3: Modificar RagDocument — quitar IAggregateRoot**

Contenido final de `RagDocument.cs`:

```csharp
namespace BigSchool.Domain.Entities;

public class RagDocument : BaseEntity
{
}
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "refactor: quitar IAggregateRoot de entidades hijas (Transaction, Holding, RagDocument)"
```

---

### Task 2: Eliminar repositorios de entidades hijas

**Files:**
- Delete: `src/backend/src/BigSchool.Application/Interfaces/Repositories/ITransactionRepository.cs`
- Delete: `src/backend/src/BigSchool.Application/Interfaces/Repositories/IHoldingRepository.cs`
- Delete: `src/backend/src/BigSchool.Application/Interfaces/Repositories/IRagDocumentRepository.cs`

- [ ] **Step 1: Eliminar ITransactionRepository.cs**

```bash
cd src/backend
rm src/BigSchool.Application/Interfaces/Repositories/ITransactionRepository.cs
```

- [ ] **Step 2: Eliminar IHoldingRepository.cs**

```bash
rm src/BigSchool.Application/Interfaces/Repositories/IHoldingRepository.cs
```

- [ ] **Step 3: Eliminar IRagDocumentRepository.cs**

```bash
rm src/BigSchool.Application/Interfaces/Repositories/IRagDocumentRepository.cs
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "refactor: eliminar repositorios de entidades hijas (solo ARs tienen repositorio)"
```

---

### Task 3: Limpiar DbContext de DbSets innecesarios como navegación directa

**Files:**
- Modify: `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs`

Nota: EF Core necesita `DbSet<T>` para generar tablas, pero conceptualmente el acceso a entidades hijas se hace a través de las navigation properties del AR. Mantenemos los DbSets para que EF Core genere las tablas correctamente pero añadimos un comentario clarificador.

- [ ] **Step 1: Añadir comentario en BigSchoolDbContext**

Reemplazar la sección de DbSets por:

```csharp
    // Aggregate Roots — acceso principal
    public DbSet<User> Users => Set<User>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    // Entidades hijas — DbSet necesario para EF Core migrations/queries
    // El acceso de escritura se hace siempre a través del Aggregate Root
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<RagDocument> RagDocuments => Set<RagDocument>();
```

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "refactor: reorganizar DbSets con comentarios de diseño DDD (ARs vs entidades hijas)"
```

---

### Task 4: Verificar build + tests existentes

**Files:** Ninguno nuevo.

- [ ] **Step 1: Build del proyecto completo**

```bash
cd src/backend
dotnet build --no-restore -v q
```

Expected: Build succeeded, 0 errors, 0 warnings relevantes

- [ ] **Step 2: Ejecutar todos los tests**

```bash
cd src/backend
dotnet test --no-restore -v q
```

Expected: Todos los tests existentes pasan (3 tests de Domain del scaffolding original)

- [ ] **Step 3: Commit (si hubo algún ajuste menor)**

Solo si fue necesario arreglar algo para compilar.

---

### Task 5: Actualizar docs/02-backend-design.md

**Files:**
- Modify: `docs/02-backend-design.md`

- [ ] **Step 1: Añadir sección de Aggregate Roots al documento de diseño**

Insertar después de la sección "Modelo de Datos" (antes de "Arquitectura CQRS") la siguiente sección:

```markdown
## Agregados DDD

### Reglas de Diseño

1. **Solo los Aggregate Roots implementan `IAggregateRoot`** y tienen repositorio propio
2. **Solo los AR disparan Domain Events** — las entidades hijas notifican al AR que algo cambió
3. **Las entidades hijas no exponen métodos públicos de mutación** — se acceden a través de su AR
4. **Las Queries (lectura) usan Dapper** y acceden directamente a tablas sin pasar por el AR
5. **Los Commands (escritura) cargan el AR** con sus hijos y mutan a través de métodos del AR

### Mapa de Agregados

| Aggregate Root | Entidades hijas | Repositorio |
|---|---|---|
| **User** | Transaction, SubCategory, RagDocument | `IUserRepository` |
| **Portfolio** | Holding | `IPortfolioRepository` |
| **Company** | Valuation | `ICompanyRepository` |

### Ejemplo de acceso a entidad hija (escritura)

```csharp
// Command: CreateTransaction
var user = await _userRepository.GetByIdWithTransactionsAsync(userId);
user.AddTransaction(type, amount, date, mainCategory, subCategoryId, description);
await _userRepository.UnitOfWork.SaveChangesAsync();
// El AR dispara TransactionCreatedEvent
```

### Ejemplo de lectura directa (Dapper)

```csharp
// Query: GetMonthlyExpenses — NO pasa por el AR
var sql = "SELECT * FROM Transactions WHERE IdUser = @UserId AND MONTH(Date) = @Month";
var transactions = await connection.QueryAsync<TransactionDto>(sql, new { UserId = userId, Month = month });
```
```

- [ ] **Step 2: Actualizar la sección de Interfaces en la estructura**

En la sección "Estructura Clean Architecture", reemplazar las interfaces de repositorios:

Cambiar:
```
│   │   │   ├── ITransactionRepository.cs
│   │   │   ├── ICompanyRepository.cs
│   │   │   ├── IPortfolioRepository.cs
│   │   │   ├── IHoldingRepository.cs
│   │   │   ├── IRagDocumentRepository.cs
```

Por:
```
│   │   │   ├── IUserRepository.cs       → AR User + sus hijos (Transactions, SubCategories, RagDocuments)
│   │   │   ├── ICompanyRepository.cs    → AR Company + Valuations
│   │   │   ├── IPortfolioRepository.cs  → AR Portfolio + Holdings
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "docs: actualizar diseño backend con modelo correcto de agregados DDD"
```

---

## Resultado Esperado

Al completar este plan:
- `IAggregateRoot` solo lo implementan: `User`, `Portfolio`, `Company`
- No existen repositorios para entidades hijas
- El documento de diseño refleja las reglas DDD correctas
- El build y los tests siguen pasando
- La base está lista para el Plan 1 (Cross-cutting + Auth) que usará `IUserRepository` como punto de acceso a Transactions y SubCategories del usuario
