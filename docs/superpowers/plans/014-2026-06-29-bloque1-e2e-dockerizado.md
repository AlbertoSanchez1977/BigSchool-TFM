# Bloque 1 — E2E dockerizado y aislado Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que `pnpm test:e2e` levante un entorno efímero (frontend + backend dockerizados + BD `bigschool_e2e` solo-estructura), ejecute los specs Playwright contra el frontend dockerizado (`:3001`) y limpie todo al terminar, sin tocar `bigschool` ni `bigschool_test`; conservando un modo de depuración local con `pnpm test:e2e:local`.

**Architecture:** Dos configuraciones de Playwright (decisión "Opción B"). La config **por defecto** (`playwright.config.ts`) es la **dockerizada**: `globalSetup` crea/puebla `bigschool_e2e` (aplicando `init.sql`) y levanta el compose e2e; `globalTeardown` baja el compose y dropea la BD; `baseURL` → `http://localhost:3001`. La config **local** (`playwright.local.config.ts`) conserva el comportamiento actual (`webServer: pnpm dev`, `:3000`, backend en IDE `:5285`) para iteración con breakpoints. El backend e2e alcanza el MySQL dev por `host.docker.internal:3306` (sin compartir red entre composes).

**Tech Stack:** Playwright (configs TS, `globalSetup`/`globalTeardown`), Docker Compose, los Dockerfiles del Bloque 0 (`infra/docker/backend.Dockerfile` puerto 8081, `infra/docker/frontend-web.Dockerfile` puerto 3001), MySQL 8 (contenedor dev `bigschool-mysql`).

**Prerrequisitos (ya en `develop`):**
- Bloque 0 mergeado: existen `infra/docker/backend.Dockerfile` (EXPOSE 8081) y `infra/docker/frontend-web.Dockerfile` (EXPOSE 3001, build-arg `NEXT_PUBLIC_API_URL`).
- El MySQL dev se levanta con `cd infra && docker compose --env-file .env up -d mysql && cd -` (contenedor `bigschool-mysql`, puerto host `3306`, credenciales en `infra/.env`). **IMPORTANTE:** especificar `-f infra/docker-compose.yml` sin el override recrea el contenedor sin el port binding `3306:3306`; siempre arrancar desde `infra/` o pasando ambos `-f`.

**Contexto verificado del repo (no requiere relectura):**
- `infra/docker/mysql/init.sql` (318 líneas): crea esquema + `__EFMigrationsHistory` + catálogo base EF (`SubCategories` 1-28 línea 70, `Companies` 1-4 línea 187, `Valuations` 1-8 línea 214). Única sentencia `USE \`bigschool\`;` en la línea 9. **NO** incluye datos demo (eso es `seed.sql`). Este catálogo base es justo lo que necesitan los specs (`create-expense` usa SubCategories; `sell-holding` selecciona "primera empresa" y su valoración).
- Los 3 specs E2E (`login`, `create-expense`, `sell-holding`) **registran usuarios con email único** por run → no dependen de datos sembrados.
- `infra/.env` contiene `MYSQL_ROOT_PASSWORD`, `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRATION_MINUTES`.
- El backend no migra en arranque; la conexión MySQL es lazy → arranca y responde `/health` aunque la BD aún no tenga datos.
- El frontend inlinea `NEXT_PUBLIC_API_URL` en build → la imagen e2e se construye con `http://localhost:8081/api/v1` (el navegador de Playwright, en el host, llama al backend publicado en `:8081`).

---

## File Structure

| Fichero | Acción | Responsabilidad |
|---------|--------|-----------------|
| `infra/docker-compose.e2e.yml` | Crear | Servicios `backend` (8081) + `frontend-web` (3001) e2e; backend → MySQL dev vía `host.docker.internal` |
| `src/frontend-web/tests/e2e/global-setup.ts` | Crear | (Re)crear `bigschool_e2e` + aplicar `init.sql`; `compose up --build`; esperar healthchecks |
| `src/frontend-web/tests/e2e/global-teardown.ts` | Crear | `compose down`; `DROP DATABASE bigschool_e2e` |
| `src/frontend-web/playwright.config.ts` | Modificar | Config dockerizada por defecto: sin `webServer`, `baseURL :3001`, `globalSetup`/`globalTeardown` |
| `src/frontend-web/playwright.local.config.ts` | Crear | Config de debug local: `webServer: pnpm dev`, `baseURL :3000` |
| `src/frontend-web/package.json` | Modificar | Scripts `test:e2e` (docker) y `test:e2e:local` (local) |

