# BigSchool-TFM — Diseño del Frontend-Web

**Fecha**: 2026-06-23
**Estado**: Diseño aprobado (MVP)
**Relación**: deriva de `docs/01-arquitectura.md` (ADR-008, reenfoque a MVP).

Este documento fija el sistema de diseño y las decisiones de UX del Frontend-Web. El detalle de
implementación (scaffolding, convenciones de código, glosario de aprendizaje) vive en
`src/frontend-web/AGENTS.md`.

---

## 1. Principios

1. **Fintech sobrio**: los grises mandan; el color es la excepción, no la regla.
2. **Light-first**: tema claro por defecto, con tokens preparados para modo oscuro (toggle al final).
3. **Datos reales**: cada gráfica, KPI o tabla debe corresponder a una capacidad real del Backend.
   La Landing (datos *fake*) solo muestra lo que el producto sabe hacer.
4. **Honestidad**: nada de funciones prometidas que no existan; el AI Scanner se marca "Próximamente".

---

## 2. Fundamentos

| Aspecto | Decisión | Por qué |
|---|---|---|
| Tipografía | **Inter** (UI) — alternativa: Geist | Clásica fintech, legible en tablas densas |
| Radio de borde | `0.5rem` (medio) | Limpio, ni cuadrado ni "burbuja" |
| Densidad | Filas compactas en tablas (estilo zona privada DeGiro) | Datos densos sin agobiar |
| Tema | **Light-first**, dark preparado | Decisión de diseño |
| Formato de color | Variables CSS (HSL) estilo shadcn/ui | Un token = un significado |

---

## 3. Tokens de color

> **Cómo leerlo:** cada variable es un *canal HSL* (`tono saturación% luz%`). Los componentes
> **nunca** escriben un color literal; escriben `hsl(var(--positive))`. Para el modo oscuro el
> patrón es: **mismo tono, subo la luz y bajo la saturación**. Eso resuelve el contraste.

```css
:root {
  /* Neutros (gris frío slate) */
  --background:        0 0% 100%;     /* blanco */
  --foreground:        222 20% 18%;   /* casi negro azulado */
  --card:              0 0% 100%;
  --card-foreground:   222 20% 18%;
  --muted:             215 20% 96%;   /* gris muy claro (fondos sutiles) */
  --muted-foreground:  215 15% 45%;   /* gris medio (texto secundario) */
  --border:            215 18% 90%;
  --input:             215 18% 90%;

  /* Marca / primario: azul celeste apagado */
  --primary:            208 78% 47%;
  --primary-foreground: 0 0% 100%;
  --ring:               208 78% 47%;

  /* Semánticos de bolsa (saturación media, nada flúor) */
  --positive:          145 55% 38%;   /* ganancia */
  --negative:          0 65% 48%;     /* pérdida */
  --info:              208 78% 47%;   /* acción / neutro-bolsa */

  /* Gráficas: línea = celeste apagado; barras = paleta categórica armónica (años) */
  --chart-line:        205 65% 55%;   /* azul celeste apagado, protagonista */
  --chart-1:           208 70% 52%;   /* azure        (año A) */
  --chart-2:           178 45% 42%;   /* teal         (año B) */
  --chart-3:           245 45% 58%;   /* índigo       (año C) */
  --chart-4:           215 25% 55%;   /* gris-azulado (año D) */
}

.dark {
  --background:        222 24% 10%;
  --foreground:        210 20% 92%;
  --card:              222 22% 13%;
  --card-foreground:   210 20% 92%;
  --muted:             217 18% 20%;
  --muted-foreground:  215 15% 65%;
  --border:            217 18% 22%;
  --input:             217 18% 22%;

  --primary:            208 75% 60%;  /* +luz, -saturación */
  --primary-foreground: 222 24% 10%;
  --ring:               208 75% 60%;

  --positive:          145 45% 58%;   /* verde más claro y suave */
  --negative:          0 70% 65%;     /* rojo más claro */
  --info:              208 75% 65%;

  --chart-line:        205 70% 65%;
  --chart-1:           208 70% 62%;
  --chart-2:           178 45% 55%;
  --chart-3:           245 50% 70%;
  --chart-4:           215 25% 68%;
}
```

