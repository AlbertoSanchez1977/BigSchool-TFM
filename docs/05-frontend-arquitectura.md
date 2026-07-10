# BigSchool-TFM — Arquitectura del Frontend-Web

**Fecha**: 2026-07-09
**Estado**: Documentación de arquitectura (describe la estructura ya consolidada del código)
**Relación**: complementa `docs/03-frontend-design.md` (diseño visual/UX) y `docs/01-arquitectura.md` (ADRs globales). El detalle de scaffolding y convenciones de código vive en `src/frontend-web/AGENTS.md`.

> **Alcance de este documento.** Aquí se describe **cómo está construido** el frontend (capas, flujo
> de datos, invariantes), no cómo se ve (eso es `03-frontend-design.md`) ni las convenciones de
> nombrado (eso es el `AGENTS.md` del módulo). Sirve de referencia para que los desarrollos futuros
> respeten la estructura sin desviarse.

---

## 1. Idea rectora

Arquitectura **por capas con separación estricta entre acceso a datos y presentación**, en la misma
línea mental que el backend Clean Architecture del proyecto. El código lo hace explícito: los propios
comentarios comparan TanStack Query con `IMemoryCache + IHttpClientFactory` y `ApiError` con una
excepción de dominio tipada de C#.

Es, en la práctica, una **arquitectura hexagonal "ligera" para frontend**:

- el `apiClient` es el **adaptador de salida** (único punto que habla HTTP),
- los `services` son los **puertos por recurso** (traducen dominio ⇄ endpoints REST),
- los `hooks` son la **capa de aplicación** (cache, estados, CQRS lectura/escritura),
- `components` + `app` son la **capa de presentación**,
- `types/` y `lib/schemas/` (Zod) son los **contratos en los bordes**.

---

## 2. Las cinco capas

De fuera (transporte) hacia dentro (presentación):

```
app/          →  Routing + composición de página (App Router)
components/   →  Presentación: UI pura (ui/) + componentes de dominio
hooks/        →  Estado de servidor (React Query): el "pegamento"
services/     →  Clientes de recurso: dominio ⇄ endpoints REST
lib/api*      →  Transporte HTTP: fetch, envelope, auth, errores
```

### 2.1 Transporte — `lib/apiClient.ts` + `lib/api.ts`

Única puerta de salida HTTP. **Nadie hace `fetch` fuera de aquí.**

- `apiClient.ts` conoce el **envelope** del backend (`{ data, errors[], meta }`), inyecta el
  `Authorization: Bearer <jwt>`, desenvuelve `data` y normaliza los errores a `ApiError` tipado
  (`code` + `message` + `field?` + `status?`), pensado para pintar errores inline en formularios.
- `api.ts` expone el **singleton** `api` (`api.get`, `api.post`, `api.getWithMeta`…) ya configurado
  con la base URL de entorno y el token leído **en tiempo de petición** (no de importación → siempre
  actualizado).