> **Nota de "test" en infra/E2E:** el criterio rojo→verde de cada tarea es ejecutar comandos reales (`docker compose config`, arranque + `curl`, y finalmente `pnpm test:e2e`). Todos los comandos se ejecutan con la herramienta Bash (Git Bash). Se usa `curl --retry` en vez de `sleep`. **Requiere Docker Desktop en marcha** y el MySQL dev levantado.

---

## Task 1: Compose efímero de E2E

**Files:**
- Create: `infra/docker-compose.e2e.yml`

- [x] **Step 1: Verificar que el compose aún no existe (test rojo)**

Run:
```bash
docker compose --env-file infra/.env -f infra/docker-compose.e2e.yml config >/dev/null
```
Expected: FALLA con `no such file or directory` (el fichero no existe todavía).

- [x] **Step 2: Crear `infra/docker-compose.e2e.yml`**

```yaml
# Entorno EFÍMERO para los E2E de Playwright (Bloque 1).
# Levanta SOLO frontend + backend dockerizados; el MySQL es el contenedor dev
# existente (bigschool-mysql), alcanzado por host.docker.internal:3306.
# Lo orquestan global-setup.ts / global-teardown.ts de Playwright.
services:
  backend:
    build:
      context: ../src/backend
      dockerfile: ../../infra/docker/backend.Dockerfile
    container_name: bigschool-e2e-backend
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Server=host.docker.internal;Port=3306;Database=bigschool_e2e;User=root;Password=${MYSQL_ROOT_PASSWORD};"
      Jwt__Secret: ${JWT_SECRET}
      Jwt__Issuer: ${JWT_ISSUER}
      Jwt__Audience: ${JWT_AUDIENCE}
      Jwt__ExpirationMinutes: ${JWT_EXPIRATION_MINUTES}
    ports:
      - "8081:8081"
    extra_hosts:
      - "host.docker.internal:host-gateway"

  frontend-web:
    build:
      context: ../src/frontend-web
      dockerfile: ../../infra/docker/frontend-web.Dockerfile
      args:
        NEXT_PUBLIC_API_URL: "http://localhost:8081/api/v1"
    container_name: bigschool-e2e-frontend
    ports:
      - "3001:3001"
    depends_on:
      - backend
```

- [x] **Step 3: Validar el compose (test verde de sintaxis)**

Run:
```bash
docker compose --env-file infra/.env -f infra/docker-compose.e2e.yml config >/dev/null && echo "compose OK"
```
Expected: imprime `compose OK` (YAML válido y variables resueltas desde `infra/.env`).

- [x] **Step 4: Smoke manual de arranque (requiere MySQL dev arriba)**

Run:
```bash
cd infra && docker compose --env-file .env up -d mysql && cd -
# crear la BD e2e mínima para que el backend conecte (esquema completo lo hará global-setup)
docker exec -i -e MYSQL_PWD="$(grep '^MYSQL_ROOT_PASSWORD=' infra/.env | cut -d= -f2-)" bigschool-mysql \
  mysql -uroot -e "CREATE DATABASE IF NOT EXISTS \`bigschool_e2e\`;"
docker compose --env-file infra/.env -f infra/docker-compose.e2e.yml up -d --build
curl --retry 30 --retry-delay 2 --retry-connrefused -fsS http://localhost:8081/health; echo
curl --retry 30 --retry-delay 2 --retry-connrefused -fsS -o /dev/null -w "%{http_code}\n" http://localhost:3001
docker compose --env-file infra/.env -f infra/docker-compose.e2e.yml down
docker exec -i -e MYSQL_PWD="$(grep '^MYSQL_ROOT_PASSWORD=' infra/.env | cut -d= -f2-)" bigschool-mysql \
  mysql -uroot -e "DROP DATABASE IF EXISTS \`bigschool_e2e\`;"
```
Expected: el primer `curl` imprime `Healthy`; el segundo imprime `200`; `down` elimina los contenedores `bigschool-e2e-*`.

