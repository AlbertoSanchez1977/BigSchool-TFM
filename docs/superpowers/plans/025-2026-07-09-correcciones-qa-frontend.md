# Correcciones QA post-pruebas manuales — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Corregir los 6 hallazgos de la ronda de pruebas manuales (`docs/guia-pruebas-manuales-frontend.md`), agrupados por tema técnico en 5 tareas independientes.

**Architecture:** Cada tarea = una rama = un PR = revisión humana = merge (flujo `feature-branch-workflow` habitual del proyecto). Backend .NET 8 (Clean Architecture, FluentValidation, DDD) + Frontend Next.js 15 (TypeScript, Zod, TanStack Query, Vitest/RTL). Se centralizan utilidades de fecha en un único `src/lib/dates.ts` que van construyendo las Tareas 2–4.

**Tech Stack:** .NET 8, FluentValidation, xUnit + FluentAssertions (backend) · Next.js App Router, TypeScript, Zod, Vitest + React Testing Library (frontend).

---

## Decisiones cerradas con el usuario (no reabrir al ejecutar)

- **Bucket A (no fechas futuras):** se corrige **backend + frontend** en **compra de holding, venta de holding y valoración**. **Transacciones se EXCLUYEN a propósito** (un pago emitido a fecha futura es un caso válido; asumir Saldo Real vs. corriente queda fuera de alcance).
- **Bucket B (fechas-hora UTC):** solución simple = **mostrar la hora UTC tal cual y etiquetarla "UTC"** (sin conversión de zona). Verificado que el backend las emite con `DateTime.UtcNow` (`User.LastLoginDate`, `EmailLog.SentAt`, `Contact.CreatedAt`).
- **Bucket C (fecha por defecto de "Nueva transacción"):** si la parrilla está en el mes actual → **hoy**; si está en otro mes → **día 1 del mes visible**.
- **Bucket D (higiene):** **crear favicon propio** (SVG file-based de Next) y **retirar Vercel Analytics** por completo.
- **Bucket E (Alcance):** se **reescribe el contenido** de `/scope` para reflejar la iteración 2 (contacto/emails/perfil ya son backend real, no localStorage; existen Mercado y Valoraciones). El usuario revisa el contenido propuesto en este plan / en el PR.

---

## Estructura de ficheros (qué toca cada tarea)

| Fichero | Tarea | Responsabilidad |
|---|---|---|
| `src/frontend-web/src/app/icon.svg` (crear) | 1 | Favicon file-based de Next (marca BigSchool) |
| `src/frontend-web/src/app/layout.tsx` (mod) | 1 | Quitar `<Analytics/>` + bloque `icons` manual roto |
| `src/frontend-web/package.json` (mod) | 1 | Quitar dependencia `@vercel/analytics` |
| `src/frontend-web/src/lib/dates.ts` (crear en T2, ampliar en T3/T4) | 2,3,4 | Utilidades de fecha centralizadas |
| `src/frontend-web/tests/unit/dates.test.ts` (crear en T2, ampliar en T3/T4) | 2,3,4 | Tests de las utilidades de fecha |
| `src/frontend-web/src/components/transactions/transaction-sheet.tsx` (mod) | 2 | Aceptar `defaultDate` para modo creación |
| `src/frontend-web/src/app/(private)/expenses/page.tsx` (mod) | 2 | Pasar `defaultDate` según periodo visible |
| `src/backend/.../Commands/AddHolding/AddHoldingCommandValidator.cs` (mod) | 3 | Regla "no futura" en `BuyDate` |
| `src/backend/.../Commands/SellShares/SellSharesCommandValidator.cs` (mod) | 3 | Regla "no futura" en `SellDate` |
| `src/backend/.../Commands/AddValuation/AddValuationCommandValidator.cs` (mod) | 3 | Regla "no futura" en `Date` |
| `src/backend/tests/.../Validators/Investments/*ValidatorTests.cs` (crear ×3) | 3 | Tests unitarios de los 3 validadores |
| `src/backend/tests/.../Integration.Tests/Investments/{PostHolding,PostSale,AddValuation}Tests.cs` (mod) | 3 | Caso E2E 400 fecha futura |
| `src/frontend-web/src/app/(private)/investments/[id]/page.tsx` (mod) | 3 | `max`/refine en compra y venta |
| `src/frontend-web/src/app/(private)/market/[id]/page.tsx` (mod) | 3 | `max`/refine en valoración |
| `src/frontend-web/src/app/(private)/profile/page.tsx` (mod) | 4 | Etiqueta UTC en "Último acceso" |
| `src/frontend-web/src/app/(private)/emails/page.tsx` (mod) | 4 | Etiqueta UTC en `sentAt` |
| `src/frontend-web/src/app/(private)/contacts/page.tsx` (mod) | 4 | Etiqueta UTC en `createdAt` |
| `src/frontend-web/src/app/(public)/scope/page.tsx` (mod) | 5 | Contenido actualizado a iteración 2 |

> Todos los comandos de shell asumen `cwd = src/frontend-web` para el frontend y `cwd = src/backend`
> para el backend, salvo que se indique otra cosa. El proyecto usa **pnpm** como gestor de paquetes
> (existe `pnpm-lock.yaml`); los scripts se ejecutan con `npm run <script>`.

