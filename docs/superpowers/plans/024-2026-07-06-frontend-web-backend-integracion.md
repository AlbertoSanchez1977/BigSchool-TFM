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

- [ ] **Step 1: Tipos + constante de categorías por tipo**

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

- [ ] **Step 2: Métodos de servicio** — `src/services/transactionService.ts` (añadir métodos e
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

- [ ] **Step 3: Reescribir el test de `useCategoryChart`** — ahora debe afirmar 4 llamadas a
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

- [ ] **Step 4: Verificar fallo** — `npm run test -- tests/unit/hooks/useCategoryChart.test.tsx` → FAIL.

- [ ] **Step 5: Reescribir el cuerpo de `useCategoryChart.ts`** (misma firma y mismo output; la página
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

- [ ] **Step 6: Verificar que pasa** — repetir Step 4 → PASS. La Gráfica A ya funciona contra el
  backend sin tocar `category-bars.tsx` ni la página.

- [ ] **Step 7: Test + implementación `useMonthlySeries.ts`** — para el tipo dado, una query por cada
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

- [ ] **Step 8: Componente Gráfica B** — `src/components/charts/monthly-stacked-bars.tsx`: `BarChart`
  apilado (Recharts). Transformar `CategoryMonthlySeries[]` a filas por mes `{ label: "MMM YY",
  [category]: value }` (una fila por cada (año,mes) de la ventana). Una `<Bar stackId="a">` por
  categoría, color con la paleta `--chart-*` (ciclar si hay >4, usar el campo `income` o `expense`
  del punto según el `type`). Estado vacío (`emptyLabel`). Reusar el wrapper responsive y el tooltip
  HTML de `components/charts/category-bars.tsx` como referencia de estilo.

- [ ] **Step 9: Pestaña Gráficas en `expenses/page.tsx`** — añadir un **selector de año de
  referencia** (máx = año actual) que alimenta ambas gráficas. Mantener el conmutador Gastos/Ingresos
  y la **Gráfica A actual** (`useCategoryChart` + `<CategoryBars>`) tal cual, pasándole el año
  seleccionado. Debajo, añadir la **Gráfica B** (`useMonthlySeries` + `<MonthlyStackedBars>`), ambas
  con `enabled: tab === 'charts'`. No se elimina ningún componente existente.

- [ ] **Step 10: Verificar y commitear**

```bash
npm run lint && npm run typecheck && npm run test
git add -A
git commit -m "feat(finance): migrar gráfica por categoría al backend (fix límite 100) y añadir serie mensual apilada"
```

- [ ] **Step 11: PR** a `develop`.

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

- [ ] **Step 1: Tipo** — en `src/types/auth.ts`, añadir a `RegisterDto`: `baseCurrency: Currency`
  (importar `Currency` de `@/types/enums`). Verificar el nombre exacto del campo contra el request.

- [ ] **Step 2: Test que falla** — el schema exige `baseCurrency`:

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

- [ ] **Step 3: Verificar fallo** — `npm run test -- tests/unit/schemas/auth.test.ts` → FAIL.

- [ ] **Step 4: Schema** — en `registerSchema` añadir dentro del `.object({...})`:
  `baseCurrency: z.enum(['EUR', 'USD', 'GBP', 'CHF', 'JPY'], { message: 'Selecciona una moneda' })`.

- [ ] **Step 5: Verificar que pasa** — repetir Step 3 → PASS.

- [ ] **Step 6: UI en `register-form.tsx`** — añadir un campo `<Select>` (shadcn) de moneda entre
  email y password. Como `Select` no es un `<input>` nativo, usar `Controller` de react-hook-form (o
  `setValue`), igual que el `Select` de empresa en `investments/[id]/page.tsx`. Pasar el valor en
  `onSubmit`: `registerUser({ email, password, fullName, baseCurrency: data.baseCurrency })`. Opciones:
  las 5 de `Currency`.

- [ ] **Step 7: Verificar E2E** — si existe flujo Playwright de registro, seleccionar moneda y
  ajustar el test. `npm run test:e2e`.

- [ ] **Step 8: Commit + PR**

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

- [ ] **Step 1: Tipos** — `src/types/users.ts`:

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

- [ ] **Step 2: Servicio** — `src/services/userService.ts`:

```typescript
import { api } from '@/lib/api'
import type { UserProfile, UpdateUserDto } from '@/types/users'
export const userService = {
  me:     () => api.get<UserProfile>('/users/me'),
  update: (data: UpdateUserDto) => api.put<UserProfile>('/users/me', data),
}
```

- [ ] **Step 3: Test que falla + hook** — `useProfile.test.tsx` (mock `@/lib/api`), luego
  `src/hooks/useProfile.ts`:

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

- [ ] **Step 4: Página** — en `profile/page.tsx`: sustituir los `InfoRow` mock por `useProfile()`
  (mostrar `email`, `baseCurrency`, `lastLoginDate` formateada; estados loading/error). En `onSubmit`
  llamar `useUpdateProfile().mutate({ fullName, password: values.password || null }, { onSuccess,
  onError: (e) => mostrar ApiError.message })`. Quitar los comentarios `TODO (deuda técnica)`.

- [ ] **Step 5: Verificar y commit + PR**

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

- [ ] **Step 1: Tipo compartido** — `src/types/pagination.ts`:

```typescript
export interface PageMeta { page: number; pageSize: number; totalCount: number; totalPages: number }
```
Reexportar desde `transactions.ts` para no romper imports existentes:
`export type { PageMeta } from './pagination'` (y eliminar la definición duplicada de `PageMeta` allí).

- [ ] **Step 2: Test que falla** — `pagination.test.tsx`: renderiza rango "1–20 de 45", Anterior
  deshabilitado en page 1, click en Siguiente llama `onPageChange(2)`.

- [ ] **Step 3: Componente** — `src/components/ui/pagination.tsx`:

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

- [ ] **Step 4: Verificar que pasa** el test.

- [ ] **Step 5: Servicio + tipo paginado** — en `portfolios.ts` añadir
  `import type { PageMeta } from './pagination'` y
  `export interface PagedPortfolios { items: PortfolioListItem[]; meta: PageMeta }`. En
  `portfolioService.list({page,pageSize})` usar `api.getWithMeta<PortfolioListItem[]>('/portfolios?...')`
  y mapear `meta` igual que `transactionService.list`.

- [ ] **Step 6: Hook** — `usePortfolios({ page, pageSize })` con `queryKey: ['portfolios', page, pageSize]`.
  `useCreatePortfolio` invalida `['portfolios']` (prefijo).

- [ ] **Step 7: Páginas** — en `investments/page.tsx`: `const [page, setPage] = useState(1)`;
  consumir `data.items`; añadir `<Pagination ... onPageChange={setPage} />`. En `expenses/page.tsx`:
  reemplazar el bloque de paginación inline por `<Pagination page={page} pageSize={pageSize}
  totalCount={data.meta.totalCount} totalPages={data.meta.totalPages} onPageChange={setPage} />`.

- [ ] **Step 8: Verificar y commit + PR**

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

- [ ] **Step 1: Tipo + servicio**

```typescript
// types/portfolios.ts
export interface RenamePortfolioDto { name: string }
```
```typescript
// portfolioService.ts (añadir)
rename: (id: number, data: RenamePortfolioDto) => api.put<Portfolio>(`/portfolios/${id}`, data),
remove: (id: number) => api.delete<void>(`/portfolios/${id}`),
```

- [ ] **Step 2: Test que falla + hooks** — verificar antes la `queryKey` real del detalle en
  `usePortfolioDetail` (en `useHoldings.ts`) y usarla en la invalidación. `usePortfolioMutations.ts`:

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

- [ ] **Step 3: UI en la cabecera de `investments/[id]/page.tsx`** — junto al breadcrumb añadir un
  `DropdownMenu` (⋯) con "Renombrar" y "Eliminar":
  - **Renombrar**: modal centrado (patrón `CreatePortfolioModal` de `investments/page.tsx`) con el
    nombre precargado → `useRenamePortfolio(portfolioId).mutate({ name })`, toast, cerrar.
  - **Eliminar**: modal de confirmación → `useDeletePortfolio().mutate(portfolioId, { onSuccess:
    () => router.push('/investments'), onError: (e) => toast.error(e.message) })`. El 409 del guard
    fiscal llega como `ApiError.message`; mostrarlo tal cual.

- [ ] **Step 4: Verificar y commit + PR**

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

- [ ] **Step 1: Tipos** — en `HoldingPerformance` quitar el bloque `TODO` y, **si el backend garantiza
  los campos**, dejarlos como presentes (no opcionales); añadir `marketValueOriginal: number`. Añadir
  el tipo del summary global (verificar campos reales contra el DTO):

```typescript
export interface PortfoliosSummary {
  baseCurrency: string
  marketValue: number
  costBasis: number
  unrealizedPnL: number
  realizedPnL: number
  totalPnL: number
  returnPct: number
}
```

- [ ] **Step 2: Servicio + hook**

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

- [ ] **Step 3: `PerformanceTab`** — añadir la celda "Valor mercado original"
  (`h.marketValueOriginal`, formateado en `origCur`) al grid de cada holding. Ajustar los
  `!`/fallbacks si los campos pasan a no-opcionales.

- [ ] **Step 4: Dashboard** — verificar en `dashboard/page.tsx` qué usa hoy el mini-resumen de
  inversiones (probablemente `/portfolios/{id}/performance`) y sustituirlo por `usePortfoliosSummary()`.
  Mostrar valor mercado / realizado / no realizado / % con estados loading/error.

- [ ] **Step 5: Verificar y commit + PR**

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

- [ ] **Step 1: Tipos** — en `companies.ts` añadir (verificar contra `CompanyDto`):

```typescript
import type { PageMeta } from './pagination'
export interface Company {           // GET /companies/{id}
  idCompany: number
  name: string
  ticker: string
  sector: string | null
  market: string | null
  currency: string
  lastPrice: number | null
  lastValuationDate: string | null
}
export interface CreateCompanyDto {  // POST /companies (verificar campos requeridos)
  name: string
  ticker: string
  sector?: string | null
  market?: string | null
  currency: string
}
export interface PagedCompanies { items: CompanyListItem[]; meta: PageMeta }
```

- [ ] **Step 2: Servicio** — `companyService.ts`: `listPaged({page,pageSize})` con `getWithMeta`,
  `getById(id)`, `create(dto)`. Además, en `holdingsService.listCompanies` cambiar a
  `api.get<CompanyListItem[]>('/companies?pageSize=100')` (comentar: filtro server-side = deuda).

- [ ] **Step 3: Hooks + tests** — `useCompaniesList({page,pageSize})` (paginado), `useCompanyDetail(id)`,
  `useCreateCompany()` (invalida `['companies']`). Tests con mock de api.

- [ ] **Step 4: Combobox base** — `src/components/ui/combobox.tsx` usando shadcn `Command` +
  `Popover`. Si el bloque `command` no existe, instalarlo: `npx shadcn@latest add command`. API:
  `items`, `value`, `onChange`, `placeholder`, render de cada item.

- [ ] **Step 5: `company-combobox.tsx`** — envuelve el combobox base: fuente = `useCompanies()`
  (`?pageSize=100`), muestra 10 resultados iniciales y filtra en cliente por `ticker`/`name`. Emite
  `idCompany`. Test: escribir filtra la lista.

- [ ] **Step 6: Usar el combobox en AddHoldingModal** — en `investments/[id]/page.tsx` reemplazar el
  `<Select>` de empresa por `<CompanyCombobox value={idCompany} onChange={...} />` (mantener el
  `Controller` de react-hook-form).

- [ ] **Step 7: Página listado `/market`** — cabecera con botón "Nueva empresa" (modal →
  `useCreateCompany`), tabla/cards de empresas (ticker, nombre, sector, último precio), `<Pagination>`
  de la Task 5, estados loading/error/empty. Click en fila → `router.push('/market/{id}')`.

- [ ] **Step 8: Página detalle `/market/[id]`** — breadcrumb `← Mercado`, cabecera con
  ticker/nombre/moneda/último precio (`useCompanyDetail`). Dejar un contenedor para serie/valoraciones
  (Task 9). **Sin** botones editar/borrar (deuda: sin PUT/DELETE) — comentar la costura.

- [ ] **Step 9: Nav "Mercado"** — en `navbar-private.tsx` añadir `{ href: '/market', label: 'Mercado',
  icon: Building2 }` (lucide) al array `navItems` (desktop + móvil ya iteran ese array).

- [ ] **Step 10: Verificar y commit + PR**

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

- [ ] **Step 1: Tipos** (verificar contra los DTO reales):

```typescript
import type { PageMeta } from './pagination'
export interface ValuationSeriesPoint { date: string; price: number }
export interface ValuationSeries {
  currency: string
  points: ValuationSeriesPoint[]
  min: number; max: number; last: number; changePct: number   // confirmar nombres del summary DTO
}
export interface CreateValuationDto { price: number; date: string; source?: string | null }
export interface PagedValuations { items: ValuationListItem[]; meta: PageMeta }
export type ValuationPeriod = '3m' | '6m' | '1y' | '3y' | '5y'
```

- [ ] **Step 2: Servicio** — en `companyService.ts` añadir:

```typescript
listValuations: (id: number, page: number, pageSize: number) =>
  api.getWithMeta<ValuationListItem[]>(`/companies/${id}/valuations?page=${page}&pageSize=${pageSize}`),
createValuation: (id: number, data: CreateValuationDto) =>
  api.post<ValuationListItem>(`/companies/${id}/valuations`, data),
valuationSeries: (id: number, period: ValuationPeriod) =>
  api.get<ValuationSeries>(`/companies/${id}/valuations/series?period=${period}`),
```

- [ ] **Step 3: Hooks + tests** — `useValuations.ts`:
  - `useValuations(id, page, pageSize)` → useQuery paginado (mapear meta).
  - `useCreateValuation(id)` → mutation, invalida `['valuations', id]` y `['valuation-series', id]`.
  - `useValuationSeries(id, period)` → useQuery con `queryKey: ['valuation-series', id, period]`.
  Test: comprobar rutas y que `createValuation` invalida ambas keys.

- [ ] **Step 4: Gráfico de serie** — `valuation-series-chart.tsx`: `LineChart` de Recharts con
  `--chart-line`; selector de periodo (3m/6m/1y/3y/5y) que cambia `useValuationSeries`; fila de summary
  (min/max/último/variación % con `text-positive/negative` según signo). Estados loading/error/empty.
  Reusar el wrapper responsive de `components/charts/portfolio-chart.tsx` como referencia.

- [ ] **Step 5: Integrar en `/market/[id]`** — añadir: (a) el `valuation-series-chart`; (b) botón
  "Nueva valoración" → modal (`price`, `date`, `source?`) con `useCreateValuation`; (c) listado
  paginado de valoraciones (`useValuations` + `<Pagination>`), estados. **Sin** acciones ver/borrar
  por valoración (deuda) — comentar la costura.

- [ ] **Step 6: Verificar y commit + PR**

```bash
npm run lint && npm run typecheck && npm run test
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
