# Frontend-Web Iteración 2: integración con el Backend — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: usa `superpowers:subagent-driven-development`
> (recomendado) o `superpowers:executing-plans` para ejecutar este plan tarea a tarea. Los pasos
> usan checkbox (`- [ ]`) para seguimiento. **Gate del repo**: 1 tarea = 1 rama
> `feature/024-frontend-integracion-taskN` (desde `develop`) = 1 PR = revisión humana = merge → next
> (skill `feature-branch-workflow`).

**Goal:** Cablear el Frontend-Web a las capacidades que el Backend ya expone (specs 006–010) y añadir
las pantallas nuevas de "Mercado" (Companies + Valuations), retirando los mocks/costuras del MVP.

**Architecture:** Next.js App Router + TanStack Query. Cada recurso tiene `types/` (espejo del DTO
real) → `services/` (llamadas al `api` de `lib/api`) → `hooks/` (useQuery/useMutation) → página/
componente (presentación con estados carga/error/vacío). Se reutilizan los patrones ya montados en
el MVP; no se reescribe arquitectura.

**Tech Stack:** TypeScript strict, TanStack Query v5, react-hook-form + Zod, Recharts, shadcn/ui,
Tailwind v4 (tokens semánticos), Vitest + React Testing Library, Playwright (E2E).

**Spec de referencia:** `docs/superpowers/specs/011-2026-07-06-frontend-web-backend-integracion-design.md`.

---

## Convenciones compartidas (LEER antes de cada tarea)

1. **Verificar tipos contra el backend (obligatorio).** Antes de consumir un DTO, abre el
   Controller/DTO real en `src/backend/...` y ajusta el `types/*`. Nunca de memoria. Los enums viajan
   como **nombre string** (`JsonStringEnumConverter`); los DTO de **query (Dapper)** suelen devolver
   `currency`/enums como **string** y números para categorías/tipos, mientras que los DTO de
   **command** devuelven los enums tipados. Controllers relevantes:
   - `src/backend/src/BigSchool.WebApi/Controllers/Notifications/{ContactsController,EmailsController}.cs`
   - `src/backend/src/BigSchool.WebApi/Controllers/Finance/TransactionsController.cs`
   - `src/backend/src/BigSchool.WebApi/Controllers/Auth/{AuthController,UsersController}.cs`
   - `src/backend/src/BigSchool.WebApi/Controllers/Investments/{PortfoliosController,CompaniesController}.cs`

2. **Cliente HTTP** (`src/lib/apiClient.ts`, singleton en `src/lib/api.ts`):
   - `api.get<T>(path)`, `api.post<T>(path, body)`, `api.put<T>(path, body)`, `api.delete<T>(path)`
     → devuelven `envelope.data` desenvuelto y lanzan `ApiError { code, message, field?, status }`.
   - `api.getWithMeta<T>(path)` → `{ data, meta }` para **endpoints paginados**; el `meta` es
     `{ page, pageSize, totalCount, totalPages }`.

3. **Patrón de servicio** (ej. `src/services/portfolioService.ts`): objeto con métodos async que
   llaman a `api.*`. Query string con `URLSearchParams` (ver `transactionService.buildParams`).

4. **Patrón de hook** (ej. `src/hooks/usePortfolios.ts`): `useQuery({ queryKey, queryFn })` para
   lecturas; `useMutation({ mutationFn, onSuccess: invalidateQueries })` para escrituras. Diferir
   fetch con `enabled` cuando la vista no está activa. Para N llamadas en paralelo, `useQueries`.

5. **Patrón de página**: estados **carga** (Skeleton), **error** (bloque `text-destructive`),
   **vacío** (bloque con CTA) y **datos**. Tokens semánticos (verde/rojo solo para signo de dinero,
   azul acción). Altas/acciones → **modal centrado** (`Dialog`), ver
   `src/components/transactions/transaction-sheet.tsx`. Detalle → breadcrumb `← Lista` con `<Link>`.

6. **Testing (Vitest + RTL)**: los tests viven en `tests/unit/...` espejando `src/`. Mockear el
   módulo `@/lib/api` con `vi.mock`. Para hooks, envolver en un `QueryClientProvider` de test.
   Ejecutar: `npm run test -- <ruta>`. Los **E2E Playwright** (`tests/e2e/`) críticos (login, crear
   gasto, vender holding) deben seguir **verdes**: `npm run test:e2e`.

7. **Commits**: Conventional Commits en español. Cada tarea abre PR a `develop` con la skill
   `feature-branch-workflow`. Comando de arranque de cada tarea:
   ```bash
   git checkout develop && git pull && git checkout -b feature/024-frontend-integracion-taskN
   ```

8. **Comando de verificación global** (antes de cada commit de tarea, en `src/frontend-web/`):
   `npm run lint && npm run typecheck && npm run test`.

---

## Resumen de tareas

| Task | Título | Módulo | PRs |
|------|--------|--------|-----|
| 1 | Contactos + Emails a endpoints reales | Notifications | 1 |
| 2 | Finance: migrar Gráfica A al backend + añadir Gráfica B | Finance | 1 |
| 3 | Moneda en registro | Auth | 1 |
| 4 | Perfil (GET/PUT /users/me) | Auth | 1 |
| 5 | Paginación reutilizable + lista de carteras | Transversal | 1 |
| 6 | Renombrar / borrar cartera | Investments | 1 |
| 7 | Holdings `*Original` + summary global (Dashboard) | Investments | 1 |
| 8 | Pantallas "Mercado": Companies (listado/detalle/crear) + combobox | Investments | 1 |
| 9 | Valuations: listado + crear + gráfico de serie | Investments | 1 |

> Nota de orden vs spec: la spec numeró T3 como "3a/3b"; aquí son las tareas **3** (registro) y **4**
> (perfil). La paginación de la spec (T4) se implementa como tarea **5** (componente reutilizable +
> lista de carteras); Companies/Valuations nacen paginados en las tareas 8/9.

---

## Task 1: Contactos + Emails a endpoints reales (módulo Notifications)

**Contexto:** hoy el formulario público guarda en `localStorage` (`hooks/useContacts.ts`) y
`hooks/useEmails.ts` deriva un mock. El backend ya expone `POST /contacts` (público), `GET /contacts`
y `GET /emails`. La navegación **ya existe** (`ProfileDropdown` enlaza `/contacts` y `/emails`): esta
tarea solo cambia las fuentes de datos. **Verifica primero** `ContactDto` y `EmailLogDto` en
`Controllers/Notifications/*` y `BigSchool.Application/Notifications/...`.

**Files:**
- Create: `src/types/notifications.ts`
- Create: `src/services/notificationService.ts`
- Modify: `src/hooks/useContacts.ts` (reemplaza `localStorage` por query/mutation)
- Modify: `src/hooks/useEmails.ts` (reemplaza mock por query real)
- Modify: `src/components/sections/contact-form.tsx` (POST real)
- Modify: `src/app/(private)/contacts/page.tsx` (consumir query: loading/error/empty/datos)
- Modify: `src/app/(private)/emails/page.tsx` (consumir query)
- Test: `tests/unit/hooks/useContacts.test.tsx`, `tests/unit/hooks/useEmails.test.tsx`

- [x] **Step 1: Tipos espejo del backend** — `src/types/notifications.ts`:

```typescript
// Espejo de ContactDto / EmailLogDto (Notifications). Verificar campos y forma de fecha
// contra Controllers/Notifications/*. Los DTO de query (Dapper) devuelven fechas como
// "YYYY-MM-DDTHH:mm:ss" (string). Ajustar si el backend difiere.
export interface Contact {
  idContact: number
  fullName: string
  email: string
  message: string
  createdAt: string     // ISO
}

export interface CreateContactDto {
  fullName: string
  email: string
  message: string
}

// EmailLog: idUser NULL = email de sistema; NOT NULL = del usuario (bienvenida, etc.)
export interface EmailLog {
  idEmailLog: number
  toAddress: string
  subject: string
  body: string
  type: string          // p.ej. "Welcome" | "ContactAck" — confirmar enum real
  status: string        // p.ej. "Sent" — confirmar
  createdAt: string     // ISO
}
```

- [x] **Step 2: Servicio** — `src/services/notificationService.ts`:

```typescript
import { api } from '@/lib/api'
import type { Contact, CreateContactDto, EmailLog } from '@/types/notifications'

export const notificationService = {
  createContact: (data: CreateContactDto) => api.post<Contact>('/contacts', data),
  listContacts:  () => api.get<Contact[]>('/contacts'),
  listEmails:    () => api.get<EmailLog[]>('/emails'),
}
```

- [x] **Step 3: Escribir tests que fallan** — `tests/unit/hooks/useContacts.test.tsx` y
  `useEmails.test.tsx`. Mockear `@/lib/api`, envolver en `QueryClientProvider` de test:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useContacts } from '@/hooks/useContacts'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn() } }))
import { api } from '@/lib/api'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

describe('useContacts', () => {
  beforeEach(() => vi.clearAllMocks())
  it('devuelve la lista de contactos del backend', async () => {
    vi.mocked(api.get).mockResolvedValue([
      { idContact: 1, fullName: 'Ana', email: 'a@x.com', message: 'hola', createdAt: '2026-07-06T10:00:00' },
    ])
    const { result } = renderHook(() => useContacts(), { wrapper })
    await waitFor(() => expect(result.current.contacts).toHaveLength(1))
    expect(api.get).toHaveBeenCalledWith('/contacts')
  })
})
```

- [x] **Step 4: Verificar que fallan** — `npm run test -- tests/unit/hooks/useContacts.test.tsx`.
  Esperado: FAIL (el hook aún lee `localStorage`).

- [x] **Step 5: Reescribir `useContacts.ts`** — reemplazar todo el fichero por query + mutation:

```typescript
'use client'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { notificationService } from '@/services/notificationService'
import type { CreateContactDto } from '@/types/notifications'

const CONTACTS_KEY = ['contacts'] as const

export function useContacts() {
  const q = useQuery({ queryKey: CONTACTS_KEY, queryFn: () => notificationService.listContacts() })
  return { contacts: q.data ?? [], isLoading: q.isLoading, isError: q.isError }
}

export function useCreateContact() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateContactDto) => notificationService.createContact(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: CONTACTS_KEY }),
  })
}
```

- [x] **Step 6: Reescribir `useEmails.ts`**:

```typescript
'use client'
import { useQuery } from '@tanstack/react-query'
import { notificationService } from '@/services/notificationService'

export function useEmails() {
  const q = useQuery({ queryKey: ['emails'], queryFn: () => notificationService.listEmails() })
  return { emails: q.data ?? [], isLoading: q.isLoading, isError: q.isError }
}
```

- [x] **Step 7: Actualizar `contact-form.tsx`** — sustituir el `saveContact` de `localStorage` por
  `useCreateContact()`; en `onSubmit` llamar `mutate(values, { onSuccess: toast.success, onError:
  (e) => toast.error(e.message) })`. Mantener el diseño y los estados del formulario.

- [x] **Step 8: Actualizar `contacts/page.tsx` y `emails/page.tsx`** — consumir los hooks nuevos y
  añadir estados **loading** (Skeleton) y **error** (además del vacío ya existente). `emails/page.tsx`
  deja de recibir `user`; ajustar `formatDate`/campos al DTO real (`createdAt`, `toAddress`,
  `subject`, `type`).

- [x] **Step 9: Retirar restos de mock** — eliminar `LS_KEY`/`loadContacts`/`saveContact` y la
  interfaz `ContactSubmission` (grep global para asegurar que nadie los importa).

- [x] **Step 10: Verificar y commitear**

```bash
npm run lint && npm run typecheck && npm run test
git add src/types/notifications.ts src/services/notificationService.ts src/hooks/useContacts.ts src/hooks/useEmails.ts src/components/sections/contact-form.tsx "src/app/(private)/contacts/page.tsx" "src/app/(private)/emails/page.tsx" tests/unit/hooks/useContacts.test.tsx tests/unit/hooks/useEmails.test.tsx
git commit -m "feat(notifications): conectar contactos y emails con el backend real"
```

- [x] **Step 11: Abrir PR** a `develop` (skill `feature-branch-workflow`).

---

## Task 2: Finance — migrar Gráfica A al backend + añadir Gráfica B (módulo Finance)

**Contexto — leer con atención (decisión del humano):**
- **Gráfica A NO cambia de aspecto ni de comportamiento.** Es la gráfica actual "barras agrupadas por
  año × categoría, últimos 4 años" (`components/charts/category-bars.tsx`). Se **conserva intacta** el
  componente, la página y `lib/charts/aggregateByCategory.ts` (función pura, se queda como
  fallback/tests). **Solo se reescribe el cuerpo de `hooks/useCategoryChart.ts`**: en vez de traer
  transacciones (`pageSize=5000`, que el backend **topa en 100** → hoy nunca agrega los 4 años
  completos = **bug**), hace **4 llamadas a `by-category`** (una por año) y las mapea al **mismo**
  `CategoryAggregation`. Esto es exactamente la migración que prometía la costura del hook, y **arregla
  el bug** del límite de 100.
- **Gráfica B es lo único nuevo**: barras apiladas por mes (segmentos = categorías), filtro solo por
  Tipo, N llamadas a `monthly` (una por MainCategory del tipo) sobre la ventana de 4 años.

**Verifica** en `TransactionsController.cs`: `GET /transactions/by-category` (→
`CategoryTotalDto { idMainCategory, mainCategory, total }`, filtros `from,to,type`) y
`GET /transactions/monthly` (→ `MonthlyChartPointDto { year, month, income, expense }`, filtros
`from,to,category,type`).

**Categorías por tipo** (rango del enum): gasto = `EssentialExpenses, Investment, Savings, Donations,
Luxuries, Education, Amortizations`; ingreso = `Salary, Rentals, Dividends, Other`.

**Files:**
- Modify: `src/types/transactions.ts` (añadir `CategoryTotal`)
- Modify: `src/services/transactionService.ts` (añadir `byCategory`, `monthly`)
- Create: `src/lib/finance/categoriesByType.ts`
- Modify: `src/hooks/useCategoryChart.ts` (**solo el cuerpo**; misma firma y mismo output `CategoryAggregation`)
- Create: `src/hooks/useMonthlySeries.ts`
- Create: `src/components/charts/monthly-stacked-bars.tsx`
- Modify: `src/app/(private)/expenses/page.tsx` (pestaña Gráficas: selector de año + Gráfica A actual + Gráfica B)
- **Conservar sin tocar**: `src/components/charts/category-bars.tsx`, `src/lib/charts/aggregateByCategory.ts`
- Test: `tests/unit/hooks/useCategoryChart.test.tsx` (reescribir aserciones), `tests/unit/hooks/useMonthlySeries.test.tsx`

- [x] **Step 1: Tipos + constante de categorías por tipo**

`src/types/transactions.ts` (añadir):
```typescript
// GET /transactions/by-category → CategoryTotalDto (Dapper: mainCategory como nombre string)
export interface CategoryTotal {
  idMainCategory: number
  mainCategory: string   // 'EssentialExpenses' | ... (mc.ToString())
  total: number
}
```

`src/lib/finance/categoriesByType.ts`:
```typescript
import type { MainCategory, TransactionType } from '@/types/enums'