---

## Task 1: Higiene — favicon propio + retirar Vercel Analytics (Bucket D · #2)

Cierra los 404 de consola (`icon.svg`, `icon-light-32x32.png`, `/_vercel/insights/script.js`). No hay
tests unitarios: es assets + config; la verificación es build limpio + consola sin 404.

**Files:**
- Create: `src/frontend-web/src/app/icon.svg`
- Modify: `src/frontend-web/src/app/layout.tsx`
- Modify: `src/frontend-web/package.json`

- [x] **Step 1: Crear el favicon file-based** `src/frontend-web/src/app/icon.svg`

Next.js App Router detecta automáticamente `app/icon.svg` y genera el `<link rel="icon">` — no hace
falta declararlo en `metadata`. Marca BigSchool (línea de cotización blanca sobre azul primario):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">
  <rect width="32" height="32" rx="7" fill="#2f80d6"/>
  <path d="M8 8 v13 a3 3 0 0 0 3 3 h13" fill="none" stroke="#ffffff" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M11 19 l4-4 3 3 5-6" fill="none" stroke="#ffffff" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```

- [x] **Step 2: Retirar Analytics y el bloque `icons` roto de** `src/frontend-web/src/app/layout.tsx`

Quita la línea 1 `import { Analytics } from '@vercel/analytics/next'`, el bloque `icons: {...}` de
`metadata` (líneas 18–34; las referencias a `/icon-light-32x32.png`, `/icon-dark-32x32.png`,
`/icon.svg`, `/apple-icon.png` sobran: el `app/icon.svg` del Step 1 las sustituye) y la línea
`{process.env.NODE_ENV === 'production' && <Analytics />}` del `<body>`. El fichero queda así:

```tsx
import type { Metadata, Viewport } from 'next'
import { Geist, Geist_Mono } from 'next/font/google'
import './globals.css'
import { Providers } from './providers'

const geistSans = Geist({ variable: '--font-geist-sans', subsets: ['latin'] })
const geistMono = Geist_Mono({
  variable: '--font-geist-mono',
  subsets: ['latin'],
})

export const metadata: Metadata = {
  title: 'BigSchool · Finanzas personales e inversiones con IA',
  description:
    'Controla tus gastos e ingresos, gestiona tus carteras de inversión con ventas FIFO y rentabilidad multimoneda, y apóyate en un asistente de IA. Value investing al alcance de todos.',
}

export const viewport: Viewport = {
  colorScheme: 'light dark',
  themeColor: [
    { media: '(prefers-color-scheme: light)', color: 'white' },
    { media: '(prefers-color-scheme: dark)', color: 'black' },
  ],
}

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    <html
      lang="es"
      className={`${geistSans.variable} ${geistMono.variable} bg-background`}
    >
      <body className="font-sans antialiased">
        <Providers>{children}</Providers>
      </body>
    </html>
  )
}
```

- [x] **Step 3: Retirar la dependencia** `@vercel/analytics`

Run: `pnpm remove @vercel/analytics`
Expected: elimina la línea de `package.json` y actualiza `pnpm-lock.yaml`.

- [x] **Step 4: Verificar typecheck y build**

Run: `npm run typecheck`
Expected: sin errores.

Run: `npm run build`
Expected: build correcto; en el output NO aparece `@vercel/analytics`.

- [x] **Step 5: Verificación visual manual**

Arranca `npm run dev`, abre `http://localhost:3000` con la consola (F12). Confirma: (a) el favicon de
la pestaña es el nuevo icono; (b) NO hay 404 de `icon.svg`, `icon-light-32x32.png` ni
`/_vercel/insights/script.js`.

**Confirmado por el humano** (comprobación visual manual): favicon nuevo visible en la pestaña, sin
404 de los assets rotos ni del script de Vercel.

- [x] **Step 6: Commit**

```bash
git add src/frontend-web/src/app/icon.svg src/frontend-web/src/app/layout.tsx src/frontend-web/package.json src/frontend-web/pnpm-lock.yaml
git commit -m "fix(frontend): favicon propio y retirada de Vercel Analytics (cierra 404 de consola)"
```

---

## Task 2: Fecha por defecto de "Nueva transacción" = periodo visible (Bucket C · #4)

Al crear una transacción con la parrilla filtrada a un mes distinto del actual, la fecha por defecto
(hoy) caía fuera del filtro y la transacción "desaparecía". Se calcula la fecha por defecto según el
mes/año visible.

**Files:**
- Create: `src/frontend-web/src/lib/dates.ts`
- Create: `src/frontend-web/tests/unit/dates.test.ts`
- Modify: `src/frontend-web/src/components/transactions/transaction-sheet.tsx`
- Modify: `src/frontend-web/src/app/(private)/expenses/page.tsx`

- [x] **Step 1: Escribir el test que falla** `src/frontend-web/tests/unit/dates.test.ts`

