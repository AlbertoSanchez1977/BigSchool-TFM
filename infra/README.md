# Infra — Docker Compose

Todos los comandos se ejecutan desde el directorio `infra/`.  
Requiere Docker Desktop en marcha y el fichero `infra/.env` con las variables de entorno.

Docker Compose fusiona automáticamente `docker-compose.yml` y `docker-compose.override.yml`
cuando ambos están en el mismo directorio — no es necesario especificarlos con `-f`.

Los scripts `init.sql` y `seed.sql` solo se ejecutan en el **primer arranque con volumen vacío**.
En arranques sucesivos MySQL usa los datos ya persistidos en `mysql_data`.

---

## Stack completo

**Primera vez o reinicio limpio** — construye imágenes y ejecuta `init.sql` + `seed.sql`:

```bash
docker compose --env-file .env down -v
docker compose --env-file .env up -d --build
```

**Arranques sucesivos** — volumen existente, no recarga scripts:

```bash
docker compose --env-file .env up -d
```

URLs una vez arriba:

| Servicio       | URL                          |
|----------------|------------------------------|
| Frontend       | http://localhost:3002        |
| Backend API    | http://localhost:8082/api/v1 |
| Backend health | http://localhost:8082/health |
| MySQL          | localhost:3306               |
| Qdrant REST    | http://localhost:6333        |

---

## Solo MySQL y/o Qdrant

### MySQL con `init.sql` + `seed.sql` (vol limpio — esquema + datos demo)

```bash
docker compose --env-file .env down -v mysql
docker compose --env-file .env up -d mysql
```

### MySQL con solo `init.sql` (vol limpio — esquema sin datos demo)

Comentar temporalmente la línea del seed en `docker-compose.override.yml`:

```yaml
# - ./docker/mysql/seed.sql:/docker-entrypoint-initdb.d/02-seed.sql
```

Luego:

```bash
docker compose --env-file .env down -v mysql
docker compose --env-file .env up -d mysql
```

Restaurar la línea una vez arrancado.

### MySQL con vol existente (no recarga scripts)

```bash
docker compose --env-file .env up -d mysql
```

### Solo Qdrant

```bash
docker compose --env-file .env up -d qdrant
```

### MySQL + Qdrant (sin backend ni frontend)

Con vol limpio (ejecuta `init.sql` + `seed.sql`):

```bash
docker compose --env-file .env down -v mysql qdrant
docker compose --env-file .env up -d mysql qdrant
```

Con vol existente:

```bash
docker compose --env-file .env up -d mysql qdrant
```

---

## Destruir el entorno

### Parar todos los servicios — conservar datos

```bash
docker compose --env-file .env down
```

### Parar y borrar volúmenes — DESTRUYE todos los datos

```bash
docker compose --env-file .env down -v
```

### Parar y eliminar un contenedor específico

```bash
# Backend
docker compose --env-file .env stop backend && docker compose --env-file .env rm -f backend

# Frontend
docker compose --env-file .env stop frontend-web && docker compose --env-file .env rm -f frontend-web

# MySQL — conserva el volumen (los datos persisten en el siguiente up)
docker compose --env-file .env stop mysql && docker compose --env-file .env rm -f mysql

# MySQL — borra también los datos
docker compose --env-file .env stop mysql && docker compose --env-file .env rm -f mysql
docker volume rm infra_mysql_data

# Qdrant — conserva el volumen
docker compose --env-file .env stop qdrant && docker compose --env-file .env rm -f qdrant

# Qdrant — borra también los datos
docker compose --env-file .env stop qdrant && docker compose --env-file .env rm -f qdrant
docker volume rm infra_qdrant_data
```

---

## Estado y logs

```bash
# Estado de todos los servicios
docker compose --env-file .env ps

# Logs en tiempo real
docker compose --env-file .env logs -f backend
docker compose --env-file .env logs -f frontend-web
docker compose --env-file .env logs -f mysql
docker compose --env-file .env logs -f qdrant
```
