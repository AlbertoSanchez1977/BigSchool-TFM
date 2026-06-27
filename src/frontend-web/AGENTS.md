# AGENTS.md — Frontend Web

> **Contexto de aprendizaje.** El desarrollador domina C#/.NET y backend, pero es **principiante en
> frontend** (CSS, HTML, React/Next) — TypeScript le resulta familiar por su parecido con C#. Por
> tanto, al trabajar en este módulo el agente debe **explicar el porqué** de cada paso, no solo el
> qué, y apoyarse en analogías con C#/.NET cuando ayuden. Ver el glosario al final.

Diseño visual y decisiones de UX: `docs/03-frontend-design.md`. Prompt de v0: `docs/03-frontend-v0-prompt.md`.

---

## Tecnología (decisiones cerradas)

- **Next.js 14+ (App Router)**
- **TypeScript** (strict mode — sin `any` salvo caso extremo justificado)
- **Tailwind CSS** para estilos
- **shadcn/ui** para componentes (se *copian* al proyecto y se editan; no es un paquete cerrado)
- **Recharts** para gráficas
- **TanStack Query** (React Query) para estado servidor (cache, loading/error)
- **fetch** envuelto en un cliente con `Authorization: Bearer <jwt>`
- Estado local: Context API; **Zustand** solo si algo lo justifica

### Testing
- **Unitarios y componentes**: Vitest + React Testing Library
- **E2E**: Playwright (flujos críticos: login, crear gasto, ver gráficas, vender holding)
- No testear estilos ni implementación interna

---

## Estructura del Proyecto

```
frontend-web/
├── src/
│   ├── app/                  → App Router (páginas y layouts)
│   │   ├── (public)/         → Landing, Contacto, Alcance, Login, Registro
│   │   ├── (private)/            → Rutas protegidas (requieren JWT)
│   │   │   ├── dashboard/
│   │   │   ├── expenses/     → Gastos/Ingresos
│   │   │   ├── investments/  → Carteras → holdings
│   │   │   └── ai-scanner/   → Chat (toggle local / "Próximamente")
│   │   └── layout.tsx
│   ├── components/
│   │   ├── ui/              → Componentes base de shadcn (button, card, input, table…)
│   │   ├── charts/         → Envoltorios de Recharts (línea, barras por año)
│   │   └── forms/
│   ├── hooks/              → Custom hooks (useTransactions, usePortfolios…)
│   ├── lib/                → apiClient (fetch + JWT), utils, tipos compartidos
│   ├── services/           → Llamadas al Backend agrupadas por recurso
│   └── types/              → Interfaces TypeScript (DTOs del Backend)
├── public/
├── tests/{unit,e2e}/      → Vitest (unit) · Playwright (e2e)
├── next.config.mjs · tsconfig.json  (Tailwind v4: config por CSS en globals.css)
├── vitest.config.ts · playwright.config.ts · package.json
```

---

## Sistema de diseño (resumen — detalle en docs/03-frontend-design.md)

- **Light-first**, tokens preparados para oscuro. **Nunca** escribir un color literal: usar los
  tokens semánticos (`bg-background`, `text-foreground`, `text-positive`, `text-negative`…).
- Verde/rojo **solo** para signo de dinero; azul para acción; grises para el resto.
- Gráficas de línea en azul celeste apagado; barras agrupadas por año con la paleta `--chart-1..4`.
- **Altas/ediciones y acciones puntuales** (crear, editar, vender…) → **modal centrado** (`Dialog`),
  **nunca** panel lateral. Un solo componente desktop/móvil: centrado, `sm:max-w-md`, `max-h` con
  scroll interno, X arriba para cerrar. Referencia: `src/components/transactions/transaction-sheet.tsx`
  (detalle del patrón en `docs/03-frontend-design.md` §7).

---

## Funcionalidades (alcance MVP — alineado con ADR-008)

### Públicas
- **Landing**: generada con v0 (ver prompt). Secciones coherentes con capacidades reales.
- **Contacto**: formulario **simulado** (sin envío real; el email es pieza futura separada) NOTA HUMANA: Tendrá su endPoint en backend simulando el envío guardando registro en tabla.
- **Alcance y trabajos futuros**: página estática (se hace al final).
- **Registro / Login**: JWT real contra `/auth/register` y `/auth/login`; redirección a Dashboard. NOTA HUMANA: `/auth/register` Disparará en backend Evento de Dominio que simulará envío de email de bienvenida, simulación de envío guardando registro en tabla.

### Privadas (requieren JWT)
- **Dashboard**: KPIs + barras ingresos/gastos + línea de balance + resumen de inversiones +
  últimas transacciones (ver composición en el design doc).
- **Gastos/Ingresos**: lista + **modal centrado** de alta/edición, selector mes/año (por defecto
  mes actual), CRUD. Tabla responsive (en móvil, tarjetas de 2 líneas). Pestaña de gráficas: barras
  por categoría de los últimos 4 años — **agregación en cliente** (el Backend no agrega por categoría).
- **Inversiones**: carteras como cards (solo crear; no hay renombrar/borrar en Backend) →
  holdings como cards con **modal centrado** para **vender** (Disposal **FIFO a nivel empresa**, puede
  tocar varios lotes), editar Notes, añadir y borrar holding.
- **AI Scanner**: configuración con **toggle** activar/desactivar (solo local por coste). Si está
  off → pantalla **"Próximamente"**.
