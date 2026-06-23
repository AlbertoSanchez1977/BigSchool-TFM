# Prompt de v0 — Sistema de diseño + Landing

**Uso:** pégalo como **primer mensaje** en [v0.app](https://v0.app/). Esta tirada genera el
**sistema de diseño + la Landing pública** (lo más caro de hacer a mano). El resto de la app
(pantallas privadas) se construye con shadcn/ui + código, reutilizando el theme y los componentes
base que devuelva v0.

> Con plan Free: si v0 ofrece iterar, **guarda créditos** — descarga el código y se sigue en local.

Referencia de tokens y decisiones: `docs/03-frontend-design.md`.

---

```text
Eres un diseñador de producto senior. Crea el sistema de diseño y la LANDING PAGE pública
de una webapp fintech llamada "BigSchool": finanzas personales + inversiones (value investing)
con un asistente de IA. Todo el texto visible en ESPAÑOL.

STACK: Next.js (App Router), TypeScript, Tailwind CSS, shadcn/ui y Recharts para gráficas.
Entrega componentes reutilizables (Navbar, Footer, Button, Card, Section) y un theme con
variables CSS (bloques :root y .dark) para reutilizar en el resto de la app.

ESTÉTICA: fintech sobria, limpia y data-forward. Base blanco + grises fríos (slate); acento
azul celeste apagado. Verde/rojo SOLO para signo de dinero (ganancia/pérdida), tonos de
saturación media, nada flúor. Inspiración: estilo "modern-minimal", landing tipo
v0-optimus-delta.vercel.app, y la densidad de datos de la zona privada de DeGiro. Modo claro
por defecto, pero deja el tema preparado para oscuro.

PALETA (HSL, úsala en las variables CSS):
- fondo 0 0% 100%, texto 222 20% 18%, muted 215 20% 96%, borde 215 18% 90%
- primario/azure 208 78% 47%
- positive 145 55% 38%, negative 0 65% 48%, info 208 78% 47%
- gráfica línea 205 65% 55%; barras por año: 208 70% 52% / 178 45% 42% / 245 45% 58% / 215 25% 55%

GRÁFICAS: líneas con protagonismo del azul celeste apagado; barras agrupadas por año con la
paleta categórica anterior (un color por año, armónicos, no chillones).

SECCIONES DE LA LANDING (deben reflejar capacidades REALES del producto):
1. Hero: titular sobre controlar gastos e inversiones con ayuda de IA + botones "Registrarse"
   e "Iniciar sesión". Ilustración/mock sutil de un dashboard.
2. Gastos e Ingresos: seguimiento mensual multimoneda con filtros. Incluye una mini gráfica
   de barras FAKE Ingresos vs Gastos por mes (con los colores por año).
3. Inversiones: carteras, ventas FIFO y rentabilidad multimoneda (realizado / no realizado).
   Incluye una mini gráfica de LÍNEA FAKE del valor de cartera (azul celeste) y 3 tarjetas KPI
   (Valor de mercado, PnL total, % retorno).
4. AI Scanner (etiqueta "Próximamente"): tres flujos — Screener (filtrar empresas),
   Criterios (análisis a fondo de una empresa) y Revisión de cartera.
5. Cierre con CTA + Footer con enlaces: Contacto, Alcance y trabajos futuros, Iniciar sesión.

DATOS FAKE COHERENTES (moneda base EUR): nómina ~2.850 €, gastos mensuales variados;
cartera con AAPL, MSFT, SAN, valor de mercado ~6.500 €, PnL +540 €, retorno +9,1 %.

REQUISITOS: responsive (móvil y escritorio), buen contraste (accesible), componentes
reutilizables y código limpio. No inventes funciones que no estén en estas secciones.
```
