# Diseño: Backend — User / Registro: moneda obligatoria + update de perfil (Spec 2)

**Fecha**: 2026-07-04
**Autor**: Alberto Sánchez
**Estado**: Diseño en revisión
**Módulo**: backend (módulo **Auth**)
**Backlog**: `docs/04-backend-tech-debt.md` (#1 moneda en registro, #2/A update + lectura de usuario).

---

## 1. Contexto y motivación

Dos huecos del módulo Auth que el frontend ya dio por hechos:
- **#1**: `RegisterCommand(Email, Password, FullName)` **no** pide moneda; `User.BaseCurrency` cae en
  `DEFAULT 'EUR'` → **todo usuario acaba en EUR**, aunque `User.Create(...)` **ya acepta** el parámetro
  `baseCurrency`. La app es multimoneda: la moneda base debe elegirse en el registro.
- **#2/A**: no existe `UserController` ni `GET /users/me` ni `PUT /users/me`. El frontend (plan 012,
  Task 15) construyó la pantalla **Profile** contra un mock, con el contrato ya escrito:
  `GET /users/me → {idUser, email, fullName, baseCurrency, lastLoginDate}` y
  `PUT /users/me {fullName, password?} → UserProfile`.

## 2. Objetivo y no-objetivos

**Objetivo**
- `BaseCurrency` (enum `Currency`) **obligatoria** en el registro.
- `GET /users/me` (Dapper) y `PUT /users/me` (EF, re-hash Argon2 si cambia password), en un
  `UsersController` bajo `/api/v1/users`.
- Métodos de dominio `User.UpdateProfile(fullName)` y `User.ChangePassword(hash, salt)`.

**No-objetivos**
- **`email` y `baseCurrency` son inmutables** tras el registro (§3).
- **No** se implementa cambio de email, borrado de cuenta, ni gestión de subcategorías (Spec 009).
- **Frontend fuera de alcance** (solo §6 como apunte): backend puro.

## 3. Decisiones de alcance (cerradas en revisión)

| Tema | Decisión |
|------|----------|
| Campos editables | `PUT /users/me` edita **solo** `fullName` y `password` (opcional). |
| Campos inmutables | **`email`** (identificador/login) y **`baseCurrency`** (afecta todos los snapshots en moneda base) se fijan en el registro y son **read-only**. `GET /users/me` los devuelve. |
| Ubicación | Nuevo **`UsersController`** (módulo Auth) en `/api/v1/users`, separando "sesión/credenciales" (`/auth`) de "perfil" (`/users`). |
| Moneda en registro | **Obligatoria** (`IsInEnum`); admite todo el enum `Currency` (EUR/USD/GBP/CHF/JPY). |
| Cambio de password | Solo re-hashea (Argon2). **No** invalida tokens ya emitidos (JWT stateless, sin store; válidos hasta expirar ~60 min). Limitación documentada. |

## 4. #1 — Moneda obligatoria en el registro

- **Command**: `RegisterCommand(string Email, string Password, string FullName, Currency BaseCurrency)`.
  Como `AuthController.Register` bindea el command directo y los enums viajan como **nombre string**
  (`JsonStringEnumConverter`), el body pasa a incluir `"baseCurrency": "USD"`.
- **Validator** (`RegisterCommandValidator`): añadir
  `RuleFor(x => x.BaseCurrency).IsInEnum().WithMessage("La moneda base es obligatoria y debe ser válida.")`.
  Un valor omitido bindea a `default(Currency) = 0`, que **no** es un miembro válido → `IsInEnum` lo
  rechaza (400 `VALIDATION_ERROR`).
- **Handler** (`RegisterCommandHandler`): pasar `request.BaseCurrency` a
  `User.Create(request.Email, hash, salt, request.FullName, request.BaseCurrency)` (el parámetro ya existe).
- ⚠️ **Cambio de contrato rompedor**: `POST /auth/register` ahora exige `baseCurrency`. Hay que
  **actualizar los E2E de register** (`RegisterTests` y bases de Auth) y cualquier **seed de usuario**
  demo para incluir la moneda. Sin este ajuste, la suite queda en rojo (es la señal de que el contrato
  cambió).

## 5. #2/A — Update + lectura de perfil

### 5.1 Dominio (Auth)
Métodos nuevos en `User` (el hash lo calcula Application y lo pasa ya resuelto, igual que `Create`):
```csharp
public void UpdateProfile(string fullName)          // valida no vacío; set FullName + UpdatedAt
public void ChangePassword(string hash, string salt) // set PasswordHash/Salt + UpdatedAt
```