**Reglas de uso:**
- Verde/rojo (`--positive`/`--negative`) **solo** para signo de dinero (ganancia/pérdida,
  ingreso/gasto). El azul (`--primary`/`--info`) es el color de acción/interacción.
- Los grises ocupan ~90% de la pantalla → es lo que da el aire "fintech sobrio".
- **Barras por año**: `--chart-1..4` rotan por año (Ene-2024 azure, Ene-2025 teal, Ene-2026
  índigo…). Todos fríos y apagados → contraste sin chillar.
- **Líneas**: protagonismo de `--chart-line` (azul celeste apagado).

---

## 4. Mapa de páginas ↔ Backend

| Página | Acceso | Datos / endpoints reales | Estado |
|--------|--------|--------------------------|--------|
| **Landing** | Público | Datos *fake* coherentes con las capacidades reales | MVP |
| **Contacto** | Público | — (formulario *fake*, sin envío real) | MVP (simulado) |
| **Alcance y trabajos futuros** | Público | Estática | MVP (al final) |
| **Login / Registro** | Público | `/auth/register`, `/auth/login`, `/auth/refresh` (JWT) | ✅ |
| **Dashboard** | Privado | `/transactions/summary`, `/transactions/monthly-chart`, `/portfolios/{id}/performance` | ✅ |
| **Gastos/Ingresos** | Privado | `/transactions` (CRUD, filtros, paginación), `/categories` | ✅ |
| **Inversiones** | Privado | `/portfolios`, `/portfolios/{id}`, holdings, `/sales`, `/performance`, `/companies` | ✅ |
| **AI Scanner** | Privado | — (toggle local-only + "Próximamente") | Futuro |
| **Editar usuario** | Privado | — (Backend sin update de usuario) | Fuera de alcance MVP |

---

## 5. Huecos conocidos del Backend (atajos aceptados para el MVP)

1. **No hay agregación por categoría.** El Backend agrega por mes y por tipo, no por categoría.
   → La gráfica "barras por categoría (últimos 4 años)" se calcula **en el navegador** a partir
   de la lista de transacciones. Aceptable para la demo.
2. **No hay renombrar/borrar cartera.** Solo `POST /portfolios` (crear) y la gestión de holdings.
   → En el MVP la cartera no se edita ni se borra (solo se crea y se gestionan sus holdings).
3. **La venta no es por holding, es FIFO a nivel empresa.** `POST /portfolios/{id}/sales` vende
   acciones de *una empresa* y consume los lotes (holdings) en orden FIFO automáticamente.
   → El botón "vender" de una card de holding debe comunicar que vende acciones de *esa empresa*
   y puede tocar varios lotes.

---

## 6. Composición del Dashboard

De arriba a abajo:

1. **Fila de tarjetas KPI** (4): Balance del mes · Ingresos vs Gastos del mes · Tasa de ahorro ·
   Valor de cartera + PnL total.
2. **Barras Ingresos vs Gastos** del año actual por mes (`/transactions/monthly-chart`).
3. **Línea de evolución del balance** (acumulado, derivado de transacciones).
4. **Mini-resumen de inversiones**: valor de mercado, realizado/no realizado, % retorno, top holdings.
5. **Últimas 5 transacciones**.

---

## 7. Layouts por página

### Patrón de alta/edición/acciones — modal centrado (decisión 2026-06-27)

Todos los formularios de **crear/editar** y las **acciones puntuales** (p. ej. vender) se presentan
en un **modal centrado** (`Dialog` de shadcn/base-ui), **no** en un panel lateral. Razones: un único
componente sirve para desktop y móvil sin bifurcar lógica, y encaja con el estándar "tarjeta
flotante" (estilo Trade Republic) mejor que el cajón lateral (estilo DeGiro) de la primera iteración.

