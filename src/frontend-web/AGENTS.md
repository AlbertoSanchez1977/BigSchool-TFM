# AGENTS.md — Frontend Web

## Tecnología
- Next.js 14+ (App Router)
- TypeScript (strict mode)
- Tailwind CSS para estilos
- Componentes UI generados con v0.app cuando sea necesario
- Chart.js o Recharts para gráficas
- React Query (TanStack Query) para estado servidor
- Zustand o Context API para estado local
- Axios o fetch nativo para HTTP

## Testing
- **Unitarios y componentes**: Vitest + React Testing Library
- **E2E**: Playwright

## Estructura del Proyecto

```
frontend-web/
├── src/
│   ├── app/                  → App Router (páginas y layouts)
│   │   ├── (auth)/          → Páginas protegidas
│   │   ├── dashboard/       → Dashboard principal
│   │   ├── expenses/        → Gestión de gastos/ingresos
│   │   ├── investments/     → Gestión de inversiones
│   │   ├── ai-scanner/      → Chat con LLM + gestión RAG
│   │   └── layout.tsx
│   ├── components/          → Componentes reutilizables
│   │   ├── ui/             → Componentes base (botones, inputs, cards)
│   │   ├── charts/         → Componentes de gráficas
│   │   └── forms/          → Formularios
│   ├── hooks/              → Custom hooks
│   ├── lib/                → Utilidades, API client, tipos
│   ├── services/           → Llamadas a la API backend
│   └── types/              → Interfaces y tipos TypeScript
├── public/
├── tests/
│   ├── unit/              → Tests con Vitest
│   └── e2e/               → Tests con Playwright
├── next.config.ts
├── tailwind.config.ts
├── tsconfig.json
├── vitest.config.ts
├── playwright.config.ts
└── package.json
```

## Funcionalidades

### Dashboard
- Balance general (ingresos - gastos del mes)
- Gráfica de evolución mensual
- Resumen de cartera de inversiones
- Alertas o notificaciones

### Gastos/Ingresos
- Tabla con filtros (fecha, categoría, importe)
- Formulario de alta/edición
- Gráficas por categoría (pie chart)
- Gráficas por mes (bar/line chart)
- Exportación (CSV opcional)

### Inversiones
- Listado de acciones en cartera
- Histórico de valoraciones por empresa
- Gráfica de evolución de cartera
- Rentabilidad por acción

### AI Scanner (Chat + RAG)
- Chat conversacional con el LLM
- Visualización de empresas sugeridas
- Panel de gestión del contexto RAG (subir/eliminar documentos)
- Historial de conversaciones

## Convenciones de Código

### Nombrado
- Componentes: PascalCase (`ExpenseTable.tsx`, `MonthlyChart.tsx`)
- Hooks: camelCase con prefijo use (`useExpenses.ts`, `usePortfolio.ts`)
- Servicios: camelCase (`expenseService.ts`)
- Tipos: PascalCase con sufijo si es necesario (`Expense`, `CreateExpenseDto`)

### Componentes
- Componentes funcionales siempre (no clases)
- Props tipadas con interface
- Separar lógica (hooks) de presentación (componentes)
- Componentes pequeños y específicos

### Estilos
- Tailwind CSS como primera opción
- No CSS modules salvo caso justificado
- Usar v0.app para generar componentes complejos de UI
- Responsive: mobile-first

### Testing
- Vitest + React Testing Library para componentes y hooks
- Playwright para flujos E2E completos
- No testear estilos ni implementación interna
- Tests E2E: flujos críticos (login, crear gasto, ver gráficas)

## Instrucciones para el Agente

1. Usar TypeScript strict — no `any` salvo caso extremo justificado
2. Los componentes de UI deben ser lo más declarativos posible
3. No poner lógica de negocio en componentes — extraer a hooks o servicios
4. Usar React Query para toda comunicación con el backend
5. Manejar estados de carga, error y vacío en todas las vistas
6. El desarrollador tiene experiencia baja en CSS — usar Tailwind y v0.app
7. Priorizar funcionalidad sobre estética, pero mantener coherencia visual