### 5.2 Lectura — `GET /users/me` (Dapper)
- `GetMeQuery(int IdUser) : IRequest<UserProfileDto?>` en `Application/Auth/Queries/GetMe/`.
- `UserProfileDto(int IdUser, string Email, string FullName, string BaseCurrency, DateTime? LastLoginDate)`.
  `BaseCurrency` como **string** (Dapper no convierte CHAR(3)→enum; convención del repo para DTOs de
  lectura). SQL `private const … _QUERY` filtrando `IdUser = @IdUser AND IdStatus <> @StatusDeleted`.

### 5.3 Escritura — `PUT /users/me` (EF)
- `UpdateUserCommand(int IdUser, string FullName, string? Password) : IRequest<UserProfileDto>`.
- `UpdateUserCommandValidator`: `FullName` requerido, `MaximumLength(200)`; `Password` **opcional**
  pero, si viene, `MinimumLength(8).MaximumLength(100)` (`When(x => x.Password is not null, …)`) —
  espejo del registro.
- `UpdateUserCommandHandler`:
  ```csharp
  var user = await _userRepository.GetByIdAsync(request.IdUser, ct)
             ?? throw new NotFoundException(...);           // 404 si no existe
  user.UpdateProfile(request.FullName);
  if (request.Password is not null)
  {
      var (hash, salt) = _passwordHasher.HashPassword(request.Password);
      user.ChangePassword(hash, salt);
  }
  await _userRepository.UnitOfWork.SaveChangesAsync();
  return new UserProfileDto(user.IdUser, user.Email, user.FullName,
                            user.BaseCurrency.ToString(), user.LastLoginDate);
  ```
  (Devuelve `BaseCurrency.ToString()` → "EUR" — mismo shape string que el GET.)

### 5.4 Controller — `WebApi/Controllers/Auth/UsersController.cs`
`[ApiController] [Authorize] [Route("api/v1/users")]`, `UserId` vía `CurrentUser.GetId(User, _encryptor)`
(igual que `TransactionsController`/`PortfoliosController`):
| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/users/me` | `GetMeQuery(UserId)` → 200 `UserProfileDto` / 404 |
| PUT | `/users/me` | body `UpdateUserRequest(string FullName, string? Password)` → `UpdateUserCommand(UserId, …)` → 200 `UserProfileDto` |

Todo dentro del módulo **Auth** (User es su AR) → sin cruces de frontera; `ModuleBoundaryTests` no cambia.

## 6. Frontend (solo apunte — FUERA DE ALCANCE)

No se implementa aquí. Para una sesión de frontend posterior: selector de moneda **obligatorio** en el
registro (`register/page.tsx` + schema zod); pantalla Profile (Task 15) del mock a `GET/PUT /users/me`
(email y baseCurrency como labels read-only; fullName + password editables); retirar el `TODO`.

## 7. Fuera de alcance

- Cambio de email, borrado de cuenta, verificación de email.
- Invalidación de tokens al cambiar password (no hay store; JWT stateless).
- Subcategorías / re-modelado de `SubCategory` (Spec 009).
- Frontend (§6).

## 8. Verificación

- **Unit (Domain)**: `User.Create` con `baseCurrency` la persiste; `User.UpdateProfile` valida no
  vacío y actualiza `FullName`/`UpdatedAt`; `User.ChangePassword` cambia hash/salt/`UpdatedAt`.
- **Unit (Application)**: `RegisterCommandValidator` rechaza moneda omitida/ inválida (`IsInEnum`);
  `UpdateUserCommandValidator` (password corto solo cuando se envía); `UpdateUserCommandHandler`
  (re-hash solo si viene password; 404 si el usuario no existe).
- **Integración E2E** (`WebApplicationFactory` + MySQL `bigschool_test`):
  - `POST /auth/register` con `baseCurrency: "USD"` → persiste `BaseCurrency = USD` (Dapper directo);
    sin `baseCurrency` o inválida → 400 `VALIDATION_ERROR` con `field`.
  - `GET /users/me` `[Authorize]` (401 sin token) → devuelve `{idUser, email, fullName, baseCurrency,
    lastLoginDate}` del usuario del token.
  - `PUT /users/me` cambia `fullName`; con `password` → **round-trip real**: el login posterior con la
    **nueva** contraseña funciona y con la vieja falla (ejercita el hashing Argon2, sin mockear).
  - Ajustar los `RegisterTests`/bases de Auth existentes al nuevo contrato (moneda obligatoria).

---

## Referencias
- Backlog: `docs/04-backend-tech-debt.md`. Contrato frontend: plan 012 Task 15 (`/users/me`).
- Código a tocar: `Application/Auth/Commands/Register/*`, `Domain/Auth/Entities/User.cs`,
  nuevos `Application/Auth/{Queries/GetMe,Commands/UpdateUser}/*`, `WebApi/Controllers/Auth/UsersController.cs`.
- Convenciones: `src/backend/AGENTS.md` (Dapper `_QUERY`, factories, E2E DoD por endpoint).
