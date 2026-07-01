# Bloque 3 — Dockerización completa para k8s local Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que `docker compose up` levante el stack completo de la app (`mysql` con `bigschool` = `init.sql` + `seed.sql`, `backend` y `frontend-web` dockerizados) con imágenes que arrancan y se hablan, dejando la base lista para un despliegue Kubernetes local posterior.

**Architecture:** Se activan los servicios `backend` y `frontend-web` (hoy comentados) en `docker-compose.yml` (build con los Dockerfiles del Bloque 0) y `docker-compose.override.yml` (runtime). El backend habla con `mysql` por la red interna `bigschool-network` (`Server=mysql`); el frontend se construye con `NEXT_PUBLIC_API_URL` apuntando al backend publicado en el host. Puertos elegidos para **no chocar** con el IDE (`5285`/`3000`) ni con el compose E2E (`8081`/`3001`): backend `8082:8081`, frontend `3002:3001`.

**Tech Stack:** Docker Compose, Dockerfiles del Bloque 0 (`infra/docker/backend.Dockerfile` listen `8081`, `infra/docker/frontend-web.Dockerfile` listen `3001`), MySQL 8 con `init.sql` + `seed.sql`.

**Prerrequisitos (ya en `develop`):**
- Bloque 0: `infra/docker/backend.Dockerfile` (EXPOSE 8081, `ASPNETCORE_URLS=http://+:8081`, HEALTHCHECK a `/health`) y `infra/docker/frontend-web.Dockerfile` (EXPOSE 3001, build-arg `NEXT_PUBLIC_API_URL`, HEALTHCHECK).
- `infra/docker/mysql/init.sql` (esquema + catálogo) y `infra/docker/mysql/seed.sql` (datos demo; el contenido rico llega al ejecutar el Bloque 2, pero este bloque funciona con cualquier `seed.sql` válido presente).
- `infra/.env` con `MYSQL_ROOT_PASSWORD, MYSQL_DATABASE (bigschool), MYSQL_USER, MYSQL_PASSWORD, JWT_SECRET, JWT_ISSUER, JWT_AUDIENCE, JWT_EXPIRATION_MINUTES`.

**Contexto verificado del repo (no requiere relectura):**
- `docker-compose.override.yml` ya monta `./docker/mysql/init.sql` → `01-init.sql` y `./docker/mysql/seed.sql` → `02-seed.sql`, y crea `bigschool` vía `MYSQL_DATABASE`. El `entrypoint` de MySQL ejecuta esos scripts **solo en la primera inicialización** (volumen vacío).
- **El backend lee `ConnectionStrings__DefaultConnection`** (verificado en `Program.cs`). El bloque de ejemplo comentado usa por error `ConnectionStrings__MySQL`; el plan usa la clave correcta.
- El backend no migra en arranque y la conexión es lazy → arranca y responde `/health` enseguida; al primer request real conecta a `bigschool`.
- Endpoint sin auth que escribe en BD (para verificación del cableado): `POST /api/v1/auth/register` con body JSON `{ "email", "password", "fullName" }` → `200` con envelope `{ data: { ...token } }`.
- `bigschool_user` (`MYSQL_USER`) tiene todos los privilegios sobre `bigschool` (`MYSQL_DATABASE`) → suficiente para el backend (no crea BDs en runtime).

---

## File Structure

| Fichero | Acción | Responsabilidad |
|---------|--------|-----------------|
| `infra/docker-compose.yml` | Modificar | Activar `build` de `backend` y `frontend-web` (este último con build-arg `NEXT_PUBLIC_API_URL`) |
| `infra/docker-compose.override.yml` | Modificar | Activar runtime de `backend` (env conexión + JWT, `8082:8081`, depends_on mysql) y `frontend-web` (`3002:3001`, depends_on backend) |

> **Nota de "test":** el criterio rojo→verde es ejecutar el stack real (`docker compose up --build`) y comprobar healthchecks + un round-trip de API que toca `bigschool`. Comandos con la herramienta Bash (Git Bash). **Requiere Docker Desktop.**