- NOTA HUMANA: **Emails Logging**: pantalla de listado sencilla (Esa tabla simulada) con: Emails de contacto enviados + email de bienvenida del usuario

### Fuera de alcance MVP
- **Editar usuario** (el Backend no tiene update de usuario aún).

---

## Huecos del Backend a tener presentes
1. Sin agregación por categoría → agregar en cliente desde la lista de transacciones.
2. Sin renombrar/borrar cartera → en el MVP la cartera solo se crea.
3. La venta es FIFO a nivel empresa → el botón "vender" de un holding vende acciones de *esa
   empresa* y consume lotes en orden FIFO.

---

## Integración con el Backend (NORMA — leer antes de tocar `types/` o `services/`)

- **Los tipos de `src/types/` son espejo de los DTOs del Backend.** Se **verifican contra el código
  fuente del Backend** (mismo monorepo, `src/backend/...`) **al inicio de la tarea que los consume**,
  nunca de memoria. Si vas a crear/editar un servicio o formulario de un recurso, primero localiza
  su DTO/Controller real y ajusta el tipo. Ejemplo de mismatch real ya corregido: las categorías son
  **anidadas** (`Category{idMainCategory, name, subCategories[]}`).
- **Base URL**: `NEXT_PUBLIC_API_URL=http://localhost:5285/api/v1`. Todos los controllers cuelgan de
  `/api/v1` (`/auth`, `/transactions`, `/categories`, `/portfolios`, `/companies`).
- **Envelope**: toda respuesta es `{ data, errors[], meta }`; `errors[] = { code, message, field? }`.
  No parsees `fetch` a mano: usa `lib/apiClient` (ya desenvuelve `data` y lanza `ApiError`).
- **Enums viajan como nombre string** (`JsonStringEnumConverter`): `TransactionType`
  (`"Income"`/`"Expense"`), `MainCategory` (`"EssentialExpenses"`…), `Currency` (`"EUR"`…). Ver
  `src/types/enums.ts`.
- **Sesión/refresco**: no hay refresh token en el Backend. Refresco **proactivo** en cliente con
  `lib/auth/refreshPolicy` (máx. 5 refrescos o 24 h → re-login; token de acceso ~60 min). El
  `AuthProvider` (Task 2) cablea el timer y la llamada `POST /auth/refresh`.
- **Tipos provisionales**: `portfolios.ts` y `companies.ts` están marcados como tales hasta
  verificarlos en sus tareas (8-10).

> Spec de detalle: `docs/superpowers/specs/003-2026-06-24-frontend-web-mvp-design.md`.
> Plan de ejecución: `docs/superpowers/plans/012-2026-06-24-frontend-web-mvp.md`.

---

## Convenciones de Código

### Nombrado
- Componentes: PascalCase (`ExpenseTable.tsx`, `MonthlyChart.tsx`)
- Hooks: camelCase con prefijo `use` (`useExpenses.ts`, `usePortfolio.ts`)
- Servicios: camelCase (`transactionService.ts`)
- Tipos: PascalCase (`Transaction`, `CreateTransactionDto`)

### Componentes
- Funcionales siempre (no clases). Props tipadas con `interface`.
- Separar **lógica** (hooks/servicios) de **presentación** (componentes).
- Componentes pequeños y específicos; declarativos.

### Estilos
- Tailwind primero; tokens semánticos del design system (no hex sueltos).
- Sin CSS modules salvo caso justificado. Responsive **mobile-first**.

### Datos
- **Toda** comunicación con el Backend pasa por TanStack Query.
- Manejar siempre estados de **carga, error y vacío** en cada vista.

---

## Instrucciones para el Agente

1. **Explicar mientras se construye** — el objetivo no es solo entregar, es que el desarrollador
   aprenda. Comentar decisiones, conceptos React/Next y trampas comunes de CSS.
2. TypeScript strict; sin `any` injustificado.
3. Componentes declarativos; nada de lógica de negocio dentro de la presentación.
4. Estado servidor con TanStack Query; estados de carga/error/vacío siempre.
5. Respetar el sistema de diseño (tokens), no improvisar colores.
6. Coherencia visual ante todo: ya hay sistema de diseño, no se "prioriza función sobre estética".

---

## Glosario React/Next ↔ C#/.NET (para aprendizaje)

| Frontend | Equivalente mental en C#/.NET |
|----------|-------------------------------|
| Componente funcional | Método que devuelve UI (como un partial view / Razor component) |
| Props | Parámetros del método/componente (inmutables desde dentro) |
| `useState` | Campo de estado local que, al cambiar, re-renderiza la "vista" |
| `useEffect` | Efecto secundario al montar/cambiar dependencias (≈ `OnInitialized`/eventos de ciclo de vida) |
| Custom hook (`useX`) | Servicio/lógica reutilizable extraída (como una clase de servicio inyectada) |
| TanStack Query | `HttpClient` + cache con invalidación y estados de carga/error |
| Context Provider | Contenedor de dependencias por árbol de componentes (≈ scope de DI) |
| Tailwind classes | Estilos utilitarios en el marcado (en vez de CSS/clases externas) |
| Variables CSS (tokens) | Tema centralizado (≈ `ResourceDictionary`/constantes), no valores mágicos |
| `tsconfig` strict | `<Nullable>` + analizadores estrictos del compilador C# |
