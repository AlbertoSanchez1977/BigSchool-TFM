# Bloque 0 — Dockerfiles base (backend + frontend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Crear los Dockerfiles multi-stage de backend (.NET 8) y frontend (Next.js 16) que producen imágenes que arrancan y responden, reutilizables por los bloques 1, 3, 4 y 5.

**Architecture:** Dos Dockerfiles en `infra/docker/` con build context en cada proyecto (`src/backend`, `src/frontend-web`). Backend: SDK→aspnet, escucha en `8081`, healthcheck a `/health`. Frontend: Next.js en modo `output: 'standalone'`, escucha en `3001`, con `NEXT_PUBLIC_API_URL` inyectado como build-arg (se inlinea en build). Los puertos `8081`/`3001` no chocan con el debug local (backend `5285`, `next dev` `3000`).

**Tech Stack:** Docker multi-stage, .NET 8 (`mcr.microsoft.com/dotnet/{sdk,aspnet}:8.0`), Node 22 (`node:22-bookworm-slim`) + pnpm via corepack, Next.js 16.2.6 standalone.

**Contexto verificado del repo (no requiere relectura):**
- Backend: no hay `.sln`, hay `Backend.slnx`. Se compila el proyecto `src/BigSchool.WebApi/BigSchool.WebApi.csproj` (referencia a Application + Infrastructure + Domain). Versiones NuGet centralizadas en `src/backend/Directory.Build.props` (necesario para `dotnet restore`). `TargetFramework=net8.0`.
- Backend `Program.cs`: la conexión MySQL es **lazy** (`ServerVersion.AutoDetect` dentro del registro del DbContext, no en el arranque) → el contenedor arranca y responde `/health` **sin** BD disponible. La config JWT **sí** se lee al arrancar (`builder.Configuration.GetSection("Jwt")`), por eso el smoke-test pasa envs `Jwt__*`.
- Backend escucha por `ASPNETCORE_URLS`; lee la conexión de `ConnectionStrings__DefaultConnection`. Healthcheck disponible en `/health`.
- La imagen runtime `aspnet:8.0` (Debian) **no trae `curl`** → se instala en la stage runtime para el `HEALTHCHECK`.
- Frontend: gestor `pnpm` con `pnpm-lock.yaml`. `next` 16.2.6, React 19. `next.config.mjs` actual NO tiene `output: 'standalone'`. El frontend inlinea `process.env.NEXT_PUBLIC_API_URL` en build (`src/lib/api.ts`).

---

## File Structure

| Fichero | Acción | Responsabilidad |
|---------|--------|-----------------|
| `infra/docker/backend.Dockerfile` | Crear | Imagen multi-stage del backend .NET 8 (escucha `8081`) |
| `src/backend/.dockerignore` | Crear | Excluir `bin/`, `obj/`, `logs/`, `tests/` del contexto de build |
| `src/frontend-web/next.config.mjs` | Modificar | Añadir `output: 'standalone'` |
| `infra/docker/frontend-web.Dockerfile` | Crear | Imagen multi-stage del frontend Next.js standalone (escucha `3001`) |
| `src/frontend-web/.dockerignore` | Crear | Excluir `node_modules`, `.next`, reportes y `.env*` del contexto |

> **Nota de "test" en infra:** los Dockerfiles no son unit-testables. El criterio rojo→verde de cada tarea es: **construir la imagen** (debe fallar antes de existir el Dockerfile) y **arrancar el contenedor verificando el endpoint**. Todos los comandos `docker`/`curl` se ejecutan con la herramienta Bash (Git Bash). Se usa `curl --retry` en vez de `sleep` para esperar el arranque.

---

## Task 1: Dockerfile del backend (.NET 8)

**Files:**
- Create: `infra/docker/backend.Dockerfile`
- Create: `src/backend/.dockerignore`

- [x] **Step 1: Verificar que el build falla porque el Dockerfile no existe (test rojo)**

Run:
```bash
docker build -f infra/docker/backend.Dockerfile -t bigschool-backend:dev src/backend
```
Expected: FALLA con un error tipo `failed to read dockerfile: open infra/docker/backend.Dockerfile: no such file or directory`.