// Derivado del rango del enum backend: 1-7 gasto, 10-13 ingreso.
export const CATEGORIES_BY_TYPE: Record<TransactionType, MainCategory[]> = {
  Expense: ['EssentialExpenses', 'Investment', 'Savings', 'Donations', 'Luxuries', 'Education', 'Amortizations'],
  Income:  ['Salary', 'Rentals', 'Dividends', 'Other'],
}
```

- [x] **Step 2: Métodos de servicio** — `src/services/transactionService.ts` (añadir métodos e
  imports de `CategoryTotal`, `MonthlyChartPoint`, `MainCategory`):

```typescript
// GET /transactions/by-category?from&to&type
async byCategory(from: string, to: string, type: TransactionType): Promise<CategoryTotal[]> {
  const qs = new URLSearchParams({ from, to, type }).toString()
  return api.get<CategoryTotal[]>(`/transactions/by-category?${qs}`)
},

// GET /transactions/monthly?from&to&category&type
async monthly(from: string, to: string, type: TransactionType, category?: MainCategory): Promise<MonthlyChartPoint[]> {
  const p = new URLSearchParams({ from, to, type })
  if (category) p.set('category', category)
  return api.get<MonthlyChartPoint[]>(`/transactions/monthly?${p.toString()}`)
},
```

- [x] **Step 3: Reescribir el test de `useCategoryChart`** — ahora debe afirmar 4 llamadas a
  `by-category` (una por año) y el mapeo a `CategoryAggregation`:

```tsx
// mock api.get; el hook hace 4 llamadas by-category y agrupa por categoría × año.
it('pide by-category una vez por año de la ventana de 4 y mapea a CategoryAggregation', async () => {
  vi.mocked(api.get).mockResolvedValue([{ idMainCategory: 1, mainCategory: 'EssentialExpenses', total: 100 }])
  const { result } = renderHook(() => useCategoryChart('Expense', 2026, { enabled: true }), { wrapper })
  await waitFor(() => expect(api.get).toHaveBeenCalledTimes(4))
  expect(api.get).toHaveBeenCalledWith(expect.stringContaining('from=2023-01-01'))
  expect(api.get).toHaveBeenCalledWith(expect.stringContaining('from=2026-01-01'))
  await waitFor(() => expect(result.current.data.years).toEqual([2023, 2024, 2025, 2026]))
  expect(result.current.data.rows[0].category).toBe('EssentialExpenses')
})
```

- [x] **Step 4: Verificar fallo** — `npm run test -- tests/unit/hooks/useCategoryChart.test.tsx` → FAIL.

- [x] **Step 5: Reescribir el cuerpo de `useCategoryChart.ts`** (misma firma y mismo output; la página
  y `category-bars.tsx` no se tocan):

```typescript
'use client'
import { useMemo } from 'react'
import { useQueries } from '@tanstack/react-query'
import { transactionService } from '@/services/transactionService'
import type { CategoryAggregation, CategoryYearTotals } from '@/lib/charts/aggregateByCategory'
import type { MainCategory, TransactionType } from '@/types/enums'

const WINDOW_YEARS = 4

export interface UseCategoryChartResult {
  data: CategoryAggregation
  isLoading: boolean
  isError: boolean
}