```ts
import { describe, it, expect } from 'vitest'
import { defaultTransactionDate } from '@/lib/dates'

describe('defaultTransactionDate', () => {
  // "hoy" fijo para determinismo: 2026-07-09
  const today = new Date(2026, 6, 9) // meses 0-indexados: 6 = julio

  it('devuelve la fecha de hoy si el periodo visible es el mes/año actual', () => {
    expect(defaultTransactionDate(2026, 7, today)).toBe('2026-07-09')
  })

  it('devuelve el día 1 si el periodo visible es un mes anterior', () => {
    expect(defaultTransactionDate(2026, 3, today)).toBe('2026-03-01')
  })

  it('devuelve el día 1 si el periodo visible es un mes de otro año', () => {
    expect(defaultTransactionDate(2025, 12, today)).toBe('2025-12-01')
  })

  it('devuelve el día 1 si el periodo visible es un mes futuro', () => {
    expect(defaultTransactionDate(2026, 9, today)).toBe('2026-09-01')
  })
})
```

- [x] **Step 2: Ejecutar el test y verificar que falla (RED)**

Run: `npm run test -- --run tests/unit/dates.test.ts`
Expected: FAIL — `Failed to resolve import "@/lib/dates"` (el módulo aún no existe).

- [x] **Step 3: Crear** `src/frontend-web/src/lib/dates.ts`

```ts
// Utilidades de fecha centralizadas. Las fechas de negocio del backend viajan como
// 'YYYY-MM-DD' (DateOnly) y las de auditoría como timestamps UTC (DateTime.UtcNow).

// Fecha de hoy en 'YYYY-MM-DD' según la zona LOCAL del navegador (no UTC — evita el
// desfase de un día cerca de medianoche que tiene `toISOString()`).
export function todayISO(today = new Date()): string {
  const y = today.getFullYear()
  const m = String(today.getMonth() + 1).padStart(2, '0')
  const d = String(today.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

// Fecha por defecto para "Nueva transacción" según el mes/año visible en la parrilla:
// - si el periodo visible es el mes/año actual → hoy (comportamiento previo).
// - si es otro mes → día 1 de ese mes, para que la transacción nueva caiga DENTRO del
//   filtro visible y no "desaparezca".
export function defaultTransactionDate(
  viewedYear: number,
  viewedMonth: number,
  today = new Date(),
): string {
  const isCurrentMonth =
    viewedYear === today.getFullYear() && viewedMonth === today.getMonth() + 1
  if (isCurrentMonth) return todayISO(today)
  const m = String(viewedMonth).padStart(2, '0')
  return `${viewedYear}-${m}-01`
}
```

- [x] **Step 4: Ejecutar el test y verificar que pasa (GREEN)**

Run: `npm run test -- --run tests/unit/dates.test.ts`
Expected: PASS (4 tests).

- [x] **Step 5: Aceptar `defaultDate` en el sheet** `src/frontend-web/src/components/transactions/transaction-sheet.tsx`

Añade el prop y úsalo en modo creación. Cambios:

1. En la interfaz de props (tras `transaction?: Transaction`):
```tsx
interface TransactionSheetProps {
  open: boolean
  onClose: () => void
  transaction?: Transaction   // si se pasa → modo edición; si no → modo creación
  defaultDate?: string        // fecha inicial en modo creación (por defecto: hoy)
}
```
2. En la firma del componente: `export function TransactionSheet({ open, onClose, transaction, defaultDate }: TransactionSheetProps) {`
3. Sustituye las **dos** apariciones de `transactionDate: todayISO(),` (en `defaultValues` y en el
   `reset(...)` del `useEffect`) por `transactionDate: defaultDate ?? todayISO(),`. El helper local
   `todayISO()` ya existente en este fichero se mantiene como fallback.

- [x] **Step 6: Pasar `defaultDate` desde la página** `src/frontend-web/src/app/(private)/expenses/page.tsx`

1. Añade el import: `import { defaultTransactionDate } from '@/lib/dates'`
2. Localiza el render del sheet (`<TransactionSheet open={sheetOpen} ... transaction={editingTx} />`)
   y añade el prop, calculado con el `year`/`month` de estado de la página:
```tsx
<TransactionSheet
  open={sheetOpen}
  onClose={() => setSheetOpen(false)}
  transaction={editingTx}
  defaultDate={defaultTransactionDate(year, month)}
/>
```
   (Mantén el resto de props tal cual estén; solo se añade `defaultDate`.)

- [x] **Step 7: Verificar typecheck y suite**

Run: `npm run typecheck`
Expected: sin errores.

Run: `npm run test -- --run`
Expected: toda la suite en verde (incluye los 4 tests nuevos).

- [x] **Step 8: Verificación visual manual**

En `/expenses`, retrocede a un mes anterior con la flecha ‹, pulsa **Nueva transacción** y comprueba
que la fecha por defecto es el **día 1 de ese mes**. Vuelve al mes actual y confirma que el default
vuelve a ser **hoy**.

**Confirmado por el humano** (comprobación visual manual, mes actual y mes anterior).

- [x] **Step 9: Commit**