- [x] **Step 5: Commit**

```bash
git add infra/docker-compose.e2e.yml
git commit -m "infra: añadir docker-compose efímero para E2E (frontend+backend, bigschool_e2e)"
```

---

## Task 2: globalSetup y globalTeardown de Playwright

**Files:**
- Create: `src/frontend-web/tests/e2e/global-setup.ts`
- Create: `src/frontend-web/tests/e2e/global-teardown.ts`

- [x] **Step 1: Crear `src/frontend-web/tests/e2e/global-setup.ts`**

```typescript
import { execSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import path from 'node:path'

// tests/e2e → ../../../.. = raíz del repo
const REPO_ROOT = path.resolve(__dirname, '../../../..')
const COMPOSE_FILE = path.join(REPO_ROOT, 'infra/docker-compose.e2e.yml')
const ENV_FILE = path.join(REPO_ROOT, 'infra/.env')
const INIT_SQL = path.join(REPO_ROOT, 'infra/docker/mysql/init.sql')
const MYSQL_CONTAINER = 'bigschool-mysql'

function readEnvValue(key: string): string {
  const line = readFileSync(ENV_FILE, 'utf8')
    .split(/\r?\n/)
    .find((l) => l.startsWith(`${key}=`))
  if (!line) throw new Error(`No se encontró ${key} en ${ENV_FILE}`)
  return line.slice(key.length + 1).trim()
}

async function waitForHttp(url: string, attempts = 40): Promise<void> {
  for (let i = 0; i < attempts; i++) {
    try {
      const res = await fetch(url)
      if (res.ok) return
    } catch {
      /* aún no responde */
    }
    await new Promise((r) => setTimeout(r, 2000))
  }
  throw new Error(`Timeout esperando ${url}`)
}

export default async function globalSetup(): Promise<void> {
  const rootPass = process.env.MYSQL_ROOT_PASSWORD ?? readEnvValue('MYSQL_ROOT_PASSWORD')

  // 1. El MySQL dev debe estar en marcha
  try {
    execSync(`docker inspect -f "{{.State.Running}}" ${MYSQL_CONTAINER}`, { stdio: 'pipe' })
  } catch {
    throw new Error(
      `El contenedor ${MYSQL_CONTAINER} no está en marcha.\n` +
        'Ejecuta antes: cd infra && docker compose --env-file .env up -d mysql && cd -',
    )
  }

  // 2. (Re)crear bigschool_e2e y aplicar el esquema base (init.sql con USE reescrito).
  //    DROP+CREATE garantiza idempotencia aunque un run previo dejara restos.
  const schemaSql =
    'DROP DATABASE IF EXISTS `bigschool_e2e`;\n' +
    'CREATE DATABASE `bigschool_e2e` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;\n' +
    readFileSync(INIT_SQL, 'utf8').replace(/USE `bigschool`/g, 'USE `bigschool_e2e`')

  execSync(`docker exec -i -e MYSQL_PWD ${MYSQL_CONTAINER} mysql -uroot`, {
    input: schemaSql,
    env: { ...process.env, MYSQL_PWD: rootPass },
    stdio: ['pipe', 'inherit', 'inherit'],
  })

  // 3. Levantar front + back dockerizados.
  execSync(`docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" up -d --build`, {
    stdio: 'inherit',
    env: { ...process.env, MYSQL_ROOT_PASSWORD: rootPass },
  })

  // 4. Esperar a que ambos respondan.
  await waitForHttp('http://localhost:8081/health')
  await waitForHttp('http://localhost:3001')
}
```

- [x] **Step 2: Crear `src/frontend-web/tests/e2e/global-teardown.ts`**

```typescript
import { execSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import path from 'node:path'

const REPO_ROOT = path.resolve(__dirname, '../../../..')
const COMPOSE_FILE = path.join(REPO_ROOT, 'infra/docker-compose.e2e.yml')
const ENV_FILE = path.join(REPO_ROOT, 'infra/.env')
const MYSQL_CONTAINER = 'bigschool-mysql'

function readEnvValue(key: string): string {
  const line = readFileSync(ENV_FILE, 'utf8')
    .split(/\r?\n/)
    .find((l) => l.startsWith(`${key}=`))
  if (!line) throw new Error(`No se encontró ${key} en ${ENV_FILE}`)
  return line.slice(key.length + 1).trim()
}

export default async function globalTeardown(): Promise<void> {
  const rootPass = process.env.MYSQL_ROOT_PASSWORD ?? readEnvValue('MYSQL_ROOT_PASSWORD')

  // 1. Bajar front + back (elimina contenedores bigschool-e2e-*).
  execSync(`docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" down --remove-orphans`, {
    stdio: 'inherit',
  })

  // 2. Eliminar la BD efímera.
  execSync(`docker exec -i -e MYSQL_PWD ${MYSQL_CONTAINER} mysql -uroot`, {
    input: 'DROP DATABASE IF EXISTS `bigschool_e2e`;',
    env: { ...process.env, MYSQL_PWD: rootPass },
    stdio: ['pipe', 'inherit', 'inherit'],
  })
}
```

- [x] **Step 3: Verificación de compilación TypeScript de los scripts**

Run:
```bash
cd src/frontend-web && pnpm typecheck; cd -
```
Expected: sin errores de tipos (salida vacía / código 0). Nota: `tsc` con ficheros explícitos ignora el `tsconfig.json`; usar `pnpm typecheck` que ejecuta `tsc --noEmit` con el proyecto completo.

- [x] **Step 4: Commit**

```bash
git add src/frontend-web/tests/e2e/global-setup.ts src/frontend-web/tests/e2e/global-teardown.ts
git commit -m "test(e2e): añadir globalSetup/globalTeardown que orquestan compose e2e + bigschool_e2e"
```

---

## Task 3: Dos configs de Playwright + scripts

**Files:**
- Modify: `src/frontend-web/playwright.config.ts`
- Create: `src/frontend-web/playwright.local.config.ts`
- Modify: `src/frontend-web/package.json`

- [x] **Step 1: Reescribir `src/frontend-web/playwright.config.ts` (config dockerizada por defecto)**

Contenido completo del fichero:
```typescript
import { defineConfig, devices } from '@playwright/test'
import path from 'node:path'

// Config E2E POR DEFECTO: entorno dockerizado y aislado (CI / runs reproducibles).
// globalSetup levanta frontend+backend en contenedores y la BD bigschool_e2e;
// globalTeardown lo limpia todo. Para depurar en local usa playwright.local.config.ts.
export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: 'html',
  globalSetup: path.resolve(__dirname, './tests/e2e/global-setup.ts'),
  globalTeardown: path.resolve(__dirname, './tests/e2e/global-teardown.ts'),
  use: {
    baseURL: 'http://localhost:3001',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
```

- [x] **Step 2: Crear `src/frontend-web/playwright.local.config.ts` (debug local)**

Contenido completo del fichero:
```typescript
import { defineConfig, devices } from '@playwright/test'

// Config E2E de DEBUG LOCAL: frontend con `pnpm dev` (:3000) y backend ejecutado a mano
// desde el IDE en :5285 (NEXT_PUBLIC_API_URL=http://localhost:5285/api/v1).
// No dockeriza nada ni toca bigschool_e2e: pensada para iteración rápida con breakpoints.
export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
  },
})
```

- [x] **Step 3: Añadir el script `test:e2e:local` en `src/frontend-web/package.json`**

Reemplazar la línea:
```json
    "test:e2e": "playwright test",