// La costura ahora tira del backend: 4 llamadas by-category (una por año). El backend
// agrega en SQL, así que desaparece el límite de 100 filas que rompía la agregación cliente.
export function useCategoryChart(
  type: TransactionType,
  referenceYear: number = new Date().getFullYear(),
  options?: { enabled?: boolean },
): UseCategoryChartResult {
  const years = Array.from({ length: WINDOW_YEARS }, (_, i) => referenceYear - (WINDOW_YEARS - 1 - i))

  const results = useQueries({
    queries: years.map((y) => ({
      queryKey: ['transactions', 'by-category', y, type],
      queryFn: () => transactionService.byCategory(`${y}-01-01`, `${y}-12-31`, type),
      enabled: options?.enabled ?? true,
    })),
  })

  const data = useMemo<CategoryAggregation>(() => {
    const map = new Map<string, Record<number, number>>()
    years.forEach((y, i) => {
      for (const ct of results[i].data ?? []) {
        const totals = map.get(ct.mainCategory) ?? Object.fromEntries(years.map((yr) => [yr, 0]))
        totals[y] = ct.total
        map.set(ct.mainCategory, totals)
      }
    })
    const rows: CategoryYearTotals[] = [...map.entries()]
      .map(([category, totals]) => ({ category: category as MainCategory, totals }))
      .sort((a, b) => {
        const ta = years.reduce((s, yr) => s + a.totals[yr], 0)
        const tb = years.reduce((s, yr) => s + b.totals[yr], 0)
        return tb !== ta ? tb - ta : a.category.localeCompare(b.category)
      })
    return { years, rows }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [results.map((r) => r.data), type, referenceYear])

  return {
    data,
    isLoading: results.some((r) => r.isLoading),
    isError: results.some((r) => r.isError),
  }
}
```

- [x] **Step 6: Verificar que pasa** — repetir Step 4 → PASS. La Gráfica A ya funciona contra el
  backend sin tocar `category-bars.tsx` ni la página.

- [x] **Step 7: Test + implementación `useMonthlySeries.ts`** — para el tipo dado, una query por cada
  `MainCategory` de `CATEGORIES_BY_TYPE[type]` sobre la ventana de 4 años (`from` = 1-ene del año más
  antiguo, `to` = 31-dic del año de referencia). Test: 7 llamadas para `Expense`. Implementación:

```typescript
'use client'
import { useQueries } from '@tanstack/react-query'
import { transactionService } from '@/services/transactionService'
import { CATEGORIES_BY_TYPE } from '@/lib/finance/categoriesByType'
import type { MonthlyChartPoint } from '@/types/transactions'
import type { MainCategory, TransactionType } from '@/types/enums'

const WINDOW_YEARS = 4

export interface CategoryMonthlySeries { category: MainCategory; points: MonthlyChartPoint[] }

export function useMonthlySeries(type: TransactionType, referenceYear: number, options?: { enabled?: boolean }) {
  const from = `${referenceYear - (WINDOW_YEARS - 1)}-01-01`
  const to = `${referenceYear}-12-31`
  const categories = CATEGORIES_BY_TYPE[type]
  const results = useQueries({
    queries: categories.map((c) => ({
      queryKey: ['transactions', 'monthly', c, type, referenceYear],
      queryFn: () => transactionService.monthly(from, to, type, c),
      enabled: options?.enabled ?? true,
    })),
  })
  const data: CategoryMonthlySeries[] = categories.map((category, i) => ({ category, points: results[i].data ?? [] }))
  return { data, isLoading: results.some((r) => r.isLoading), isError: results.some((r) => r.isError) }
}
```

- [x] **Step 8: Componente Gráfica B** — ~~`src/components/charts/monthly-stacked-bars.tsx`: `BarChart`
  apilado (Recharts). Transformar `CategoryMonthlySeries[]` a filas por mes `{ label: "MMM YY",
  [category]: value }` (una fila por cada (año,mes) de la ventana). Una `<Bar stackId="a">` por
  categoría, color con la paleta `--chart-*` (ciclar si hay >4, usar el campo `income` o `expense`
  del punto según el `type`). Estado vacío (`emptyLabel`). Reusar el wrapper responsive y el tooltip
  HTML de `components/charts/category-bars.tsx` como referencia de estilo.~~
  **Revisado tras revisión humana del PR #162** (el diseño original —una sola gráfica apilada por
  categoría— no era el esperado): en su lugar, `src/components/charts/monthly-category-bars.tsx`
  renderiza **una gráfica por categoría** (desagregada), con los **12 meses en el eje X** y **una
  barra por año agrupada** dentro de cada mes (no apilada) — mismo patrón visual que
  `category-bars.tsx` pero con los ejes girados (allí el eje X es la categoría y el año agrupa; aquí
  el eje X es el mes). El componente `monthly-stacked-bars.tsx` se elimina.

- [x] **Step 9: Pestaña Gráficas en `expenses/page.tsx`** — añadir un **selector de año de
  referencia** (máx = año actual) que alimenta ambas gráficas. Mantener el conmutador Gastos/Ingresos
  y la **Gráfica A actual** (`useCategoryChart` + `<CategoryBars>`) tal cual, pasándole el año
  seleccionado. Debajo, añadir la **Gráfica B**: una rejilla con **una `<MonthlyCategoryBars>` por
  cada categoría** de `useMonthlySeries`, ambas gráficas con `enabled: tab === 'charts'`. No se
  elimina ningún componente existente de la Gráfica A.

- [x] **Step 10: Verificar y commitear**

```bash
npm run lint && npm run typecheck && npm run test
git add -A
git commit -m "feat(finance): migrar gráfica por categoría al backend (fix límite 100) y añadir serie mensual apilada"
```

- [x] **Step 11: PR** a `develop`.

---

## Task 3: Moneda base obligatoria en Registro (módulo Auth)

**Contexto:** `POST /auth/register` ya acepta `baseCurrency` y `AuthResponse` ya devuelve `currency`
(el `AuthProvider` ya lo guarda). Falta el selector en el formulario y arrastrar el campo por la
cadena `register-form → useAuth.register → RegisterDto`. **Verifica** `RegisterCommand`/`RegisterRequest`
en `AuthController.cs` (nombre exacto del campo: `baseCurrency`).

**Files:**
- Modify: `src/types/auth.ts` (añadir `baseCurrency` a `RegisterDto`)
- Modify: `src/lib/schemas/auth.ts` (añadir `baseCurrency` a `registerSchema`)
- Modify: `src/components/auth/register-form.tsx` (Select de moneda + pasar el campo)
- Test: `tests/unit/schemas/auth.test.ts`

- [x] **Step 1: Tipo** — en `src/types/auth.ts`, añadir a `RegisterDto`: `baseCurrency: Currency`
  (importar `Currency` de `@/types/enums`). Verificar el nombre exacto del campo contra el request.

- [x] **Step 2: Test que falla** — el schema exige `baseCurrency`:

```typescript
import { registerSchema } from '@/lib/schemas/auth'
it('rechaza registro sin moneda', () => {
  const r = registerSchema.safeParse({ fullName: 'Ana', email: 'a@x.com', password: '12345678', confirmPassword: '12345678' })
  expect(r.success).toBe(false)
})
it('acepta registro con moneda', () => {
  const r = registerSchema.safeParse({ fullName: 'Ana', email: 'a@x.com', password: '12345678', confirmPassword: '12345678', baseCurrency: 'EUR' })
  expect(r.success).toBe(true)
})
```

- [x] **Step 3: Verificar fallo** — `npm run test -- tests/unit/schemas/auth.test.ts` → FAIL.

- [x] **Step 4: Schema** — en `registerSchema` añadir dentro del `.object({...})`:
  `baseCurrency: z.enum(['EUR', 'USD', 'GBP', 'CHF', 'JPY'], { message: 'Selecciona una moneda' })`.

- [x] **Step 5: Verificar que pasa** — repetir Step 3 → PASS.

- [x] **Step 6: UI en `register-form.tsx`** — añadir un campo `<Select>` (shadcn) de moneda entre
  email y password. Como `Select` no es un `<input>` nativo, usar `Controller` de react-hook-form
  (mismo patrón que el `Select` de moneda ya existente en `transaction-sheet.tsx`, no el combobox de
  empresa que aún no existe). Pasar el valor en `onSubmit`. Opciones: las 5 de `Currency`.
  **Cambio respecto al plan**: en vez de repetir el array `['EUR','USD','GBP','CHF','JPY']` una
  tercera vez (ya estaba duplicado en `transaction-sheet.tsx`), se centraliza en
  `types/enums.ts` como `export const CURRENCIES = [...] as const` (un union type de TS no existe en
  runtime, así que hace falta el array `as const` para poder iterarlo en un `<Select>`/`z.enum()`) y
  `Currency` pasa a derivarse de él (`typeof CURRENCIES[number]`). `transaction-sheet.tsx` y
  `registerSchema` se actualizan para importar la misma constante — una sola fuente de verdad.

- [x] **Step 7: Verificar E2E** — **bug real encontrado y corregido** (no solo ajuste de test): el
  panel flotante de login/registro (`navbar-public.tsx`) cierra al detectar un click "fuera" de su
  `authAreaRef`; el desplegable del `<Select>` de moneda se pinta en un **Portal** (fuera del árbol
  DOM del panel), así que elegir cualquier moneda se interpretaba como "click fuera" y **cerraba el
  panel entero antes de enviar el formulario** — afecta a usuarios reales, no solo a los tests.
  Corregido en `handleOutside` ignorando los clicks dentro de `[data-slot="select-content"]`. Además,
  el test "contraseñas no coinciden" necesitó también seleccionar moneda: el `.refine()` de
  `confirmPassword===password` en Zod **no se ejecuta si el resto del `.object()` ya falló** (aquí,
  por `baseCurrency` ausente), así que sin moneda el error de "no coinciden" nunca llegaba a
  generarse. Ajustados los 3 specs que registran un usuario (`login.spec.ts`, `create-expense.spec.ts`,
  `sell-holding.spec.ts`) para seleccionar `EUR` tras rellenar el formulario. `npm run test:e2e:local`
  (config local, backend+frontend ya en marcha) → 9+2 tests PASS.

- [x] **Step 8: Commit + PR**

```bash
npm run lint && npm run typecheck && npm run test
git add -A && git commit -m "feat(auth): selector de moneda base obligatorio en el registro"
```

---

## Task 4: Perfil — GET/PUT /users/me (módulo Auth)

**Contexto:** `profile/page.tsx` es un mock (feedback optimista). El backend ya expone
`GET /users/me → { idUser, email, fullName, baseCurrency, lastLoginDate }` y
`PUT /users/me { fullName, password? } → UserProfile`. **Verifica** `UsersController.cs` +
`GetMe`/`UpdateUser` DTOs.

**Files:**
- Create: `src/types/users.ts`, `src/services/userService.ts`, `src/hooks/useProfile.ts`
- Modify: `src/app/(private)/profile/page.tsx` (datos reales + PUT)
- Test: `tests/unit/hooks/useProfile.test.tsx`

- [x] **Step 1: Tipos** — `src/types/users.ts`: verificado que `UserProfileDto` real es
  `(int IdUser, string Email, string FullName, string BaseCurrency, DateTime? LastLoginDate)` — un
  DTO de query Dapper, `BaseCurrency` es `string` en C# (no el enum), pero se sigue tipando como
  `Currency` en TS porque el valor en runtime es siempre un código válido.

```typescript
import type { Currency } from './enums'
export interface UserProfile {
  idUser: number
  email: string
  fullName: string
  baseCurrency: Currency   // command DTO → enum string; verificar (query Dapper podría ser string)
  lastLoginDate: string | null
}
export interface UpdateUserDto {
  fullName: string
  password?: string | null
}
```

- [x] **Step 2: Servicio** — `src/services/userService.ts`:

```typescript
import { api } from '@/lib/api'
import type { UserProfile, UpdateUserDto } from '@/types/users'
export const userService = {
  me:     () => api.get<UserProfile>('/users/me'),
  update: (data: UpdateUserDto) => api.put<UserProfile>('/users/me', data),
}
```

- [x] **Step 3: Test que falla + hook** — `useProfile.test.tsx` (mockeando `@/services/userService`,
  no `@/lib/api` directamente — mismo patrón ya establecido en `useTransactions.test.tsx` y en las
  Tasks 1/2), luego `src/hooks/useProfile.ts`:

```typescript
'use client'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { userService } from '@/services/userService'
import type { UpdateUserDto } from '@/types/users'

const ME_KEY = ['users', 'me'] as const
export function useProfile() {
  return useQuery({ queryKey: ME_KEY, queryFn: () => userService.me() })
}
export function useUpdateProfile() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: UpdateUserDto) => userService.update(data),
    onSuccess: (updated) => qc.setQueryData(ME_KEY, updated),
  })
}
```

- [x] **Step 4: Página** — en `profile/page.tsx`: sustituir los `InfoRow` mock por `useProfile()`
  (mostrar `email`, `baseCurrency`, `lastLoginDate` formateada; estados loading/error). En `onSubmit`
  llamar `useUpdateProfile().mutate({ fullName, password: values.password || null }, { onSuccess,
  onError: (e) => mostrar ApiError.message })`. Quitar los comentarios `TODO (deuda técnica)`.
  **Añadido, no estaba en el plan**: `ProfileDropdown`/navbar leen `fullName` de `AuthProvider`
  (memoria + `localStorage.bs_full_name`), no de esta query — sin sincronizarlo, tras editar el
  nombre seguirían mostrando el viejo hasta el próximo login/refresh de token. Nuevo método
  `useAuth().updateFullName(fullName)` (y `tokenStore.updateFullName`, mismo patrón read-modify-write
  que `incrementRefreshCount`) llamado en el `onSuccess` de la mutación.

- [x] **Step 5: Verificar y commit + PR**

```bash
npm run lint && npm run typecheck && npm run test
git add -A && git commit -m "feat(auth): perfil de usuario contra GET/PUT /users/me (retira mock)"
```

---

## Task 5: Componente de paginación reutilizable + lista de carteras (transversal)

**Contexto:** extraer el patrón de paginación de `expenses/page.tsx` (líneas ~360-384) a un componente
reutilizable y aplicarlo a la lista de carteras. `GET /portfolios` pasa a paginado
(`api.getWithMeta`). **Verifica** que `PortfoliosController.GET` acepta `page/pageSize` y devuelve
`meta`.

**Files:**
- Create: `src/types/pagination.ts` (mover aquí `PageMeta`)
- Create: `src/components/ui/pagination.tsx`
- Modify: `src/services/portfolioService.ts` (`list` paginado con `getWithMeta`)
- Modify: `src/hooks/usePortfolios.ts` (aceptar `{ page, pageSize }`)
- Modify: `src/types/portfolios.ts` (añadir `PagedPortfolios`)
- Modify: `src/app/(private)/investments/page.tsx` (estado `page` + `<Pagination>`)
- Modify: `src/app/(private)/expenses/page.tsx` (usar `<Pagination>` en vez del bloque inline)
- Test: `tests/unit/components/pagination.test.tsx`

- [x] **Step 1: Tipo compartido** — `src/types/pagination.ts`:

```typescript
export interface PageMeta { page: number; pageSize: number; totalCount: number; totalPages: number }
```
Reexportar desde `transactions.ts` para no romper imports existentes:
`export type { PageMeta } from './pagination'` (y eliminar la definición duplicada de `PageMeta` allí).

- [x] **Step 2: Test que falla** — `pagination.test.tsx`: renderiza rango "1–20 de 45", Anterior
  deshabilitado en page 1, click en Siguiente llama `onPageChange(2)`.

- [x] **Step 3: Componente** — `src/components/ui/pagination.tsx`:

```tsx
'use client'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui/button'