```bash
git add src/frontend-web/src/lib/dates.ts src/frontend-web/tests/unit/dates.test.ts src/frontend-web/src/components/transactions/transaction-sheet.tsx "src/frontend-web/src/app/(private)/expenses/page.tsx"
git commit -m "fix(finance): fecha por defecto de nueva transacción según el mes visible"
```

---

## Task 3: Prohibir fechas futuras en compra / venta / valoración (Bucket A · #5, #6)

Backend (FluentValidation) + Frontend (Zod refine + `max` en el input). **Transacciones se excluyen a
propósito** (decisión de negocio).

**Files:**
- Modify: `src/backend/src/BigSchool.Application/Investments/Commands/AddHolding/AddHoldingCommandValidator.cs`
- Modify: `src/backend/src/BigSchool.Application/Investments/Commands/SellShares/SellSharesCommandValidator.cs`
- Modify: `src/backend/src/BigSchool.Application/Investments/Commands/AddValuation/AddValuationCommandValidator.cs`
- Create: `src/backend/tests/BigSchool.Application.Tests/Validators/Investments/AddHoldingCommandValidatorTests.cs`
- Create: `src/backend/tests/BigSchool.Application.Tests/Validators/Investments/SellSharesCommandValidatorTests.cs`
- Create: `src/backend/tests/BigSchool.Application.Tests/Validators/Investments/AddValuationCommandValidatorTests.cs`
- Modify: `src/backend/tests/BigSchool.Integration.Tests/Investments/PostHoldingTests.cs`
- Modify: `src/backend/tests/BigSchool.Integration.Tests/Investments/PostSaleTests.cs`
- Modify: `src/backend/tests/BigSchool.Integration.Tests/Investments/AddValuationTests.cs`
- Modify: `src/frontend-web/src/lib/dates.ts` (+ `tests/unit/dates.test.ts`)
- Modify: `src/frontend-web/src/app/(private)/investments/[id]/page.tsx`
- Modify: `src/frontend-web/src/app/(private)/market/[id]/page.tsx`

### 3A · Backend

- [x] **Step 1: Escribir el test unitario del validador de compra (RED)** `.../Validators/Investments/AddHoldingCommandValidatorTests.cs`

**Desviación**: nomenclatura de tests ajustada a la convención real del proyecto
(`Validate_<Escenario>_NoError`/`HasError`, ver `RenamePortfolioCommandValidatorTests.cs`) en vez de
`BuyDate_today_is_valid` del borrador del plan.

```csharp
using System;
using BigSchool.Application.Investments.Commands.AddHolding;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Investments;

public class AddHoldingCommandValidatorTests
{
    private readonly AddHoldingCommandValidator _validator = new();

    private static AddHoldingCommand ValidCommand(DateOnly buyDate) =>
        new(PortfolioId: 1, IdUser: 1, IdCompany: 1, Shares: 10m, BuyPrice: 100m, BuyDate: buyDate, Notes: null);

    [Fact]
    public void BuyDate_today_is_valid()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _validator.TestValidate(ValidCommand(today)).ShouldNotHaveValidationErrorFor(x => x.BuyDate);
    }

    [Fact]
    public void BuyDate_in_the_future_is_invalid()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        _validator.TestValidate(ValidCommand(tomorrow)).ShouldHaveValidationErrorFor(x => x.BuyDate);
    }
}
```

- [x] **Step 2: Ejecutar y verificar que falla (RED)**

Run (cwd `src/backend`): `dotnet test tests/BigSchool.Application.Tests --filter FullyQualifiedName~AddHoldingCommandValidatorTests`
Expected: FAIL en `BuyDate_in_the_future_is_invalid` (aún no existe la regla).

Confirmado: falló `Validate_BuyDateInFuture_HasError` (1 con error, 1 superado).

- [x] **Step 3: Añadir la regla "no futura" en los 3 validadores**

`AddHoldingCommandValidator.cs` — añade tras la regla de `BuyDate`:
```csharp
RuleFor(x => x.BuyDate)
    .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
    .WithMessage("La fecha de compra no puede ser futura.");
```
(añade `using System;` si no está.)

`SellSharesCommandValidator.cs` — añade tras la regla de `SellDate`:
```csharp
RuleFor(x => x.SellDate)
    .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
    .WithMessage("La fecha de venta no puede ser futura.");
```

`AddValuationCommandValidator.cs` — añade tras la regla de `Date`:
```csharp
RuleFor(x => x.Date)
    .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
    .WithMessage("La fecha de la valoración no puede ser futura.");
```

- [x] **Step 4: Escribir los tests de venta y valoración** (misma estructura)

`SellSharesCommandValidatorTests.cs`:
```csharp
using System;
using BigSchool.Application.Investments.Commands.SellShares;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Investments;

public class SellSharesCommandValidatorTests
{
    private readonly SellSharesCommandValidator _validator = new();

    private static SellSharesCommand ValidCommand(DateOnly sellDate) =>
        new(PortfolioId: 1, IdUser: 1, IdCompany: 1, Shares: 5m, SellPrice: 120m, SellDate: sellDate, Notes: null);

    [Fact]
    public void SellDate_today_is_valid()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _validator.TestValidate(ValidCommand(today)).ShouldNotHaveValidationErrorFor(x => x.SellDate);
    }

    [Fact]
    public void SellDate_in_the_future_is_invalid()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        _validator.TestValidate(ValidCommand(tomorrow)).ShouldHaveValidationErrorFor(x => x.SellDate);
    }
}
```