Especificación del patrón:
- Centrado en pantalla; ancho `sm:max-w-md` en desktop, casi pantalla completa en móvil.
- `max-h-[calc(100dvh-2rem)]` con **scroll interno** (clave para iPhone SE y teclado abierto).
- Botón **X** arriba a la derecha para cerrar; contenido con padding `p-6`.
- Campos esenciales en **grid responsive** (en móvil 2 columnas para compactar verticalmente).
- **Implementación de referencia**: `src/components/transactions/transaction-sheet.tsx`
  (mantiene el nombre `*-sheet` por compatibilidad, pero internamente es un `Dialog` centrado).

### Gastos/Ingresos
- **Listado + modal centrado de alta/edición** (ver patrón arriba). Click en una fila → modal de edición.
- Selector de **mes/año** (por defecto, mes actual). Insertar/Modificar/Borrar desde ahí.
- **Tabla responsive**: en desktop, tabla completa; en móvil, cada transacción es una tarjeta de
  dos líneas (`Fecha │ Importe` arriba, `Categoría · Subcategoría` debajo). La descripción no es
  columna: se indica con un icono (con la descripción en el `title`).
- **Pestaña de gráficas**: barras agrupadas por año (últimos 4 años) por categoría de gasto e
  ingreso (agregación en cliente — ver hueco #1).

### Carteras
- **Lista de cards alargadas** con todas las carteras + **modal centrado** para **crear** (no editar,
  ver hueco #2). Click en una card → pantalla de holdings.

### Holdings
- **Lista de cards alargadas** + **modal centrado** para: **vender** (Disposal FIFO a nivel empresa —
  ver hueco #3), **editar Notes** del holding, **añadir** holding y **borrar** holding.

### AI Scanner
- **Configuración con toggle** activar/desactivar (por coste, solo local de momento).
- Si está **off** → pantalla **"Próximamente"**.
- Si está **on** (local) → chat + (a futuro) los flujos screener / criterios / revisión de cartera.

### Públicas (Landing, Contacto, Alcance)
- Landing según el prompt de v0 (`docs/03-frontend-v0-prompt.md`).
- Contacto: formulario simulado (sin envío real; pieza de email separada y futura).
- Alcance: tabla de funcionalidades actuales vs futuras (se hace al final).

---

## 8. Flujo de trabajo: tres capas

| Capa | Herramienta | Qué produce | Coste |
|------|-------------|-------------|-------|
| **A. Diseño + Landing** | **v0** | Sistema de diseño (theme/tokens) + Landing pública | Prompts v0 |
| **B. "Muebles"** | **shadcn/ui blocks** | Login, sidebar, layout dashboard, tablas, formularios | Gratis |
| **C. Lógica** | **Código (manual)** | Conexión Backend API, auth JWT, datos reales, gráficas, toggles, routing/guards | Tiempo |

> Con plan Free de v0: gastar los prompts **solo en la capa A**. El resto se construye con
> shadcn (gratis) + código, reutilizando el theme y los componentes base que genere v0.

---

## 9. Stack

- **Next.js (App Router) + TypeScript (strict)**
- **Tailwind CSS** + **shadcn/ui** (componentes que se copian al proyecto, no un paquete cerrado)
- **Recharts** para gráficas
- **TanStack Query** (React Query) para estado servidor (cache, loading/error)
- **fetch** envuelto en un cliente con el `Authorization: Bearer` del JWT

Auth y routing protegido (dónde guardar el token, guards de ruta) se deciden al montar el esqueleto.

---

## 10. Apéndice

- Prompt de v0 (copia-pega): `docs/03-frontend-v0-prompt.md`
- Convenciones de código y glosario de aprendizaje React↔C#: `src/frontend-web/AGENTS.md`