- [x] **Step 2: Crear `src/backend/.dockerignore`**

```gitignore
**/bin/
**/obj/
**/logs/
**/.vs/
tests/
*.user
```

- [x] **Step 3: Crear `infra/docker/backend.Dockerfile`**

```dockerfile
# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Props centrales (versiones NuGet) + csproj primero para cachear el restore
COPY Directory.Build.props ./
COPY src/BigSchool.Domain/BigSchool.Domain.csproj                 src/BigSchool.Domain/
COPY src/BigSchool.Application/BigSchool.Application.csproj         src/BigSchool.Application/
COPY src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj  src/BigSchool.Infrastructure/
COPY src/BigSchool.WebApi/BigSchool.WebApi.csproj                  src/BigSchool.WebApi/
RUN dotnet restore src/BigSchool.WebApi/BigSchool.WebApi.csproj

# Resto del código y publish
COPY src/ src/
RUN dotnet publish src/BigSchool.WebApi/BigSchool.WebApi.csproj \
    -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8081
EXPOSE 8081
HEALTHCHECK --interval=10s --timeout=5s --retries=5 --start-period=20s \
    CMD curl -f http://localhost:8081/health || exit 1
ENTRYPOINT ["dotnet", "BigSchool.WebApi.dll"]
```

- [x] **Step 4: Construir la imagen (debe pasar ya)**

Run:
```bash
docker build -f infra/docker/backend.Dockerfile -t bigschool-backend:dev src/backend
```
Expected: termina en `naming to docker.io/library/bigschool-backend:dev done` (build OK).

- [x] **Step 5: Arrancar el contenedor y verificar `/health` (smoke test)**

Run:
```bash
docker run --rm -d --name bs-back-smoke -p 8081:8081 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=bigschool;User=root;Password=irrelevante;" \
  -e Jwt__Secret="YourSuperSecretKeyForDevelopment_AtLeast32Chars!" \
  -e Jwt__Issuer=bigschool-tfm \
  -e Jwt__Audience=bigschool-tfm \
  -e Jwt__ExpirationMinutes=60 \
  bigschool-backend:dev
curl --retry 20 --retry-delay 2 --retry-connrefused -fsS http://localhost:8081/health; echo
docker stop bs-back-smoke
```
Expected: `curl` imprime `Healthy` y devuelve código 0; `docker stop` imprime el nombre del contenedor.

- [x] **Step 6: Commit**

```bash
git add infra/docker/backend.Dockerfile src/backend/.dockerignore
git commit -m "infra: añadir Dockerfile multi-stage del backend (.NET 8, puerto 8081)"
```

---

## Task 2: Activar salida standalone de Next.js

**Files:**
- Modify: `src/frontend-web/next.config.mjs`

- [x] **Step 1: Modificar `src/frontend-web/next.config.mjs`**

Contenido completo del fichero tras el cambio:
```javascript
/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'standalone',
  images: {
    unoptimized: true,
  },
}

export default nextConfig
```

- [x] **Step 2: Verificar que el build emite el server standalone**

Run (desde `src/frontend-web`, con dependencias ya instaladas):
```bash
cd src/frontend-web && pnpm build && ls .next/standalone/server.js && cd -
```
Expected: el build termina sin error y `ls` lista `.next/standalone/server.js` (prueba de que el modo standalone está activo).

- [x] **Step 3: Commit**

```bash
git add src/frontend-web/next.config.mjs
git commit -m "infra: activar output standalone de Next.js para imagen Docker mínima"
```

---

## Task 3: Dockerfile del frontend (Next.js standalone)

**Files:**
- Create: `infra/docker/frontend-web.Dockerfile`
- Create: `src/frontend-web/.dockerignore`

- [x] **Step 1: Verificar que el build falla porque el Dockerfile no existe (test rojo)**

Run:
```bash
docker build -f infra/docker/frontend-web.Dockerfile -t bigschool-frontend:dev src/frontend-web
```
Expected: FALLA con `failed to read dockerfile: ... no such file or directory`.