interface PaginationProps {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  onPageChange: (page: number) => void
}

export function Pagination({ page, pageSize, totalCount, totalPages, onPageChange }: PaginationProps) {
  if (totalPages <= 1) return null
  return (
    <div className="mt-4 flex items-center justify-between text-sm text-muted-foreground">
      <span>
        {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} de {totalCount}
      </span>
      <div className="flex gap-2">
        <Button variant="outline" size="sm" onClick={() => onPageChange(page - 1)} disabled={page <= 1}>
          <ChevronLeft className="h-4 w-4" /> Anterior
        </Button>
        <Button variant="outline" size="sm" onClick={() => onPageChange(page + 1)} disabled={page >= totalPages}>
          Siguiente <ChevronRight className="h-4 w-4" />
        </Button>
      </div>
    </div>
  )
}
```

- [x] **Step 4: Verificar que pasa** el test.

- [x] **Step 5: Servicio + tipo paginado** — en `portfolios.ts` añadido `PagedPortfolios`;
  `portfolioService.list({page,pageSize})` usa `api.getWithMeta<PortfolioListItem[]>('/portfolios?...')`,
  mapeado igual que `transactionService.list`. Verificado `GET /portfolios` real (acepta
  `page`/`pageSize`, ya devolvía `meta` que el frontend simplemente ignoraba hasta ahora).

- [x] **Step 6: Hook** — `usePortfolios({ page, pageSize })` con `queryKey: ['portfolios', page, pageSize]`.
  `useCreatePortfolio` invalida `['portfolios']` (prefijo, sin cambios — TanStack Query ya hacía match
  parcial de queryKey).

- [x] **Step 7: Páginas** — `investments/page.tsx`: `page` state + `<Pagination>`, consumiendo
  `data.items`. `expenses/page.tsx`: bloque inline sustituido por `<Pagination>`.
  **No estaba en el plan**: `dashboard/page.tsx` también consumía `usePortfolios()` (sin argumentos,
  esperando un array) para el mini-resumen de inversiones — roto por el cambio de firma/shape.
  Ajustado a `usePortfolios({ page: 1, pageSize: 100 })` + `.items` (es un widget de resumen, no un
  listado paginado; no lleva `<Pagination>` propia).

- [x] **Step 8: Verificar y commit + PR**

```bash
npm run lint && npm run typecheck && npm run test && npm run test:e2e
git add -A && git commit -m "feat(ui): componente de paginación reutilizable y lista de carteras paginada"
```

---

## Task 6: Renombrar / borrar cartera (módulo Investments)

**Contexto:** el backend ya expone `PUT /portfolios/{id}` (nombre) y `DELETE /portfolios/{id}`
(soft-delete con **guard fiscal**: 409 si hay holdings abiertos). Acciones en la **cabecera del
detalle** (`investments/[id]`). **Verifica** los request/response de `PUT`/`DELETE` en
`PortfoliosController.cs`.

**Files:**
- Modify: `src/types/portfolios.ts` (añadir `RenamePortfolioDto`)
- Modify: `src/services/portfolioService.ts` (`rename`, `remove`)
- Create: `src/hooks/usePortfolioMutations.ts` (`useRenamePortfolio`, `useDeletePortfolio`)
- Modify: `src/app/(private)/investments/[id]/page.tsx` (acciones en cabecera + modales)
- Test: `tests/unit/hooks/usePortfolioMutations.test.tsx`

- [x] **Step 1: Tipo + servicio** — **desviación del plan**: `PUT /portfolios/{id}` real devuelve
  `ApiResponse.Success()` **sin data** (`RenamePortfolioCommand : IRequest`, no `IRequest<T>`), no un
  `Portfolio` como asumía el snippet. `rename`/`remove` tipados `Promise<void>`.

- [x] **Step 2: Test que falla + hooks** — verificada la `queryKey` real del detalle en
  `usePortfolioDetail` (`useHoldings.ts`): `['portfolios', id]` (no `['portfolio', id]` como
  apuntaba el comentario del snippet). Usada tal cual en `usePortfolioMutations.ts`:

```typescript
'use client'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { portfolioService } from '@/services/portfolioService'
import type { RenamePortfolioDto } from '@/types/portfolios'

