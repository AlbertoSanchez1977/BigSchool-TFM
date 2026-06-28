# Spec — Infraestructura: E2E dockerizado, seed demo enriquecido, despliegue Docker/k8s, Azure y CI

- **Fecha:** 2026-06-28
- **Rama:** `feature/infra-azure-spec` (desde `develop`)
- **Ámbito:** `infra/`, `src/frontend-web/` (config E2E), `.github/workflows/`, `deploy/azure/` (gitignored)
- **Estado:** especificación aprobada para planificar (un plan de implementación por bloque)

## Contexto y motivación

El proyecto tiene hoy un `docker-compose.yml` + `docker-compose.override.yml` que solo levantan
**mysql** y **qdrant**; los servicios `backend`/`frontend-web` están comentados y **no existen sus
Dockerfiles**. El backend de integración ya usa un patrón de BD dedicada (`bigschool_test`, creada/migrada/
destruida por `MySqlDatabaseFixture`) para no interferir con `bigschool` (depuración humana). Queremos
trasladar esa filosofía de aislamiento a los E2E del frontend y, además, dejar lista la dockerización
completa, un seed de demo rico, scripts de despliegue en Azure y un CI.

### Hechos del código verificados (condicionan el diseño)

- El **backend no migra en arranque**: `Program.cs` solo hace `AddHealthChecks()` + `MapHealthChecks("/health")`.
  Por tanto cualquier BD nueva (p. ej. `bigschool_e2e`) necesita recibir el esquema explícitamente (vía `init.sql`).
- El backend lee la conexión de `ConnectionStrings:DefaultConnection` (env `ConnectionStrings__DefaultConnection`).
- `playwright.config.ts` hoy arranca el frontend con `webServer: pnpm dev` y asume el backend manual en `:5285`.
  Los specs E2E registran usuarios con **email único** por ejecución → no dependen de datos sembrados.
- `Portfolio.RealizedPnLCurrency` está atado a la **moneda base del usuario** (un usuario EUR no puede
  tener una cartera USD). `Portfolio.AddHolding` valida `baseCurrency == RealizedPnL.Currency`.
- El frontend inlinea `NEXT_PUBLIC_API_URL` en **build** (`src/lib/api.ts`) → debe entrar como build-arg.
- `next.config.mjs` no declara `output: 'standalone'`.
- Companies existentes (IDs 1-9): AAPL·USD, MSFT·USD, SAN·EUR, SHEL·GBP, NVDA·USD, ITX·EUR, IBE·EUR,
  AMZN·USD, TSLA·USD.

### Decisiones transversales tomadas

| Tema | Decisión |
|------|----------|
| BD de E2E | Reusar el contenedor MySQL dev existente; crear `bigschool_e2e` dentro |
| Generación del seed | Script generador determinista que **emite** `seed.sql` (se commitean ambos) |
| Modelo de moneda de cartera | **Dos usuarios** (EUR y USD), cada cartera cuelga del usuario de su moneda |
| IaC Azure | Azure CLI puro (`az`, pwsh), en `deploy/azure/`, **gitignored** |
| Despliegue | **Solo local**: Actions compila/testea/empaqueta sin secretos; deploy = scripts `az` locales |
| Adquisiciones | Alineadas a **día-1 de mes** para casar con `ExchangeRates` |
| Manifiestos k8s | **Diferidos** a un plan posterior (esta spec deja imágenes que arrancan) |

### Mapa de puertos (clave: convivencia con debug desde IDE local)

Los puertos dockerizados se eligen para **no chocar** con los de ejecución/depuración desde compiladores
locales (IDE): el backend local sigue en `5285` y el `next dev` local en `3000`, libres.

| Stack | Backend (host → contenedor) | Frontend (host → contenedor) |
|-------|------------------------------|-------------------------------|
| Local IDE (sin cambios) | `5285` | `3000` |
| Dockerfile EXPOSE (Bloque 0) | escucha en `8081` | escucha en `3001` |
| E2E dockerizado (Bloque 1) | `8081 → 8081` | `3001 → 3001` |
| Compose completo (Bloque 3) | `8082 → 8081` | `3002 → 3001` |

> Nota de revisión: el "5000→8082" indicado se interpreta como **host 8082 → contenedor 8081**
> (la imagen del Bloque 0 escucha siempre en 8081; solo cambia el puerto publicado en el host). Confirmar.

---

## Bloque 0 — Fundación compartida: Dockerfiles

Se escriben una sola vez; los consumen los bloques 1, 3, 4 y 5.