`AddValuationCommandValidatorTests.cs`:
```csharp
using System;
using BigSchool.Application.Investments.Commands.AddValuation;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Investments;

public class AddValuationCommandValidatorTests
{
    private readonly AddValuationCommandValidator _validator = new();

    private static AddValuationCommand ValidCommand(DateOnly date) =>
        new(IdCompany: 1, Price: 190.5m, Date: date, Source: null);

    [Fact]
    public void Date_today_is_valid()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _validator.TestValidate(ValidCommand(today)).ShouldNotHaveValidationErrorFor(x => x.Date);
    }

    [Fact]
    public void Date_in_the_future_is_invalid()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        _validator.TestValidate(ValidCommand(tomorrow)).ShouldHaveValidationErrorFor(x => x.Date);
    }
}
```

- [x] **Step 5: Ejecutar los 3 tests de validadores y verificar que pasan (GREEN)**

Run (cwd `src/backend`): `dotnet test tests/BigSchool.Application.Tests --filter FullyQualifiedName~Validators.Investments`
Expected: PASS (6 tests).

Confirmado: 13/13 (6 nuevos + 7 preexistentes en `Validators.Investments`). Suite completa de
`Application.Tests`: 119/119.

- [x] **Step 6: Añadir el caso E2E 400 "fecha futura" en los 3 endpoints**

En cada fichero E2E existente, añade un test que envíe el request real con una fecha futura y espere
`400 BadRequest` con `VALIDATION_ERROR`. Usa el patrón y helpers ya presentes en cada fichero
(fábrica de request válido + cliente autenticado). Esquema del test a añadir (adáptalo a los helpers
locales de cada base — `PortfolioEndpointTestBase` / `CompanyEndpointTestBase`):

`PostHoldingTests.cs` — nuevo test:
```csharp
[Fact]
public async Task Post_holding_with_future_buy_date_returns_400()
{
    // Arrange: usuario + cartera + empresa igual que en el test principal de este fichero,
    // pero BuyDate = mañana (UTC).
    var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
    // ... construir el request válido reutilizando el helper del fichero y sustituir BuyDate = futureDate ...
    // Act: POST /api/v1/portfolios/{portfolioId}/holdings
    // Assert: StatusCode == 400 y el envelope trae un error con Code == "VALIDATION_ERROR".
}
```
Replica el mismo test en `PostSaleTests.cs` (con `SellDate` futura sobre un holding abierto) y en
`AddValuationTests.cs` (con `Date` futura). Reutiliza en cada uno el arrange del test principal del
propio fichero; el único cambio es la fecha y la aserción de `400`.

- [x] **Step 7: Ejecutar la suite de integración de Investments (GREEN)**

Run (cwd `src/backend`): `dotnet test tests/BigSchool.Integration.Tests --filter FullyQualifiedName~Investments`
Expected: PASS (incluidos los 3 nuevos casos 400). Requiere `bigschool-mysql` en marcha.

Confirmado: 73/73 en verde.

- [x] **Step 8: Commit backend**

```bash
git add src/backend/src/BigSchool.Application/Investments/Commands/AddHolding/AddHoldingCommandValidator.cs src/backend/src/BigSchool.Application/Investments/Commands/SellShares/SellSharesCommandValidator.cs src/backend/src/BigSchool.Application/Investments/Commands/AddValuation/AddValuationCommandValidator.cs src/backend/tests/BigSchool.Application.Tests/Validators/Investments/ src/backend/tests/BigSchool.Integration.Tests/Investments/PostHoldingTests.cs src/backend/tests/BigSchool.Integration.Tests/Investments/PostSaleTests.cs src/backend/tests/BigSchool.Integration.Tests/Investments/AddValuationTests.cs
git commit -m "feat(investments): rechazar fechas futuras en compra, venta y valoración (validación backend)"
```

### 3B · Frontend

- [x] **Step 9: Ampliar el test de `dates.ts` con `isNotFuture` (RED)** — añade a `tests/unit/dates.test.ts`

```ts
import { isNotFuture } from '@/lib/dates'

describe('isNotFuture', () => {
  const today = new Date(2026, 6, 9)

  it('acepta hoy', () => {
    expect(isNotFuture('2026-07-09', today)).toBe(true)
  })

  it('acepta una fecha pasada', () => {
    expect(isNotFuture('2026-07-08', today)).toBe(true)
  })

  it('rechaza una fecha futura', () => {
    expect(isNotFuture('2026-07-10', today)).toBe(false)
  })
})
```

- [x] **Step 10: Ejecutar y verificar que falla (RED)**

Run: `npm run test -- --run tests/unit/dates.test.ts`
Expected: FAIL — `isNotFuture` no está exportado.

Confirmado: 3 fallos (`isNotFuture is not a function`) + 4 tests preexistentes en verde.

- [x] **Step 11: Añadir `isNotFuture` a** `src/frontend-web/src/lib/dates.ts`

