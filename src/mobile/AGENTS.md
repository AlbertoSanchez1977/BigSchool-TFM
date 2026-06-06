# AGENTS.md — Mobile App

## Tecnología
- React Native con Expo (Managed Workflow)
- TypeScript
- React Navigation para navegación
- React Query (TanStack Query) para datos
- Componentes nativos de Expo (expo-charts o react-native-chart-kit)

## Estructura del Proyecto

```
mobile/
├── src/
│   ├── screens/            → Pantallas de la app
│   │   ├── DashboardScreen.tsx
│   │   ├── ExpensesScreen.tsx
│   │   └── InvestmentsScreen.tsx
│   ├── components/         → Componentes reutilizables
│   ├── hooks/             → Custom hooks
│   ├── services/          → API client (compartir tipos con frontend-web)
│   ├── navigation/        → Configuración de navegación
│   └── types/             → Tipos TypeScript
├── app.json
├── tsconfig.json
└── package.json
```

## Funcionalidades (Solo Lectura)

- Visualización de balance mensual
- Gráfica de gastos/ingresos
- Listado de inversiones con valoración actual
- Gráfica de evolución de cartera
- Pull-to-refresh para actualizar datos

## Restricciones

- **Solo lectura** — No hay formularios de creación/edición
- **Misma API** — Consume los mismos endpoints que el frontend web
- **Autenticación** — Mismo JWT que la web (login simple)

## Convenciones de Código

- Mismas convenciones que frontend-web (TypeScript strict, PascalCase componentes)
- Componentes funcionales con hooks
- Estilos con StyleSheet.create() o librería de estilos tipo NativeWind

## Instrucciones para el Agente

1. Mantener la app lo más simple posible — es un demostrador
2. Reutilizar tipos e interfaces del frontend-web cuando sea posible
3. El desarrollador tiene experiencia baja en mobile — priorizar simplicidad
4. Usar componentes de Expo siempre que existan antes de buscar librerías externas
5. No optimizar prematuramente — la app solo muestra datos