### `infra/docker/backend.Dockerfile`
- Multi-stage: `mcr.microsoft.com/dotnet/sdk:8.0` (restore/build/publish) → `mcr.microsoft.com/dotnet/aspnet:8.0` (runtime).
- Publica `BigSchool.WebApi`. **`EXPOSE 8081`**, `ENV ASPNETCORE_URLS=http://+:8081`.
- Healthcheck a `/health` (en el puerto 8081).
- Conexión por env `ConnectionStrings__DefaultConnection` (no hardcodear).

### `infra/docker/frontend-web.Dockerfile`
- Multi-stage Node: `deps` (instala con pnpm) → `build` (`next build`) → `runner` (imagen mínima).
- **Añadir `output: 'standalone'`** a `next.config.mjs` para copiar solo el runtime necesario.
- `ARG NEXT_PUBLIC_API_URL` pasado como build-arg (se inlinea en build). **`EXPOSE 3001`**, `ENV PORT=3001`.
- Comando de arranque: `node server.js` (salida standalone) escuchando en `3001`.

### Criterios de aceptación
- `docker build` de ambos Dockerfiles produce imágenes que arrancan en local.
- El backend responde `200` en `/health` (`:8081`); el frontend sirve la landing en `:3001`.

---

## Bloque 1 — E2E dockerizado y aislado

**Objetivo:** que `pnpm test:e2e` levante un entorno efímero (frontend + backend dockerizados + BD
`bigschool_e2e` solo-estructura), ejecute los specs contra el **frontend dockerizado** y lo limpie todo
al terminar, sin tocar `bigschool` ni `bigschool_test`.

### Componentes
- **`infra/docker-compose.e2e.yml`**: define `backend` y `frontend-web` (build con Dockerfiles del Bloque 0),
  unidos a la red `bigschool-network`. Mapeos de puerto: backend `8081:8081`, frontend `3001:3001`.
  El backend se conecta al MySQL existente (`bigschool-mysql`) con
  `ConnectionStrings__DefaultConnection` → `Database=bigschool_e2e`. El frontend se construye con
  `NEXT_PUBLIC_API_URL=http://localhost:8081/api/v1` (lo llama el navegador de Playwright desde el host).
- **Esquema de `bigschool_e2e`**: se aplica `init.sql` con `USE bigschool` reescrito a `USE bigschool_e2e`
  (el `init.sql` ya incluye el `__EFMigrationsHistory`, así que el backend no intentará migrar). **Sin seed.**
- **Orquestación en `src/frontend-web/playwright.config.ts`**:
  - Se elimina `webServer: pnpm dev`. `baseURL` → **`http://localhost:3001`** (frontend dockerizado).
  - `globalSetup` (`tests/e2e/global-setup.ts`):
    1. Crea `bigschool_e2e` y aplica el esquema (init.sql adaptado) sobre el MySQL dev.
    2. `docker compose -f infra/docker-compose.e2e.yml up -d --build`.
    3. Espera healthchecks de backend (`/health` en `:8081`) y frontend (`:3001`).
  - `globalTeardown` (`tests/e2e/global-teardown.ts`):
    1. `docker compose -f infra/docker-compose.e2e.yml down` (elimina contenedores front/back).
    2. `DROP DATABASE bigschool_e2e`.

### Prerrequisito y robustez
- Requiere el MySQL dev levantado (`docker compose up -d mysql`). El `globalSetup` debe **fallar con mensaje
  claro** si no está disponible.

### Criterios de aceptación
- `pnpm test:e2e` (en limpio, con solo mysql dev arriba) levanta todo, los specs existentes
  (`login`, `create-expense`, `sell-holding`) pasan contra el front dockerizado (`:3001`), y al terminar no
  quedan contenedores `bigschool-e2e-*` ni la BD `bigschool_e2e`.
- `bigschool` y `bigschool_test` quedan intactas. Los puertos locales `3000`/`5285` siguen libres durante el run.

---

## Bloque 2 — Seed demo enriquecido (dos usuarios)

**Objetivo:** un `seed.sql` que **reemplaza** el actual (3 meses) por un dataset rico y determinista,
generado por script.

### Generador
- **`infra/docker/mysql/generate_seed.py`**: script Python determinista y parametrizable
  (fecha-ancla = hoy). Emite `infra/docker/mysql/seed.sql`. Se commitean el generador y el `seed.sql`.