---

## Task 1: Activar `backend` y `frontend-web` en los compose

**Files:**
- Modify: `infra/docker-compose.yml`
- Modify: `infra/docker-compose.override.yml`

- [x] **Step 1: En `infra/docker-compose.yml`, sustituir el bloque comentado de `backend` por:**

```yaml
  backend:
    build:
      context: ../src/backend
      dockerfile: ../../infra/docker/backend.Dockerfile
    container_name: bigschool-backend
```

- [x] **Step 2: En `infra/docker-compose.yml`, sustituir el bloque comentado de `frontend-web` por:**

```yaml
  frontend-web:
    build:
      context: ../src/frontend-web
      dockerfile: ../../infra/docker/frontend-web.Dockerfile
      args:
        NEXT_PUBLIC_API_URL: "http://localhost:8082/api/v1"
    container_name: bigschool-frontend
```

(Los servicios `rag-service` y `mcp-server` siguen comentados: fuera de alcance.)

- [x] **Step 3: En `infra/docker-compose.override.yml`, sustituir el bloque comentado de `backend` por:**

```yaml
  backend:
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Server=mysql;Port=3306;Database=${MYSQL_DATABASE};User=${MYSQL_USER};Password=${MYSQL_PASSWORD};"
      Jwt__Secret: ${JWT_SECRET}
      Jwt__Issuer: ${JWT_ISSUER}
      Jwt__Audience: ${JWT_AUDIENCE}
      Jwt__ExpirationMinutes: ${JWT_EXPIRATION_MINUTES}
    ports:
      - "8082:8081"
    depends_on:
      mysql:
        condition: service_healthy
    networks:
      - bigschool-network
```

- [x] **Step 4: En `infra/docker-compose.override.yml`, sustituir el bloque comentado de `frontend-web` por:**

```yaml
  frontend-web:
    restart: unless-stopped
    ports:
      - "3002:3001"
    depends_on:
      backend:
        condition: service_healthy
    networks:
      - bigschool-network
```

- [x] **Step 5: Validar la configuración fusionada (test verde de sintaxis)**

Run:
```bash
cd infra && docker compose --env-file .env config | grep -E "ConnectionStrings__DefaultConnection|8082|3002|NEXT_PUBLIC_API_URL|container_name" ; cd -
```
Expected: la salida muestra `container_name: bigschool-backend` y `bigschool-frontend`, el mapeo `8082` (target 8081) y `3002` (target 3001), la `ConnectionStrings__DefaultConnection` con `Server=mysql`, y el build-arg `NEXT_PUBLIC_API_URL: http://localhost:8082/api/v1` — todo resuelto desde `infra/.env` sin errores.

- [x] **Step 6: Commit**

```bash
git add infra/docker-compose.yml infra/docker-compose.override.yml
git commit -m "infra: activar backend y frontend-web en docker-compose (stack completo, 8082/3002)"
```

---

## Task 2: Verificación del stack completo

**Files:** (ninguno — verificación end-to-end)

- [x] **Step 1: Arrancar el stack desde cero (volumen limpio para re-ejecutar init.sql+seed.sql)**

> `down -v` elimina los volúmenes `mysql_data`/`qdrant_data` (datos locales de `bigschool`): es lo deseado para validar la inicialización completa con `init.sql` + `seed.sql`.

Run:
```bash
cd infra
docker compose --env-file .env down -v
docker compose --env-file .env up -d --build
cd -
```
Expected: build de `backend` y `frontend-web` OK; los contenedores `bigschool-mysql`, `bigschool-backend`, `bigschool-frontend` (y `bigschool-qdrant`) quedan creados.

- [x] **Step 2: Esperar a backend y frontend**

Run:
```bash
curl --retry 40 --retry-delay 3 --retry-connrefused -fsS http://localhost:8082/health; echo
curl --retry 40 --retry-delay 3 --retry-connrefused -fsS -o /dev/null -w "%{http_code}\n" http://localhost:3002
```
Expected: el primero imprime `Healthy`; el segundo imprime `200`.