```ts
// True si la fecha 'YYYY-MM-DD' NO es futura (hoy o anterior). Comparación lexicográfica,
// válida para el formato ISO fecha. `today` inyectable para tests deterministas.
export function isNotFuture(iso: string, today = new Date()): boolean {
  return iso <= todayISO(today)
}
```

- [x] **Step 12: Ejecutar y verificar que pasa (GREEN)**

Run: `npm run test -- --run tests/unit/dates.test.ts`
Expected: PASS (7 tests: 4 de `defaultTransactionDate` + 3 de `isNotFuture`).

- [x] **Step 13: Aplicar refine + `max` en compra y venta** `src/frontend-web/src/app/(private)/investments/[id]/page.tsx`

1. Añade el import: `import { todayISO, isNotFuture } from '@/lib/dates'` y **elimina** la función
   local `todayISO()` de este fichero (la sustituye la de `@/lib/dates`).
2. En `addHoldingSchema`, cambia la línea de `buyDate`:
```ts
buyDate: z.string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, 'Formato de fecha inválido')
  .refine(isNotFuture, 'La fecha de compra no puede ser futura'),
```
3. En `sellSchemaBase`, cambia la línea de `sellDate`:
```ts
sellDate: z.string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, 'Formato de fecha inválido')
  .refine(isNotFuture, 'La fecha de venta no puede ser futura'),
```
4. Añade `max={todayISO()}` al `<Input>` de fecha de compra (`id="buyDate"`) y al de venta
   (`id="sell-date"`), junto a `type="date"`.

- [x] **Step 14: Aplicar refine + `max` en valoración** `src/frontend-web/src/app/(private)/market/[id]/page.tsx`

1. Añade el import: `import { todayISO, isNotFuture } from '@/lib/dates'` y **elimina** la función
   local `todayISO()` de este fichero.
2. En `createValuationSchema`, cambia la línea de `date`:
```ts
date: z.string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, 'Formato de fecha inválido')
  .refine(isNotFuture, 'La fecha no puede ser futura'),
```
3. Añade `max={todayISO()}` al `<Input>` de fecha (`id="valuation-date"`), junto a `type="date"`.

- [x] **Step 15: Verificar typecheck y suite frontend**

Run: `npm run typecheck`
Expected: sin errores.

Run: `npm run test -- --run`
Expected: toda la suite en verde.

Confirmado: `typecheck` limpio, 273/273 tests unitarios en verde (+3 nuevos de `isNotFuture`).
E2E local: 11/11 en verde (un fallo de registro en la corrida completa, flake ya observado en
tareas anteriores, no relacionado — pasó aislado en un rerun).

- [x] **Step 16: Verificación visual manual**

En una cartera, intenta **añadir un holding** y **vender** con fecha de mañana → el input impide
elegir futuro (`max`) y, si se fuerza, el submit muestra el error de Zod. Repite creando una
**valoración** en `/market/{id}`.

**Confirmado por el humano** (comprobación visual manual): calendario bloqueado a futuro y error de
Zod en los tres formularios (compra, venta, valoración); transacciones siguen aceptando fecha
futura sin cambios, tal como se decidió.

- [x] **Step 17: Commit frontend**

```bash
git add src/frontend-web/src/lib/dates.ts src/frontend-web/tests/unit/dates.test.ts "src/frontend-web/src/app/(private)/investments/[id]/page.tsx" "src/frontend-web/src/app/(private)/market/[id]/page.tsx"
git commit -m "feat(investments): impedir fechas futuras en compra, venta y valoración (guard frontend)"
```

---

## Task 4: Etiqueta UTC en fechas-hora (Bucket B · #3)

Las fechas con hora (Último acceso, Emails, Contactos) se muestran en UTC etiquetado, sin conversión
de zona. Backend confirmado en UTC (`DateTime.UtcNow`).

**Files:**
- Modify: `src/frontend-web/src/lib/dates.ts` (+ `tests/unit/dates.test.ts`)
- Modify: `src/frontend-web/src/app/(private)/profile/page.tsx`
- Modify: `src/frontend-web/src/app/(private)/emails/page.tsx`
- Modify: `src/frontend-web/src/app/(private)/contacts/page.tsx`

- [x] **Step 1: Ampliar el test de `dates.ts` con `formatDateTimeUtc` (RED)** — añade a `tests/unit/dates.test.ts`

```ts
import { formatDateTimeUtc } from '@/lib/dates'

describe('formatDateTimeUtc', () => {
  it('etiqueta la salida con " UTC"', () => {
    expect(formatDateTimeUtc('2026-07-09T10:30:00Z')).toMatch(/ UTC$/)
  })

  it('interpreta como UTC un timestamp sin sufijo Z (naive del backend)', () => {
    // Con y sin Z deben producir EXACTAMENTE la misma salida (misma hora de pared UTC).
    expect(formatDateTimeUtc('2026-07-09T10:30:00')).toBe(formatDateTimeUtc('2026-07-09T10:30:00Z'))
  })
})
```

- [x] **Step 2: Ejecutar y verificar que falla (RED)**

Run: `npm run test -- --run tests/unit/dates.test.ts`
Expected: FAIL — `formatDateTimeUtc` no está exportado.

