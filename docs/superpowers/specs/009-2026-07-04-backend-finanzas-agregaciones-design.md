# Diseño: Backend — Finanzas: agregaciones + subcategorías + re-modelado SubCategory (Spec 3)

**Fecha**: 2026-07-04
**Autor**: Alberto Sánchez
**Estado**: Diseño en revisión
**Módulo**: backend (módulo **Finanzas**; toca **Auth** solo para *quitar* dependencia)
**Backlog**: `docs/04-backend-tech-debt.md` (#6 por categoría, #7 por mes/año con filtros, #8/C
subcategorías CRUD) + la **decisión Categorías→Finanzas** (spec 005 §7): re-modelar `SubCategory`.

---

## 1. Contexto y motivación

Tres huecos de Finanzas + una deuda estructural que la Spec 0 dejó **cerrada como decisión pero
pendiente de ejecución**:

- **#6**: no hay agregación por categoría — hoy la gráfica "por categoría" se calcula **en el
  navegador** (`aggregateByCategory`, plan 012 Task 7; spec 003 §4.2 hueco #1).
- **#7**: `GET /transactions/monthly-chart?year=` agrupa por mes de **un solo año**, sin filtro de
  categoría ni rango.
- **#8/C**: crear/borrar subcategorías personalizadas estaba marcado "⬜ Plan futuro"
  (`02-backend-design.md`).
- **Re-modelado `SubCategory`**: en el plan 018 `SubCategory` se **movió** al namespace de Finanzas
  pero sigue siendo **entidad hija del AR `User`** (Auth). Por eso `ModuleBoundaryTests` tiene la
  frontera **Domain.Auth → Domain.Finanzas relajada** a propósito (la línea de `Auth` omite
  `Finanzas`, con comentario que apunta a esta spec). Aquí se **re-modela** `SubCategory` como AR
  independiente y se **reactiva** el guard test.

## 2. Objetivo y no-objetivos

**Objetivo**
- `SubCategory` = **Aggregate Root independiente** de Finanzas (referencia al usuario por `IdUser`).
- `POST /categories/sub`, `DELETE /categories/sub/{id}` (#8/C).
- `GET /transactions/by-category` (#6, por `MainCategory`).
- `GET /transactions/monthly` **nuevo** (#7, con `from/to/category/type`).
- **Validación de rango** en #6 y #7: `from ≤ to` y span **máximo 4 años**.
- **Reactivar** la frontera `Domain.Auth ⊥ Domain.Finanzas` en `ModuleBoundaryTests`.

**No-objetivos**
- `GET /transactions/monthly-chart?year=` **no se toca** (lo usa el dashboard ya construido).
- **No** se editan subcategorías (solo crear + soft-delete). **No** se borran las globales/predefinidas.
- **Frontend fuera de alcance** (§8 apunte): backend puro.

## 3. Decisiones (cerradas)

| Tema | Decisión |
|------|----------|
| #7 | **Endpoint nuevo** `GET /transactions/monthly?from&to&category&type`; `monthly-chart?year=` intacto. |
| #6 | Agrupado por **`MainCategory`** (total por categoría principal). DTO `{idMainCategory, name, total}`. |
| **Validación de rango (#6 y #7)** | Si vienen ambos, `from ≤ to` y `to ≤ from + 4 años`; si se excede → **400 `VALIDATION_ERROR`**. |
| `SubCategory.IdUser` | **Referencia suave por Id** (nullable; NULL = global predefinida), **sin FK dura** — coherente con `EmailLog` (Spec 007). |
| Unicidad de subcategoría | Se valida en el **command handler** (nombre único por `MainCategory` frente a las del usuario **y** las globales). |
| Test de frontera | Se **reactiva** (Auth vuelve a prohibir Finanzas) una vez `User` no referencia Finanzas. |

## 4. Re-modelado de `SubCategory` (el corazón de la spec)

Hoy: `SubCategory` es hija de `User` — `User` tiene `_subCategories` + `AddSubCategory`, y `IdUser` es
**propiedad sombra** (`builder.Property<int?>("IdUser")`, FK gestionada en `UserConfiguration`).

**Destino: AR independiente en Finanzas.**

- **`Domain/Finanzas/Entities/SubCategory.cs`**: implementa `IAggregateRoot`; `IdUser` pasa a
  **propiedad explícita** `public int? IdUser { get; private set; }` (NULL = global). Factory
  **pública** `Create(MainCategory mainCategory, string name, int? idUser)` (valida nombre no vacío).
  Método `Delete()` (soft-delete → `IdStatus = Deleted`). `IsGlobal => IdUser is null`.
- **`Domain/Auth/Entities/User.cs`**: **eliminar** `_subCategories`, `SubCategories` y
  `AddSubCategory`, y los `using` de Finanzas (`SubCategory`, `MainCategory`,
  `DuplicateSubCategoryDomainException`). Tras esto **`User` no referencia ningún tipo de Finanzas**.
- **`Infrastructure/Finanzas/Persistence/Configurations/SubCategoryConfiguration.cs`**: sombra →
  `builder.Property(s => s.IdUser)`; sin navegación a `User`. Mantener el query filter y demás props.
- **`UserConfiguration`**: quitar la relación `User`→`SubCategory` (la FK que vivía ahí).
- **Seed** (`SharedKernel/Persistence/Extensions/SeedDataExtensions.SeedSubCategories`): sigue
  sembrando las predefinidas con `IdUser = null` (ahora sobre la entidad standalone).
- **Repositorio nuevo** `ISubCategoryRepository : IRepository<SubCategory, int>` (Finanzas) con
  `ExistsActiveAsync(int? idUser, MainCategory mainCategory, string name)` para la guarda de unicidad.
- **Migración `RemodelSubCategoryAggregate`**: se espera **soltar la FK `SubCategories→Users`**
  (referencia suave); la **columna `IdUser` y el resto del esquema no cambian**. Verificar con
  `dotnet ef migrations has-pending-model-changes` que no aparezcan cambios inesperados.
- **`ModuleBoundaryTests`**: cambiar la línea de dominio
  `[InlineData("BigSchool.Domain.Auth", new[] { "BigSchool.Domain.Investments" })]` →
  `[InlineData("BigSchool.Domain.Auth", new[] { "BigSchool.Domain.Finanzas", "BigSchool.Domain.Investments" })]`
  y retirar el comentario de excepción. Debe quedar **verde** (prueba de que la frontera se cerró).

> `GetCategoriesQuery` **no cambia**: ya lee por Dapper `WHERE IdUser IS NULL OR IdUser = @IdUser`,
> agnóstico al re-modelado. La unicidad, que antes vivía en `User.AddSubCategory`, se traslada al
> `CreateSubCategoryCommandHandler`.

## 5. #8/C — Subcategorías CRUD (Finanzas)

`CategoriesController` (añadir `IUserIdEncryptor` para `UserId`, como Transactions/Portfolios):

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/categories/sub` | `CreateSubCategoryCommand(UserId, MainCategory, Name)` → 201/200 `SubCategoryDto` / 409 duplicada |
| DELETE | `/categories/sub/{id}` | `DeleteSubCategoryCommand(UserId, Id)` → 200 / 404 / 403-guard |

- **Create**: validator (`Name` requerido `MaxLength(100)`, `MainCategory` `IsInEnum`). Handler:
  guarda de unicidad (`ExistsActiveAsync(idUser, mainCategory, name)` contra las del usuario **y** las
  globales) → si existe, `DuplicateSubCategoryDomainException` (409); si no,
  `SubCategory.Create(mainCategory, name, idUser)` → `AddAsync` → `SaveChangesAsync`.
- **Delete**: cargar por id; **404** si no existe; **no se puede borrar** una **global** (`IdUser is null`)
  ni la de **otro** usuario ni `IsDefault` → guard (404/403); si es del usuario, `subCategory.Delete()`
  (soft-delete) → `SaveChangesAsync`.

## 6. #6 — `GET /transactions/by-category` (Dapper)

- `GetTransactionsByCategoryQuery(int IdUser, TransactionType? Type, DateOnly? From, DateOnly? To)`
  → `IReadOnlyList<CategoryTotalDto>`.
- `CategoryTotalDto(int IdMainCategory, string MainCategory, decimal Total)`.
- **Validator** `GetTransactionsByCategoryQueryValidator` (FluentValidation; corre por el
  `ValidationBehavior`, que aplica también a queries): `From ≤ To` y `To ≤ From + 4 años` cuando ambos
  vengan (regla compartida, §7.1).
- SQL (consolida sobre `BaseAmount`, espejo de `GetTransactionSummary`):
  ```sql
  SELECT IdMainCategory, SUM(BaseAmount) AS Total
  FROM Transactions
  WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
    AND (@Type IS NULL OR Type = @Type)
    AND (@From IS NULL OR TransactionDate >= @From)
    AND (@To   IS NULL OR TransactionDate <= @To)
  GROUP BY IdMainCategory
  ORDER BY Total DESC;
  ```
  El handler mapea `IdMainCategory` (int) → nombre del enum `MainCategory` (como `GetCategories`).
- Controller: `GET /transactions/by-category?type&from&to` en `TransactionsController`.

## 7. #7 — `GET /transactions/monthly` nuevo (Dapper)

- `GetMonthlyQuery(int IdUser, DateOnly? From, DateOnly? To, MainCategory? Category, TransactionType? Type)`
  → `IReadOnlyList<MonthlyChartPointDto>` (reutiliza el DTO `{Year, Month, Income, Expense}`).
- **Validator** `GetMonthlyQueryValidator`: misma regla de rango (§7.1).
- SQL (agrupa por `YEAR, MONTH` sobre `BaseAmount`, mismo split Income/Expense que `monthly-chart`):
  ```sql
  ... WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
        AND (@From IS NULL OR TransactionDate >= @From)
        AND (@To   IS NULL OR TransactionDate <= @To)
        AND (@Category IS NULL OR IdMainCategory = @Category)
        AND (@Type IS NULL OR Type = @Type)
      GROUP BY YEAR(TransactionDate), MONTH(TransactionDate)
      ORDER BY Year, Month;
  ```
- Carpeta nueva `Application/Finanzas/Queries/Transactions/GetMonthly/`. Controller:
  `GET /transactions/monthly?from&to&category&type` en `TransactionsController`.

> Estos endpoints son **series/summaries → NO se paginan** (Spec 00 §8).

### 7.1 Regla de rango compartida (#6 y #7)

Validación de entrada: si `From` y `To` vienen ambos, deben cumplir `From ≤ To` **y**
`To ≤ From.AddYears(4)` (span máximo **4 años**, coherente con la ventana "últimos 4 años" del
frontend). Si se excede → **400 `VALIDATION_ERROR`** con `field`. Si falta uno de los dos, no se
aplica el tope (consulta abierta por un lado). Se implementa como un predicado FluentValidation
reutilizado por ambos validators (p. ej. un método estático `DateRange.IsWithin4Years(from, to)`).

## 8. Frontend (solo apunte — FUERA DE ALCANCE)

No se implementa aquí. Futuro: sustituir `aggregateByCategory` (cálculo en cliente) por
`GET /transactions/by-category`; la gráfica mensual filtrada por `GET /transactions/monthly`; y un
gestor de subcategorías (crear/borrar) usando `POST/DELETE /categories/sub`.

## 9. Fuera de alcance

- Editar subcategorías; borrar/renombrar globales; re-seed de predefinidas.
- Tocar `monthly-chart?year=` (dashboard) o `GetCategories`.
- Frontend (§8).

## 10. Verificación

- **Unit (Domain)**: `SubCategory.Create` (valida, fija `IdUser`, `IsGlobal`); `SubCategory.Delete`
  (soft-delete). Los tests de `User.AddSubCategory` desaparecen (ya no existe); `SubCategory` se testea
  **directamente como AR** (ya no vía `User` — actualiza la nota [[no-internalsvisibleto-test-via-ar]]).
- **Unit (Application)**: validators de create/delete; **regla de rango 4 años** (#6 y #7: `from>to`
  y span > 4 años → inválido); handler de create (unicidad → 409 contra usuario y globales); handler
  de delete (404 inexistente, guard de global/ajena/default).
- **Arquitectura**: `ModuleBoundaryTests` con `Domain.Auth` prohibiendo `Finanzas` **en verde**
  (frontera cerrada) — es la prueba canónica del re-modelado.
- **Migración**: `has-pending-model-changes` sin sorpresas; la migración solo suelta la FK.
- **Integración E2E** (`WebApplicationFactory` + MySQL `bigschool_test`):
  - `POST /categories/sub` crea la subcategoría del usuario; duplicada (usuario o global) → 409;
    `GET /categories` la incluye. `DELETE /categories/sub/{id}` soft-borra la propia; global/ajena → 404/403.
  - `GET /transactions/by-category` con datos multi-categoría/multi-moneda sembrados → totales sobre
    `BaseAmount`, filtros `type/from/to`; rango > 4 años → **400**.
  - `GET /transactions/monthly` agrupa por año-mes con `from/to/category/type`; sin filtros ≈ toda la
    serie; rango > 4 años → **400**.
  - Los E2E existentes de `monthly-chart`/`summary`/`categories` siguen verdes (sin cambios de contrato).

---

## Referencias
- Backlog: `docs/04-backend-tech-debt.md`. Decisión Categorías→Finanzas: spec 005 §7. Test relajado:
  `tests/BigSchool.Architecture.Tests/ModuleBoundaryTests.cs` (línea `Domain.Auth`).
- Espejo de queries: `GetTransactionSummaryQueryHandler`, `GetMonthlyChartQueryHandler`,
  `GetCategoriesQueryHandler`. Convenciones: `src/backend/AGENTS.md`.