- [x] **Step 3: Estado de los servicios**

Run:
```bash
cd infra && docker compose --env-file .env ps; cd -
```
Expected: `mysql` y `backend` en estado `healthy`; `frontend-web` y `qdrant` `running`/`healthy`. Ningún servicio en `restarting`/`exited`.

- [x] **Step 4: Round-trip de API que toca `bigschool` (cableado backend↔mysql)**

Run:
```bash
curl -fsS -X POST http://localhost:8082/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"smoke+$(date +%s)@bigschool.com\",\"password\":\"Smoke2026!\",\"fullName\":\"Smoke Test\"}"; echo
```
Expected: respuesta JSON con envelope `{"data":{...}}` incluyendo un token (status 200). Esto demuestra que el backend persiste en `bigschool` a través de la red interna.

- [x] **Step 5: (Opcional) Comprobar el seed demo si el Bloque 2 ya se ejecutó**

Run:
```bash
curl -fsS -X POST http://localhost:8082/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"demo@bigschool.com","password":"Demo2026!"}'; echo
```
Expected: si `seed.sql` ya contiene el usuario demo (Bloque 2 ejecutado), devuelve `200` con token. Si aún es el seed antiguo o no existe el usuario, devolverá `400` — informativo, no bloquea este bloque (cuyo objetivo es el stack, no el contenido del seed).

- [x] **Step 6: Dejar constancia y, si procede, parar el stack**

Run (opcional, para liberar recursos tras verificar):
```bash
cd infra && docker compose --env-file .env down; cd -
```
Expected: contenedores detenidos y eliminados (sin `-v`, conservando el volumen para el siguiente arranque).

- [x] **Step 7: Commit (solo si la verificación obligó a retocar los compose)**

Si algún expected falló y corregiste los compose, commitéalo:
```bash
git add infra/docker-compose.yml infra/docker-compose.override.yml
git commit -m "infra: ajustes en docker-compose tras verificación del stack completo"
```
Si todo pasó a la primera, omitir este commit.

---

## Self-Review (verificación del autor del plan)

- **Cobertura de la spec (Bloque 3):** activar `backend` + `frontend-web` con los Dockerfiles del Bloque 0 (Task 1) ✓; MySQL levanta `bigschool` con `init.sql` + `seed.sql` (ya montados en el override) ✓; frontend construido con `NEXT_PUBLIC_API_URL` al backend (Task 1 Step 2) ✓; puertos `8082`/`3002` sin chocar con IDE (`5285`/`3000`) ni E2E (`8081`/`3001`) ✓; manifiestos k8s fuera de alcance (diferidos, como dice el spec) ✓. Criterio de aceptación ("frontend :3002, backend :8082, mysql :3306, qdrant :6333 healthy") cubierto por Task 2 Steps 2-4.
- **Sin placeholders:** los bloques YAML se dan completos; comandos y salidas esperadas concretos; payload de `register` con la forma real (`email/password/fullName`).
- **Consistencia de nombres/puertos:** backend listen `8081` → host `8082`; frontend listen `3001` → host `3002`; `ConnectionStrings__DefaultConnection` (clave correcta, no `__MySQL`); `Server=mysql` (nombre de servicio en `bigschool-network`); build-arg `NEXT_PUBLIC_API_URL=http://localhost:8082/api/v1` coherente con el puerto host del backend.
- **Riesgo conocido:** `down -v` borra el volumen de `bigschool` (intencionado para validar init+seed). El seed mostrado en el Step 5 depende de que el Bloque 2 se haya ejecutado; por eso esa comprobación es opcional y no condiciona el bloque.

## Notas para bloques siguientes
- El Bloque 4 (Azure) reutiliza estas mismas imágenes (backend/frontend) y la cadena de conexión equivalente apuntando al MySQL Flexible.
- El despliegue Kubernetes local (plan posterior) parte de estas imágenes ya verificadas; los manifiestos `k8s/` traducirán este compose (deployments/services + initContainer o Job para `init.sql`+`seed.sql`, y un Secret para credenciales).