- Reglas de cálculo replicadas del dominio: `BuyBaseAmount = round(BuyOriginalAmount * BuyExchangeRate, 2)`
  (banker's rounding, igual que `Money.Create`).

### Usuarios
- **Usuario 1 — EUR** (`IdUser=1`, `demo@bigschool.com`, `BaseCurrency='EUR'`): hash/salt actuales conservados
  (password `Demo2026!`).
- **Usuario 2 — USD** (`IdUser=2`, `demo.usd@bigschool.com`, `BaseCurrency='USD'`): mismo hash/salt que el
  usuario 1 (password `Demo2026!`) para simplificar el login de demo.

### Transactions
- **Usuario EUR:** ~8-12/mes desde **2022-01** hasta el mes en curso. Cada mes: nómina + 1-2 ingresos extra
  + 6-9 gastos variados (categorías existentes del catálogo `SubCategories`). Todo EUR (`rate=1`,
  `BaseAmount=OriginalAmount`). ~450-650 filas.
- **Usuario USD:** transacciones **ligeras** (~4-6/mes) desde **2026-01-01** hasta el mes en curso. Todo USD
  (`rate=1`). El usuario USD no tiene histórico previo a 2026.

### Companies (18 distintas)
Existentes (1-9) + nuevas (10-18):

| Id | Ticker | Moneda | Mercado | Cartera |
|----|--------|--------|---------|---------|
| 1  | AAPL   | USD | NASDAQ | P1 (EUR) |
| 2  | MSFT   | USD | NASDAQ | P1 (EUR) |
| 3  | SAN    | EUR | BME    | P1 (EUR) |
| 4  | SHEL   | GBP | LSE    | P1 (EUR) |
| 6  | ITX    | EUR | BME    | P1 (EUR) |
| 7  | IBE    | EUR | BME    | P1 (EUR) |
| 10 | BBVA   | EUR | BME    | P1 (EUR) |
| 11 | TEF    | EUR | BME    | P1 (EUR) |
| 5  | NVDA   | USD | NASDAQ | P2 (USD) |
| 8  | AMZN   | USD | NASDAQ | P2 (USD) |
| 9  | TSLA   | USD | NASDAQ | P2 (USD) |
| 12 | GOOGL  | USD | NASDAQ | P2 (USD) |
| 13 | META   | USD | NASDAQ | P2 (USD) |
| 14 | NFLX   | USD | NASDAQ | P2 (USD) |
| 15 | ORCL   | USD | NYSE   | P2 (USD) |
| 16 | SAP    | EUR | XETRA  | P2 (USD) |
| 17 | ASML   | EUR | Euronext | P2 (USD) |
| 18 | ENEL   | EUR | Borsa Italiana | P2 (USD) |

### Portfolios y Holdings
- **P1 — `RealizedPnLCurrency='EUR'`, IdUser=1, creado 2024:** 8 holdings, adquiridos **día-1** de meses
  distintos de 2024. Conversión a EUR: GBP→EUR (SHEL), USD→EUR (AAPL, MSFT); resto `rate=1`.
- **P2 — `RealizedPnLCurrency='USD'`, IdUser=2, creado 2025:** 10 holdings, adquiridos **día-1** de meses
  distintos de 2025. Conversión a USD: EUR→USD (SAP, ASML, ENEL); resto `rate=1`.
- `RealizedPnL=0` inicial. **Sin Disposals.**

### ExchangeRates
- Una fila por **cada fecha de adquisición que cruce moneda** (día-1 de mes):
  - 2024: `USD→EUR` y `GBP→EUR` para las compras de P1 que lo requieran.
  - 2025: `EUR→USD` para las compras de P2 que lo requieran.
- `Source='seed-demo'` (coherente con el patrón actual, distinto de `'seed'`).

### Valuations
- Día-1 de **cada mes posterior a la fecha de adquisición → hasta el mes actual**, por cada una de las 18
  companies. `PriceCurrency` = moneda de la company. Precios con variación realista. Cientos de filas
  generadas por el script (sin solapar el unique `IdCompany+Date` del seed EF, IDs 1-8).

### Criterios de aceptación
- `init.sql` + `seed.sql` cargan sin error en una BD limpia.
- Login con ambos usuarios funciona; el dashboard EUR muestra histórico 2022→hoy; el USD muestra 2026→hoy.
- Cada cartera muestra sus holdings con valoración y P&L no realizado coherente; las conversiones usan los
  rates sembrados (sin llamadas de red).

---

## Bloque 3 — Dockerización completa para k8s local

**Objetivo:** `docker compose up` levanta el stack completo (`bigschool` con `init.sql` + `seed.sql`).

### Componentes
- Activar `backend` y `frontend-web` en `docker-compose.yml` + `docker-compose.override.yml` (build con
  Dockerfiles del Bloque 0). Mapeos de puerto **distintos de los E2E y del IDE**:
  backend `8082:8081`, frontend `3002:3001`.
- `frontend-web` construido con `NEXT_PUBLIC_API_URL=http://localhost:8082/api/v1` (backend del compose
  visto desde el host).
- MySQL arranca `bigschool` con `init.sql` (esquema) + `seed.sql` (Bloque 2).

### Fuera de alcance (plan posterior)
- Manifiestos `k8s/` (namespace, deployments, services, mysql/qdrant PVC, configmaps). Se especificarán
  aparte, ya con imágenes que arrancan.

### Criterios de aceptación
- `docker compose up` deja el stack accesible: frontend `:3002`, backend `:8082`, mysql `:3306`,
  qdrant `:6333`, todos `healthy`. Los puertos `3000`/`5285` (IDE) y `3001`/`8081` (E2E) quedan libres.

---

## Bloque 4 — Scripts de despliegue Azure (gitignored)

**Objetivo:** scripts `az` para crear, en una suscripción Azure personal (~45€/mes), la infraestructura
mínima. **Nunca se ejecutan desde el asistente.**

### Topología (presupuesto ~45€/mes)
- 1 **Resource Group**.
- 1 **App Service Plan Linux B1** (~13€) hospedando **2 Web Apps**:
  - backend (.NET 8) — imagen/contenedor o `dotnet publish`.
  - frontend (Node/Next.js) — contenedor o artefacto Node.
- 1 **Azure Database for MySQL Flexible Server, Burstable Standard_B1ms** (~12-15€) + storage mínimo.
- Total estimado ≈ 28-32€/mes (margen para tráfico/almacenamiento).

### Ubicación y ficheros (`deploy/azure/`, en `.gitignore`)
- `01-provision.ps1` — crea RG, plan, 2 web apps, MySQL Flexible, regla de firewall, app settings y
  connection string (desde variables/entorno, sin secretos en el repo).
- `02-deploy.ps1` — publica imágenes/artefactos a las web apps.
- `99-teardown.ps1` — `az group delete` para no incurrir en coste.
- `README.md` — prerrequisitos (`az login`, suscripción, región), variables, orden de ejecución.

### Restricciones
- Prohibido ejecutar estos scripts desde el asistente; solo los lanza el usuario tras `az login`.
- Ningún secreto Azure en el repo ni en GitHub.

---

## Bloque 5 — CI con GitHub Actions

**Objetivo:** simular un ciclo CI que compile/testee/empaquete front y back. **Sin** capacidad de desplegar
(el deploy vive solo en los scripts locales del Bloque 4).

### `.github/workflows/ci.yml` (versionado, sin secretos Azure)
- Disparadores: `push`/`pull_request` a `master` y `develop`.
- **Job backend:** `dotnet restore/build/test` (incluye los unitarios; los de integración requieren MySQL,
  se documenta cómo se omiten o se levanta un service container si se decide).
- **Job frontend:** `pnpm install`, `pnpm lint`, `pnpm test` (unit), `next build`.
- **Empaquetado:** build de imágenes Docker (Bloque 0) o artefactos `dotnet publish` / `next build` subidos
  como artifacts del workflow.

### Seguridad (resuelve la duda de repo público)
- El workflow **no** contiene credenciales Azure → un fork no puede desplegar nada.
- Los Secrets del repo nunca se exponen a workflows de forks/PRs externos.
- El deploy a Azure es exclusivamente local (Bloque 4), protegido por el `az login` del propietario.

### Criterios de aceptación
- El workflow pasa en verde en `push` a `develop`.
- No referencia ningún secreto de despliegue.

---

## Decomposición de implementación

Un plan por bloque; **Bloque 0 es prerrequisito** de 1, 3, 4 y 5.

1. **Bloque 0** — Dockerfiles backend (`8081`) + frontend (`3001`, `output: standalone`).
2. **Bloque 1** — Compose E2E (`8081`/`3001`) + orquestación Playwright + BD `bigschool_e2e`.
3. **Bloque 2** — Generador + `seed.sql` de dos usuarios.
4. **Bloque 3** — Compose completo (`8082`/`3002`) sobre `bigschool`.
5. **Bloque 4** — Scripts Azure (gitignored).
6. **Bloque 5** — Workflow CI.

## Fuera de alcance

- Manifiestos Kubernetes (plan posterior).
- Despliegue de `rag-service` y `mcp-server` en Azure (esta spec cubre frontend + backend + MySQL).
- CD automatizado desde GitHub Actions.

## Riesgos / cuestiones abiertas

- **Confirmar puerto del Bloque 3**: host `8082` → contenedor `8081` (interpretación de "5000→8082").
- **Tests de integración del backend en CI**: requieren MySQL; decidir en el plan del Bloque 5 si se omiten
  o se usa un service container MySQL.
- **Frontend Next.js en App Service**: validar que el modo standalone/Node arranca bien en App Service Linux
  (alternativa: contenerizar y usar Web App for Containers).
- **Coste real Azure**: vigilar con `az consumption`; el `99-teardown.ps1` mitiga gasto entre demos.
