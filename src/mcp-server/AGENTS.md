# AGENTS.md — MCP Server

## Tecnología
- TypeScript / Node.js
- SDK oficial de MCP (@modelcontextprotocol/sdk)
- Comunicación con Backend API via HTTP

## Estructura del Proyecto

```
mcp-server/
├── src/
│   ├── index.ts            → Punto de entrada, configuración del servidor
│   ├── tools/              → Herramientas MCP expuestas
│   │   ├── balance.ts      → Consulta de balance financiero
│   │   ├── expenses.ts     → Consulta de gastos/ingresos
│   │   └── investments.ts  → Consulta de inversiones
│   ├── services/           → Clientes HTTP al backend
│   └── types/              → Tipos TypeScript
├── tsconfig.json
├── package.json
└── README.md
```

## Herramientas MCP Expuestas

### `get_balance`
- Parámetros: `month` (opcional), `year` (opcional)
- Devuelve: Balance del periodo (ingresos, gastos, neto)

### `get_expenses`
- Parámetros: `month`, `year`, `category` (opcional)
- Devuelve: Lista de gastos con totales

### `get_investments`
- Parámetros: `ticker` (opcional)
- Devuelve: Resumen de cartera o detalle de una acción

### `get_portfolio_chart`
- Parámetros: `period` (1m, 3m, 6m, 1y)
- Devuelve: Datos para graficar evolución de cartera

## Convenciones de Código

- TypeScript strict
- Funciones pequeñas y bien tipadas
- Manejo de errores explícito (try/catch con mensajes claros)
- Documentación JSDoc en cada herramienta

## Instrucciones para el Agente

1. El MCP Server es un wrapper fino sobre la API del backend
2. No debe contener lógica de negocio — solo transformar y exponer datos
3. Cada herramienta debe tener descripción clara para que el LLM la use bien
4. Manejar errores del backend graciosamente (timeouts, 404, 500)
5. Mantener el servidor ligero y sin estado