export function useRenamePortfolio(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: RenamePortfolioDto) => portfolioService.rename(id, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['portfolios'] })
      qc.invalidateQueries({ queryKey: ['portfolio', id] }) // ajustar a la key real del detalle
    },
  })
}
export function useDeletePortfolio() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => portfolioService.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['portfolios'] }),
  })
}
```
Test: mock `@/lib/api`; comprobar que `rename` hace `put('/portfolios/1', {name})` y `remove` hace
`delete('/portfolios/1')`.

- [x] **Step 3: UI en la cabecera de `investments/[id]/page.tsx`** — `DropdownMenu` (⋯, icono
  `MoreVertical`, mismo componente que `profile-dropdown.tsx`) junto al botón "Añadir holding", con
  "Renombrar" y "Eliminar":
  - **Renombrar**: modal centrado con el nombre precargado → `useRenamePortfolio(portfolioId).mutate({
    name })`, toast, cerrar.
  - **Eliminar**: modal de confirmación → `useDeletePortfolio().mutate(portfolioId, { onSuccess:
    () => router.push('/investments'), onError: (e) => toast.error(e instanceof ApiError ? e.message
    : ...) })`. El 409 del guard fiscal llega como `ApiError.message`; se muestra tal cual.
  **Ajuste sobre el plan**: el `DropdownMenuTrigger` de Base UI ya es en sí mismo el elemento
  interactivo (como en `profile-dropdown.tsx`) — se estiliza con `buttonVariants({ variant: 'outline',
  size: 'icon' })` en vez de anidar un `<Button>` dentro con un `render` prop (evita depender de si
  `Menu.Trigger` soporta ese patrón de composición, sin verificarlo primero).

- [x] **Step 4: Verificar y commit + PR**

```bash
npm run lint && npm run typecheck && npm run test
git add -A && git commit -m "feat(investments): renombrar y borrar cartera (guard fiscal en delete)"
```

---

## Task 7: Holdings `*Original` + summary global en Dashboard (módulo Investments)

**Contexto:** `GET /portfolios/{id}/performance` ya devuelve los campos `*Original`; el
`PerformanceTab` ya los pinta con fallback `—`. Falta materializar el tipo y añadir
`marketValueOriginal`, y cablear `GET /portfolios/summary` al mini-resumen del Dashboard. **Verifica**
`PortfolioPerformanceDto`/`HoldingPerformanceDto` y `GET /portfolios/summary` en `PortfoliosController.cs`.

**Files:**
- Modify: `src/types/portfolios.ts` (quitar `TODO`, marcar `*Original` presentes, `PortfoliosSummary`)
- Modify: `src/services/portfolioService.ts` (`summary`)
- Create: `src/hooks/usePortfoliosSummary.ts`
- Modify: `src/app/(private)/investments/[id]/page.tsx` (`PerformanceTab`: añadir `marketValueOriginal`)
- Modify: `src/app/(private)/dashboard/page.tsx` (mini-resumen desde `/portfolios/summary`)
- Test: `tests/unit/hooks/usePortfoliosSummary.test.tsx`

- [x] **Step 1: Tipos** — verificado `HoldingPerformanceDto` real: los 4 campos `*Original`
  (`BuyOriginalCurrency`, `CostBasisOriginal`, `MarketValueOriginal`, `UnrealizedPnLOriginal`) son
  **no-nullable** en el backend, confirmado; quitado el `TODO` y los `?`, añadido `marketValueOriginal:
  number`. **Desviación del plan**: `InvestmentsSummaryDto` real tiene un campo extra,
  `PortfolioCount: int`, que el snippet del plan no incluía — añadido a `PortfoliosSummary`.

- [x] **Step 2: Servicio + hook**

```typescript
// portfolioService.ts
summary: () => api.get<PortfoliosSummary>('/portfolios/summary'),
```
```typescript
// hooks/usePortfoliosSummary.ts
'use client'
import { useQuery } from '@tanstack/react-query'
import { portfolioService } from '@/services/portfolioService'
export function usePortfoliosSummary() {
  return useQuery({ queryKey: ['portfolios', 'summary'], queryFn: () => portfolioService.summary() })
}
```
Test: mock api, comprobar `get('/portfolios/summary')`.

- [x] **Step 3: `PerformanceTab`** — añadida la celda "Valor mercado original" (grid de 3×3 en vez
  de 3×2). Retirados los `!`/fallbacks `— ` de coste/PnL original: al ser campos garantizados por el
  backend, se renderizan directamente sin condicional.

- [x] **Step 4: Dashboard** — el mini-resumen usaba `derivePortfolioTotals` (suma en cliente sobre
  `usePortfolios`, no `/portfolios/{id}/performance`). Sustituido por `usePortfoliosSummary()`;
  `portfolioCur` pasa a leer `invSummary.baseCurrency` en vez de `portfolios[0].realizedPnLCurrency`
  (más correcto: no depende de que exista al menos 1 cartera en la página actual). `derivePortfolioTotals`
  y su tipo `PortfolioTotals` **eliminados** de `lib/dashboard/derive.ts` (código muerto sin más
  consumidores) junto con sus tests — decisión confirmada con el humano antes de borrar.

- [x] **Step 5: Verificar y commit + PR**

```bash
npm run lint && npm run typecheck && npm run test
git add -A && git commit -m "feat(investments): campos *Original en performance y summary global en dashboard"
```

---

## Task 8: Pantallas "Mercado" — Companies (módulo Investments)

**Contexto:** nueva sección `/market` (catálogo global). Listado paginado + crear empresa + detalle.
El `<Select>` de "añadir holding" pasa a **combobox typeahead** alimentado por `?pageSize=100` +
filtro en cliente (deuda: sin `?search` server-side). **Verifica** `CompanyDto`/`CompanyListItemDto`
y el request de `POST /companies` en `CompaniesController.cs`.

**Files:**
- Create: `src/app/(private)/market/page.tsx`, `src/app/(private)/market/[id]/page.tsx`
- Create: `src/services/companyService.ts`, `src/hooks/useCompaniesList.ts`, `src/hooks/useCompanyDetail.ts`, `src/hooks/useCreateCompany.ts`
- Create: `src/components/ui/combobox.tsx` (shadcn Command + Popover)
- Create: `src/components/market/company-combobox.tsx`
- Modify: `src/types/companies.ts` (añadir `Company`, `CreateCompanyDto`, `PagedCompanies`)
- Modify: `src/components/navbar-private.tsx` (entrada "Mercado")
- Modify: `src/app/(private)/investments/[id]/page.tsx` (usar `company-combobox` en AddHoldingModal)
- Modify: `src/services/holdingsService.ts` (`listCompanies` con `?pageSize=100`)
- Test: `tests/unit/hooks/useCompaniesList.test.tsx`, `tests/unit/components/company-combobox.test.tsx`

- [x] **Step 1: Tipos** — **desviación real del plan**: `GET /companies/{id}` devuelve la **misma**
  `CompanyListItemDto` que el listado (no un `Company` distinto) — verificado en
  `CompaniesController.cs`. `Company` pasa a ser el DTO de **comando** (`POST /companies` →
  `CompanyDto`: sector/market/currency tipados como enum, sin `lastPrice`/`lastValuationDate`),
  mismo patrón que `Portfolio` (comando) vs `PortfolioListItem` (query) en `types/portfolios.ts`.
  `Sector`/`Market` verificados campo a campo contra los enums reales del backend (11 y 14 valores)
  y centralizados en `types/enums.ts` (mismo patrón `as const` + tipo derivado que `CURRENCIES`),
  no en `types/companies.ts` — es el fichero ya establecido como fuente única de "reflejos de enums
  del backend". Traducción de `Sector` en `lib/investments/labels.ts` (`SECTOR_LABEL`); `Market`
  (códigos de bolsa) no se traduce.

- [x] **Step 2: Servicio** — `companyService.ts` con `listPaged`/`getById`/`create`. Añadido también
  `lib/queryKeys.ts` (factory de query keys, `companyKeys`/`portfolioKeys`) tras feedback del humano:
  cada hook declarando su propia key local (p. ej. `['portfolios']` repetido en tres ficheros
  distintos) es una fuente real de divergencia. Retrofit de `usePortfolios.ts`/`usePortfolioMutations.ts`/
  `useHoldings.ts`/`usePortfoliosSummary.ts`/`useCompanies.ts` a la factory (refactor puro, mismos
  valores de key, sin cambio de comportamiento). **Deuda anotada**: el resto de hooks (`useProfile`,
  `useCategoryChart`/`useMonthlySeries`, `useTransactions`) se migran cuando se toquen, no de una sentada.

- [x] **Step 3: Hooks + tests** — `useCompaniesList`/`useCompanyDetail`/`useCreateCompany` (TDD),
  usando `companyKeys` de la factory.

- [x] **Step 4: Combobox base** — `npx shadcn@latest add command` se quedó esperando confirmación
  interactiva para sobrescribir `button.tsx` (ya personalizado) y no la recibió — se escribió
  `command.tsx` a mano (wrapper de `cmdk`, ya instalado como dependencia por el intento del CLI).
  jsdom no implementa `ResizeObserver` ni `Element.scrollIntoView` (los usa `cmdk` internamente) —
  añadidos polyfills mínimos en `tests/setup.ts`.

- [x] **Step 5: `company-combobox.tsx`** — TDD. El "10 resultados iniciales" se resolvió añadiendo
  `initialResultsLimit` al combobox base: mientras no hay texto escrito se recorta la lista a N antes
  de pintarla; en cuanto hay búsqueda, se pasa la lista completa y el filtro propio de `cmdk` (que
  compara contra `item.label`, no `item.value` — el value real viaja por closure en `onSelect`) hace
  el resto.

- [x] **Step 6: Usar el combobox en AddHoldingModal** — sustituido, con `data-testid="select-company"`
  reexpuesto desde el combobox base (prop `data-testid`) para no romper el E2E existente.

- [x] **Step 7: Página listado `/market`** — implementado tal cual, con el `<Select modal={false}>`
  ya establecido (Task 6) para sector/moneda del formulario de creación.

- [x] **Step 8: Página detalle `/market/[id]`** — confirmado con el humano antes de escribir el
  fichero: es una **página con breadcrumb** (patrón maestro-detalle ya establecido en
  `/investments/[id]`), no un modal — el modal centrado es solo para la acción "Nueva empresa".

- [x] **Step 9: Nav "Mercado"** — añadido.

- [x] **Step 10: Verificar y commit + PR**

```bash
npm run lint && npm run typecheck && npm run test && npm run test:e2e
git add -A && git commit -m "feat(investments): sección Mercado (companies listado/detalle/crear) y combobox de empresa"
```

---

## Task 9: Valuations — listado + crear + gráfico de serie (módulo Investments)

**Contexto:** dentro de `/market/[id]`: serie de cotización con selector de periodo, listado paginado
de valoraciones y alta. **Verifica** en `CompaniesController.cs`:
`GET /companies/{id}/valuations` (paginado, → `ValuationListItemDto`),
`POST /companies/{id}/valuations` (request), `GET /companies/{id}/valuations/series?period=` (→ serie +
summary). Sin `GET{id}` ni `DELETE` de valoración (deuda: costura documentada).

**Files:**
- Modify: `src/types/companies.ts` (o nuevo `src/types/valuations.ts`): `ValuationSeries`,
  `ValuationSeriesPoint`, `CreateValuationDto`, `PagedValuations`, `ValuationPeriod`
- Modify: `src/services/companyService.ts` (`listValuations`, `createValuation`, `valuationSeries`)
- Create: `src/hooks/useValuations.ts` (`useValuations`, `useCreateValuation`, `useValuationSeries`)
- Create: `src/components/market/valuation-series-chart.tsx`
- Modify: `src/app/(private)/market/[id]/page.tsx` (integrar serie + listado + alta)
- Test: `tests/unit/hooks/useValuations.test.tsx`

- [x] **Step 1: Tipos** (verificar contra los DTO reales):

  **Desviaciones reales confirmadas contra `CompaniesController.cs`/DTOs** (verificado con
  subagente antes de escribir tipos, no de memoria):
  - Ruta base real es `/api/v1/companies/...`.
  - La lista (`ValuationListItemDto`, Dapper) trae `priceCurrency: string`; el DTO de creación
    (`ValuationDto`) trae `currency` como enum `Currency` tipado — dos nombres/formas distintas
    para el mismo concepto, documentado en comentario en `types/companies.ts`.
  - El body de `POST` (`AddValuationRequest`) **no lleva `currency`**: la hereda la empresa.
  - El summary de la serie es `{ first, last, min, max, changePct }` (camelCase) — el plan
    olvidaba `first`.
  - `ValuationPeriod` es un enum de .NET cuyo valor subyacente es el nº de meses; el query
    param `?period=` acepta el NOMBRE del miembro (`ThreeMonths`, `OneYear`…), **no** `'3m'/'1y'`
    como asumía el borrador del plan. Tipo final: `'ThreeMonths'|'SixMonths'|'OneYear'|'ThreeYears'|'FiveYears'`,
    con `VALUATION_PERIODS`/`VALUATION_PERIOD_LABEL` para la UI.
  - Serie vacía: `points: []`, summary con todo a `0`, `currency: ""` (nunca `null`/ausente).

  Implementado en `types/enums.ts` (`ValuationPeriod`, siguiendo el patrón de `Sector`/`Market`:
  es un enum del dominio, no un tipo de recurso) y `types/companies.ts` (`Valuation`,
  `CreateValuationDto`, `PagedValuations`, `ValuationSeriesPoint`, `ValuationSeriesSummary`,
  `ValuationSeries` — `ValuationListItem` ya existía de Task 8). Etiqueta de periodo en
  `lib/investments/labels.ts` (`VALUATION_PERIOD_LABEL`), mismo patrón que `SECTOR_LABEL`.

- [x] **Step 2: Servicio** — `listValuations`/`createValuation`/`valuationSeries` añadidos a
  `companyService.ts`, con `period` viajando como el nombre del enum en la query string.

- [x] **Step 3: Hooks + tests** — `useValuations.ts` (TDD: test RED confirmado antes de crear el
  fichero, luego GREEN). `valuationKeys` añadido a `lib/queryKeys.ts` (mismo patrón que
  `portfolioKeys`/`companyKeys`); `useCreateValuation` invalida por prefijo
  (`valuationKeys.all(id)` / `valuationKeys.seriesAll(id)`), cubriendo todas las páginas y
  periodos cacheados sin necesidad de conocerlos. 5 tests nuevos en
  `tests/unit/useValuations.test.tsx` (ruta real del proyecto: plano bajo `tests/unit/`, no
  `tests/unit/hooks/` como sugería el plan).

- [x] **Step 4: Gráfico de serie** — `valuation-series-chart.tsx`: `LineChart` de Recharts con
  `var(--chart-5)` (no existe una variable `--chart-line`; `--chart-5` es la que usan
  `portfolio-chart.tsx` y el balance acumulado del Dashboard para líneas/áreas — "azul celeste
  apagado" del design system). Selector de periodo como segmented-control (`role="tablist"`,
  mismo patrón que el conmutador Gastos/Ingresos de `expenses/page.tsx`). Fila de summary con
  4 celdas (Mínimo/Máximo/Último/Variación) usando `colorPnL`/`signPnL`/`formatPct` de
  `lib/transactions/labels.ts`. Estados loading/error/empty (serie sin puntos).

- [x] **Step 5: Integrar en `/market/[id]`** — añadido `ValuationSeriesChart`, botón "Nueva
  valoración" con modal (`price`/`date`/`source` opcional — sin selector de moneda, la hereda la
  empresa) y listado paginado de valoraciones (tabla en desktop, tarjetas en móvil, mismo patrón
  que `expenses/page.tsx`) con `<Pagination>` usando `DEFAULT_PAGE_SIZE`. Comentario explícito en
  el código sobre la costura "sin ver/borrar valoración individual".

- [x] **Step 6: Verificar y commit + PR**

  **Desviación**: `npm run lint` no es ejecutable en este entorno — `eslint` no está declarado
  como dependencia en `package.json` (deuda preexistente del proyecto, no introducida por esta
  tarea; no se tocó `package.json`). `typecheck` limpio, 266/266 tests unitarios en verde
  (+5 nuevos de `useValuations`), 11/11 E2E existentes en verde. Verificación visual manual en
  navegador (dev server + backend reales): creada una empresa, confirmado el estado vacío del
  gráfico/listado, dada de alta una valoración y confirmado que el summary (mín/máx/último/
  variación), la fila de la tabla y el selector de periodo se actualizan correctamente
  (capturas de pantalla revisadas, script de smoke ad hoc descartado tras la verificación).

```bash
git add -A && git commit -m "feat(investments): valoraciones de empresa (listado, alta y gráfico de serie por periodo)"
```

---

## Registro de deuda (para la próxima ronda de backend)

Estas costuras quedan documentadas en el código (sin botones muertos) y consolidadas en el §6 de la
spec 011:

1. **Companies sin `PUT/DELETE`** — Mercado no edita ni borra empresas (Task 8, Step 8).
2. **Valuations sin `GET{id}` ni `DELETE`** — sin ver-detalle-individual ni borrado (Task 9, Step 5).
3. **Búsqueda server-side de empresas (`?search=`)** — el combobox filtra en cliente sobre
   `pageSize=100` (Task 8, Steps 2/5); incompleto si el catálogo supera 100.

---

## Self-review (hecho al escribir el plan)

- **Cobertura de la spec 011**: T1↔Task1, T2↔Task2, T3a↔Task3, T3b↔Task4, T4(paginación)↔Task5,
  T5↔Task6, T6↔Task7, T7↔Task8, T8↔Task9. Nav Mercado (Task 8, Step 9); Contactos/Emails en
  ProfileDropdown ya existen (Task 1 solo cambia datos). Todas las decisiones del §4 de la spec tienen
  tarea.
- **Gráfica A sin cambio de comportamiento** (decisión del humano): Task 2 **conserva**
  `category-bars.tsx`, la pestaña y `aggregateByCategory.ts`; solo reescribe el cuerpo de
  `useCategoryChart` para tirar del backend (4 llamadas by-category), lo que además arregla el bug del
  límite de 100 filas. Gráfica B es lo único nuevo.
- **Consistencia de tipos**: `PageMeta` se centraliza en `types/pagination.ts` (Task 5) y lo
  referencian `portfolios`/`companies`/`valuations`. `Currency` string (query Dapper) vs enum
  (command): cada tarea que lo toca anota "verificar query vs command DTO".
- **Sin placeholders de implementación**: cada tarea trae código real de tipos/servicios/hooks y
  referencia el fichero-patrón exacto para el wiring de páginas grandes (no se reproducen las páginas
  existentes enteras; se dan los snippets nuevos y el punto de inserción).
- **Verificación previa obligatoria**: cada tarea empieza verificando los DTO reales del backend
  (Contact/EmailLog, UserProfile, PortfoliosSummary, Company detail, ValuationSeries summary), porque
  el plan no fija de memoria la forma exacta de DTO aún no leídos.