Confirmado: 2 fallos (`formatDateTimeUtc is not a function`) + 7 tests preexistentes en verde.

- [x] **Step 3: Añadir `formatDateTimeUtc` a** `src/frontend-web/src/lib/dates.ts`

```ts
// Formatea un timestamp UTC del backend (DateTime.UtcNow, serializado a veces SIN sufijo Z
// desde MySQL datetime) mostrando la hora de pared en UTC y etiquetándola. NO convierte a la
// zona local: solución simple y sin ambigüedad para "Último acceso", emails y contactos.
export function formatDateTimeUtc(iso: string): string {
  // Sin 'Z', `new Date` interpretaría el string como hora LOCAL (origen del bug). Lo forzamos a UTC.
  const utc = iso.endsWith('Z') ? iso : `${iso}Z`
  const formatted = new Intl.DateTimeFormat('es-ES', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
    timeZone: 'UTC',
  }).format(new Date(utc))
  return `${formatted} UTC`
}
```

- [x] **Step 4: Ejecutar y verificar que pasa (GREEN)**

Run: `npm run test -- --run tests/unit/dates.test.ts`
Expected: PASS (9 tests en total en el fichero).

- [x] **Step 5: Usar el helper en Perfil** `src/frontend-web/src/app/(private)/profile/page.tsx`

Añade `import { formatDateTimeUtc } from '@/lib/dates'` y reemplaza la función local
`formatLastLogin` por:
```tsx
function formatLastLogin(iso: string | null): string {
  if (!iso) return 'Nunca'
  return formatDateTimeUtc(iso)
}
```

- [x] **Step 6: Usar el helper en Emails** `src/frontend-web/src/app/(private)/emails/page.tsx`

Añade `import { formatDateTimeUtc } from '@/lib/dates'`, **elimina** la función local `formatDate` y
sustituye su uso `{formatDate(email.sentAt)}` por `{formatDateTimeUtc(email.sentAt)}`.

- [x] **Step 7: Usar el helper en Contactos** `src/frontend-web/src/app/(private)/contacts/page.tsx`

Añade `import { formatDateTimeUtc } from '@/lib/dates'`, **elimina** la función local `formatDate` y
sustituye su uso `{formatDate(c.createdAt)}` por `{formatDateTimeUtc(c.createdAt)}`.

- [x] **Step 8: Verificar typecheck y suite**

Run: `npm run typecheck`
Expected: sin errores.

Run: `npm run test -- --run`
Expected: toda la suite en verde.

Confirmado: `typecheck` limpio, 275/275 tests unitarios en verde (+2 nuevos de `formatDateTimeUtc`).
Sin E2E afectado (ningún spec toca Perfil/Emails/Contactos).

- [x] **Step 9: Verificación visual manual**

En Perfil, Emails y Contactos, confirma que las fechas-hora terminan en " UTC" y muestran la hora UTC
(coherente con lo que hay en BD).

**Confirmado por el humano** (comprobación visual manual): las tres pantallas muestran la fecha-hora
etiquetada " UTC".

- [x] **Step 10: Commit**

```bash
git add src/frontend-web/src/lib/dates.ts src/frontend-web/tests/unit/dates.test.ts "src/frontend-web/src/app/(private)/profile/page.tsx" "src/frontend-web/src/app/(private)/emails/page.tsx" "src/frontend-web/src/app/(private)/contacts/page.tsx"
git commit -m "fix(frontend): etiquetar como UTC las fechas-hora de perfil, emails y contactos"
```

---

## Task 5: Refrescar el contenido de la página de Alcance (Bucket E · #1)

La página `/scope` describe la iteración 1: dice que contacto/emails van en localStorage y lista
`/users/me`, `/contacts`, `/emails` como *trabajo futuro*, cuando ya son backend real (iteración 2).
Además faltan Mercado y Valoraciones. Se reescribe el contenido (solo datos, sin cambiar el layout).

> **Revisión de contenido:** las listas de abajo son la propuesta a validar por el usuario. Ajustar
> redacción si lo indica antes/durante el PR.

**Files:**
- Modify: `src/frontend-web/src/app/(public)/scope/page.tsx`

- [ ] **Step 1: Actualizar `mvpFeatures`** con la realidad de la iteración 2