- [x] **Step 2: Crear `src/frontend-web/.dockerignore`**

```gitignore
node_modules
.next
playwright-report
test-results
.git
.env*
npm-debug.log*
```

- [x] **Step 3: Crear `infra/docker/frontend-web.Dockerfile`**

```dockerfile
# syntax=docker/dockerfile:1

FROM node:22-bookworm-slim AS base
RUN corepack enable && corepack prepare pnpm@9 --activate
WORKDIR /app

# ---- deps ----
FROM base AS deps
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

# ---- build ----
FROM base AS build
ARG NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_TELEMETRY_DISABLED=1
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN pnpm build

# ---- runner ----
FROM base AS runner
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3001
ENV HOSTNAME=0.0.0.0
COPY --from=build /app/public ./public
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
EXPOSE 3001
HEALTHCHECK --interval=10s --timeout=5s --retries=5 --start-period=15s \
    CMD node -e "fetch('http://localhost:3001').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"
CMD ["node", "server.js"]
```

> Si `pnpm install --frozen-lockfile` falla por incompatibilidad de versión de lockfile, fijar la versión exacta de pnpm que generó `pnpm-lock.yaml` en la línea `corepack prepare pnpm@<versión> --activate` (mirar el campo `lockfileVersion` del lock).

- [x] **Step 4: Construir la imagen con el build-arg (debe pasar)**

Run:
```bash
docker build -f infra/docker/frontend-web.Dockerfile \
  --build-arg NEXT_PUBLIC_API_URL=http://localhost:8081/api/v1 \
  -t bigschool-frontend:dev src/frontend-web
```
Expected: build OK (`naming to docker.io/library/bigschool-frontend:dev done`).

- [x] **Step 5: Arrancar el contenedor y verificar la landing (smoke test)**

Run:
```bash
docker run --rm -d --name bs-front-smoke -p 3001:3001 bigschool-frontend:dev
curl --retry 20 --retry-delay 2 --retry-connrefused -fsS -o /dev/null -w "%{http_code}\n" http://localhost:3001
docker stop bs-front-smoke
```
Expected: `curl` imprime `200`; `docker stop` imprime el nombre del contenedor.

- [x] **Step 6: Commit**

```bash
git add infra/docker/frontend-web.Dockerfile src/frontend-web/.dockerignore
git commit -m "infra: añadir Dockerfile multi-stage del frontend (Next.js standalone, puerto 3001)"
```

---

## Self-Review (verificación del autor del plan)

- **Cobertura de la spec (Bloque 0):** backend Dockerfile (Task 1) ✓, frontend Dockerfile (Task 3) ✓, `output: standalone` (Task 2) ✓, EXPOSE 8081/3001 ✓, `NEXT_PUBLIC_API_URL` como build-arg ✓, healthcheck `/health` ✓. Criterio de aceptación de la spec ("imágenes que arrancan; backend 200 en /health :8081; frontend landing :3001") cubierto por los smoke-tests de Task 1 Step 5 y Task 3 Step 5.
- **Sin placeholders:** todos los pasos contienen el contenido real (Dockerfiles completos, comandos exactos, salidas esperadas).
- **Consistencia de nombres/puertos:** backend `8081` y frontend `3001` coherentes en EXPOSE, ENV, healthcheck y smoke-tests en todo el plan; rutas de csproj coinciden con la estructura real (`src/BigSchool.WebApi/BigSchool.WebApi.csproj`, etc.).

## Notas para bloques siguientes
- Estas imágenes (`bigschool-backend:dev`, `bigschool-frontend:dev`) y sus Dockerfiles son la base de: Bloque 1 (compose E2E, puertos `8081:8081`/`3001:3001`), Bloque 3 (compose completo, `8082:8081`/`3002:3001`), Bloques 4/5 (deploy/CI).
- El frontend inyecta `NEXT_PUBLIC_API_URL` **en build**: cada stack debe construir su propia imagen de frontend con la URL del backend correspondiente.