```
por:
```json
    "test:e2e": "playwright test",
    "test:e2e:local": "playwright test --config playwright.local.config.ts",
```

- [x] **Step 4: Verificar que Playwright resuelve ambas configs**

Run:
```bash
cd src/frontend-web && npx playwright test --list >/dev/null && npx playwright test --config playwright.local.config.ts --list >/dev/null && echo "configs OK"; cd -
```
Expected: imprime `configs OK` (ambas configs cargan y listan los 3 specs sin error de parseo). `--list` no ejecuta `globalSetup`, así que no arranca Docker en este paso.

- [x] **Step 5: Commit**

```bash
git add src/frontend-web/playwright.config.ts src/frontend-web/playwright.local.config.ts src/frontend-web/package.json
git commit -m "test(e2e): dos configs Playwright (docker por defecto + local debug) y scripts"
```

---

## Task 4: Verificación integral del flujo dockerizado

**Files:** (ninguno nuevo — ejecución end-to-end)

- [ ] **Step 1: Asegurar el MySQL dev levantado**

Run:
```bash
cd infra && docker compose --env-file .env up -d mysql && cd -
curl --retry 30 --retry-delay 2 --retry-connrefused -fsS -o /dev/null http://localhost:6333 2>/dev/null || true
docker inspect -f "{{.State.Running}}" bigschool-mysql
```
Expected: el último comando imprime `true`.

- [ ] **Step 2: Ejecutar la suite E2E dockerizada completa**

Run:
```bash
cd src/frontend-web && pnpm test:e2e; cd -
```
Expected: `globalSetup` construye/levanta los contenedores y crea `bigschool_e2e`; los 3 specs (`login`, `create-expense`, `sell-holding`) terminan en **PASS**; `globalTeardown` baja el compose y dropea la BD.

- [ ] **Step 3: Verificar que no quedan restos**

Run:
```bash
docker ps -a --filter "name=bigschool-e2e" --format "{{.Names}}"
docker exec -i -e MYSQL_PWD="$(grep '^MYSQL_ROOT_PASSWORD=' infra/.env | cut -d= -f2-)" bigschool-mysql \
  mysql -uroot -e "SHOW DATABASES LIKE 'bigschool_e2e';"