Sustituye el array `mvpFeatures` por:
```tsx
const mvpFeatures = [
  {
    area: 'Infraestructura',
    items: [
      'Monorepo Next.js 15 + .NET 8 + Docker Compose (MySQL + Qdrant)',
      'CI por ramas con PRs a develop + gate de revisión humana',
      'TDD: Vitest + RTL y Playwright (frontend) · xUnit + integración E2E (backend)',
      'Autenticación JWT con DPAPI (userId cifrado en token) y refresco proactivo en cliente',
    ],
  },
  {
    area: 'Landing pública',
    items: [
      'Hero con propuesta de valor y CTAs de registro/login',
      'Secciones de producto: Gastos, Inversiones, AI Scanner (demos visuales)',
      'Formulario de contacto contra endpoint real (POST /contacts)',
      'Página de Alcance y trabajos futuros + footer con navegación',
    ],
  },
  {
    area: 'Finanzas personales',
    items: [
      'Dashboard con resumen mensual, gráficas de ingresos/gastos y balance acumulado (Recharts)',
      'CRUD completo de Gastos/Ingresos con filtro mes/año y paginación',
      'Gráficas por categoría y serie mensual agregadas en backend (by-category / monthly)',
      'Soporte multimoneda (EUR, USD, GBP, CHF, JPY con tipos de cambio reales)',
    ],
  },
  {
    area: 'Inversiones',
    items: [
      'Carteras: crear, renombrar y borrar (con guard fiscal 409 si hay holdings abiertos)',
      'Holdings: alta, edición de notas, borrado y venta FIFO cross-lot a nivel empresa',
      'Performance: plusvalía realizada y no realizada + resumen global en Dashboard',
      'Mercado: catálogo de empresas (listado/detalle/alta) con combobox de búsqueda',
      'Valoraciones: alta, listado paginado y gráfico de serie por periodo (3M–5A)',
    ],
  },
  {
    area: 'Cuenta y perfil',
    items: [
      'Registro con moneda base y email de bienvenida (evento de dominio)',
      'Perfil real contra GET/PUT /users/me: edición de nombre y contraseña',
      'Registro de emails enviados (bienvenida + contacto) contra backend',
      'Listado de mensajes de contacto recibidos contra backend',
    ],
  },
  {
    area: 'AI Scanner (demo)',
    items: [
      'UI de chat con selector de LLM (Claude / GPT / Gemini)',
      'Panel RAG de documentos (estado local, pendiente de backend)',
      'Gestión de API keys por proveedor (local)',
      'Feature-flag NEXT_PUBLIC_AI_SCANNER_ENABLED para activar la demo',
    ],
  },
]
```

- [ ] **Step 2: Actualizar `futureWork`** retirando lo ya implementado

Sustituye el array `futureWork` por (fuera: `/users/me`, contactos/emails backend — ya hechos):
```tsx
const futureWork = [
  {
    area: 'IA / RAG',
    priority: 'high',
    items: [
      'Backend de AI Scanner: POST /ai/chat con streaming + historial',
      'Upload de documentos a Qdrant (embeddings, chunking, búsqueda semántica)',
      'Integración con Azure OpenAI / LLM externo (bloqueado por suscripción)',
      'MCP Server en Python: herramientas de screener y revisión de cartera',
    ],
  },
  {
    area: 'Inversiones',
    priority: 'medium',
    items: [
      'Editar y borrar empresas del catálogo (PUT/DELETE /companies/{id})',
      'Ver detalle y borrar valoraciones individuales (GET{id}/DELETE)',
      'Búsqueda de empresas server-side (?search=) para el combobox de holdings',
      'Envío real de emails de bienvenida y contacto (SendGrid / SES)',
    ],
  },
  {
    area: 'Producto',
    priority: 'medium',
    items: [
      'App Mobile React Native Expo (solo lectura, consume el mismo backend)',
      'Exportación a CSV / Excel de transacciones y cartera',
      'Alertas y notificaciones (precio objetivo, resumen semanal)',
      'Selector de divisa de visualización en UI (el backend ya convierte)',
    ],
  },
  {
    area: 'Plataforma',
    priority: 'low',
    items: [
      'Rol admin para gestión de contactos desde backoffice',
      'Kubernetes (manifiestos ya esquematizados en infra/k8s)',
      'Rate limiting y auditoría de accesos',
      'OAuth2 / inicio de sesión con Google',
    ],
  },
]
```

- [ ] **Step 3: Verificar typecheck**

Run: `npm run typecheck`
Expected: sin errores.

- [ ] **Step 4: Verificación visual manual**

Abre `/scope` y revisa que ninguna afirmación contradice la app real (nada de "localStorage" en
contacto/emails; aparecen Mercado y Valoraciones; el trabajo futuro no lista cosas ya hechas).

- [ ] **Step 5: Commit**

```bash
git add "src/frontend-web/src/app/(public)/scope/page.tsx"
git commit -m "docs(frontend): actualizar la página de Alcance a la iteración 2"
```

---

## Registro de deuda / notas

- **Transacciones con fecha futura:** se mantienen permitidas por decisión de negocio (un pago
  emitido a futuro es válido). Si algún día se aborda "Saldo real vs. corriente", revisar aquí.
- **`todayISO()` duplicado:** las Tareas 3–4 centralizan `todayISO`/`isNotFuture`/`formatDateTimeUtc`
  en `src/lib/dates.ts` y retiran las copias locales de `investments/[id]` y `market/[id]`. La copia
  de `transaction-sheet.tsx` se deja como fallback (no molesta); migrarla es opcional.
- **`npm run lint` no ejecutable:** deuda de tooling preexistente (`eslint` no está en
  `package.json`); fuera del alcance de este plan.

---

## Orden de ejecución sugerido

1 (higiene, quick win) → 2 (fecha por defecto) → 3 (no futuras, backend+frontend) → 4 (UTC) →
5 (Alcance). Cada tarea es un PR independiente con revisión humana.