- **Patrón clave — callback de 401 registrado** (`registerUnauthorizedHandler`): el cliente HTTP no
  depende del contexto de auth; es el `AuthProvider` quien registra el manejador al montar. Así se
  evita la dependencia circular `apiClient → AuthContext`. (Análogo a registrar un manejador de error
  centralizado en un `HttpClient` de C#.)

### 2.2 Servicios — `services/*Service.ts`

Un objeto por recurso: `companyService`, `transactionService`, `portfolioService`,
`holdingsService`, `notificationService`, `userService`.

Responsabilidad única: **mapear el lenguaje del dominio a URLs y DTOs**. Construir el query string,
desempaquetar el `meta` de paginación, saber que `period` viaja como el **nombre** del miembro del
enum (`ThreeMonths`, `OneYear`…) y no como `'3m'`/`'1y'`. Es el equivalente cliente de un
*repository*. **No saben nada de React.**

### 2.3 Estado de servidor — `hooks/use*.ts` sobre TanStack Query

La decisión arquitectónica más importante: **no hay store global de datos (Redux/Zustand); el
"estado" del servidor vive en la caché de React Query.** El estado local puntual usa Context API.

- Cada hook envuelve un service y aporta `queryKey`, `staleTime`, `enabled` e invalidación.
- **CQRS en cliente**: lectura con `useQuery` (`useTransactions`, `usePortfolios`…) separada de
  escritura con `useMutation` (`useTransactionMutations`, `usePortfolioMutations`…).
- `lib/queryKeys.ts` es la **factory de claves** (una sola fuente de verdad por recurso, para leer e
  invalidar sin divergir). **Deuda conocida**: hoy solo portfolios, companies y valuations pasan por
  la factory; el resto declara la key inline. Migrar el resto *cuando se toque*, no de una sentada.

### 2.4 Presentación — `components/`

Doble subdivisión:

- `components/ui/` → primitivas **sin dominio** (shadcn/ui sobre Base UI: button, card, input,
  table…). Reutilizables y "tontas".
- `components/{dominio}/` → `investments`, `transactions`, `market`, `charts`, `forms`, `auth`,
  `ai-scanner`, `sections`. Ya conocen el negocio pero **consumen datos solo vía hooks**, nunca
  services ni `fetch` directo.

### 2.5 Routing — `app/` (App Router)

- **Route groups como frontera de seguridad**: `(public)` (login, register, scope) y `(private)`
  (dashboard, expenses, investments, market, ai-scanner, contacts, emails, profile).
- **Protección client-side** vía `(private)/layout.tsx` + `AuthProvider`. **No hay `middleware.ts`**:
  la decisión es consciente porque no se hace SSR de datos sensibles. Si en el futuro se hiciera,
  habría que añadir un `middleware.ts`.

---

## 3. Capas transversales

| Carpeta | Rol | Nota |
|---|---|---|
| `types/` | DTOs y enums **espejo del backend** | Frontera de tipos. Se verifican contra el código fuente del backend, nunca de memoria (ver norma en `AGENTS.md`). |
| `lib/schemas/` (Zod) | Validación en el borde | Formularios con react-hook-form. Contrato de entrada. |
| `lib/auth/` | Sesión | `AuthProvider` (Context), `tokenStore`, `refreshPolicy` (refresco proactivo; no hay refresh token en backend). |
| `lib/{finance,dashboard,charts,transactions,investments}/` | Lógica de derivación/presentación **pura** | `derive.ts`, `aggregateByCategory.ts`, `labels.ts`… Sin React → tests rápidos con Vitest. |
| `lib/utils.ts`, `lib/dates.ts` | Utilidades transversales | Formateo, `cn()`, fechas (UTC). |

---

## 4. La regla de oro — flujo de datos unidireccional

**Cada capa solo habla con su vecina inmediata. Sin atajos.**

```
Componente  →  hook  →  service  →  api  →  backend
```

Invariantes que un desarrollo futuro **no debe romper**:

1. Un componente **nunca** hace `fetch` ni llama a un `service` directamente → siempre vía hook.
2. Un `service` **nunca** devuelve JSX ni usa React → solo dominio ⇄ HTTP.
3. **Toda** comunicación con el backend pasa por el `apiClient` (envelope + `ApiError`) y por
   TanStack Query (cache + estados carga/error/vacío).
4. Ningún color/dato literal se escribe fuera de su contrato (tokens de diseño; tipos de `types/`).

> Ejemplo vivo de la disciplina: `hooks/useAuth.ts` existe **solo** para re-exportar desde
> `lib/auth`, para que los componentes importen de `hooks/` y no de `lib/auth/AuthProvider` —
> separación de capas incluso cuando el contenido es trivial.

---

## 5. Correspondencia con el backend (modelo mental C#/.NET)

| Capa frontend | Equivalente mental backend |
|---|---|
| `apiClient` (adaptador de salida) | `HttpClient` centralizado + manejo de errores tipado |
| `services/*Service.ts` | Repositorios / clientes de recurso |
| `hooks` (useQuery/useMutation) | Capa de aplicación con cache; CQRS (queries vs commands) |
| `queryKeys.ts` | Claves de caché centralizadas |
| `components` + `app` | Presentación (Razor/partial views) |
| `types/` + `lib/schemas` (Zod) | DTOs + validación de entrada (FluentValidation) |
| `AuthProvider` (Context) | Scope de DI por árbol |

---

## 6. Testing (encaje con la arquitectura)

- **Lógica pura de `lib/`** → tests unitarios directos con **Vitest** (sin montar React).
- **Componentes** → **Vitest + React Testing Library** (comportamiento, no implementación ni estilos).
- **Flujos críticos** → **Playwright** E2E (login, crear gasto, ver gráficas, vender holding).
- No se testea contra `fetch` real: se testea la capa que corresponde (service, hook o componente).

---

## 7. Deudas y costuras arquitectónicas vigentes

Documentadas aquí para que no se confundan con bugs ni se "arreglen" por sorpresa:

1. **`queryKeys.ts` parcial** → migrar keys inline a la factory de forma incremental (§2.3).
2. **Protección de rutas solo cliente** → sin `middleware.ts` mientras no haya SSR sensible (§2.5).
3. **Costuras del backend** (FIFO a nivel empresa, companies sin `PUT/DELETE`, valuations sin
   `GET{id}`/`DELETE`, sin búsqueda server-side de empresas) → detalladas en `AGENTS.md` del módulo;
   son comportamiento aceptado, no carencias a tapar desde el frontend.

---

## 8. Referencias

- Diseño visual y UX: `docs/03-frontend-design.md`
- Convenciones de código, integración con backend y glosario React↔C#: `src/frontend-web/AGENTS.md`
- ADRs globales: `docs/01-arquitectura.md`