```
Expected: el primer comando no imprime ningún contenedor; el segundo no lista `bigschool_e2e` (BD eliminada). `bigschool` y `bigschool_test` quedan intactas.

- [ ] **Step 4: (Opcional) Confirmar que el modo local sigue disponible**

Este paso es manual y NO se ejecuta en CI: requiere el backend corriendo en el IDE (`:5285`) y `NEXT_PUBLIC_API_URL=http://localhost:5285/api/v1`. Comando: `pnpm test:e2e:local`. Sirve como recordatorio de que el loop de depuración con breakpoints se conserva.

- [ ] **Step 5: Commit (cierre del bloque, si procede algún ajuste)**

Si los pasos anteriores requirieron retoques en cualquier fichero, commitéalos:
```bash
git add -A
git commit -m "test(e2e): ajustes tras verificación integral del flujo dockerizado"
```
Si no hubo cambios, omitir este commit.

---

## Self-Review (verificación del autor del plan)

- **Cobertura de la spec (Bloque 1):** compose efímero front+back (Task 1) ✓; `bigschool_e2e` solo-estructura vía `init.sql` con `USE` reescrito, sin seed (Task 2, globalSetup) ✓; orquestación Playwright `globalSetup`/`globalTeardown` (Tasks 2-3) ✓; `baseURL :3001` y eliminación de `webServer` en la config por defecto (Task 3) ✓; limpieza final (compose down + DROP BD) verificada (Task 4 Step 3) ✓; no se tocan `bigschool` ni `bigschool_test` ✓. **Decisión Opción B** (dos configs) materializada en Task 3 con `test:e2e` (docker) y `test:e2e:local` (debug).
- **Sin placeholders:** todos los ficheros se dan completos; comandos y salidas esperadas son concretos.
- **Consistencia de nombres/puertos:** backend `8081` y frontend `3001` coherentes en compose, configs y verificaciones; contenedor MySQL `bigschool-mysql`; BD `bigschool_e2e`; ruta `init.sql` y única sentencia `USE \`bigschool\`` (línea 9) coinciden con el repo; `NEXT_PUBLIC_API_URL=http://localhost:8081/api/v1` coherente con el puerto del backend e2e.
- **Riesgo conocido:** `host.docker.internal` requiere Docker Desktop (Windows/Mac) o el `extra_hosts: host-gateway` añadido (Linux/CI) — ya incluido en el compose. Si `pnpm install --frozen-lockfile` del Dockerfile de frontend fallara por versión de pnpm, ver la nota del plan del Bloque 0.

## Notas para bloques siguientes
- El Bloque 5 (CI) podrá reutilizar `pnpm test:e2e` como job de E2E, levantando antes el MySQL dev como service/contenedor.
- El `host.docker.internal` y el patrón DROP+CREATE de `bigschool_e2e` son reutilizables si se quisiera paralelizar runs con BDs por-worker (no necesario ahora, YAGNI).
