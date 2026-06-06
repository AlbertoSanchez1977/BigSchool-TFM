# AGENTS.md — Infraestructura

## Tecnología
- Docker & Docker Compose (desarrollo local)
- Kubernetes (demostración local con minikube o kind)
- MySQL 8 (BD principal)
- Qdrant (vector store)

## Estructura

```
infra/
├── docker/
│   ├── backend.Dockerfile
│   ├── frontend-web.Dockerfile
│   ├── mobile.Dockerfile          → (solo para build, no para run)
│   ├── mcp-server.Dockerfile
│   ├── rag-service.Dockerfile
│   └── mysql/
│       ├── init.sql              → Script de creación de tablas
│       └── seed.sql              → Datos fake de 2-3 meses para demo
├── docker-compose.yml            → Entorno completo de desarrollo
├── docker-compose.override.yml   → Overrides para desarrollo local
├── k8s/
│   ├── namespace.yaml
│   ├── backend/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── frontend-web/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── rag-service/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── mcp-server/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── mysql/
│   │   ├── deployment.yaml
│   │   ├── service.yaml
│   │   └── pvc.yaml
│   ├── qdrant/
│   │   ├── deployment.yaml
│   │   ├── service.yaml
│   │   └── pvc.yaml
│   └── configmaps/
│       └── env-config.yaml
└── .env.example                  → Variables de entorno de ejemplo
```

## Docker Compose — Servicios

| Servicio | Puerto | Imagen |
|----------|--------|--------|
| backend | 5000 | Custom (.NET 8) |
| frontend-web | 3000 | Custom (Next.js) |
| rag-service | 8000 | Custom (Python/FastAPI) |
| mcp-server | 4000 | Custom (Node.js) |
| mysql | 3306 | mysql:8 |
| qdrant | 6333 | qdrant/qdrant |

## Variables de Entorno Requeridas

```env
# MySQL
MYSQL_ROOT_PASSWORD=
MYSQL_DATABASE=bigschool
MYSQL_USER=
MYSQL_PASSWORD=

# Azure OpenAI
AZURE_OPENAI_ENDPOINT=
AZURE_OPENAI_API_KEY=
AZURE_OPENAI_DEPLOYMENT_NAME=
AZURE_OPENAI_EMBEDDING_DEPLOYMENT=

# Qdrant
QDRANT_HOST=qdrant
QDRANT_PORT=6333

# JWT
JWT_SECRET=
JWT_ISSUER=bigschool-tfm
JWT_AUDIENCE=bigschool-tfm

# RAG Service
RAG_SERVICE_URL=http://rag-service:8000
```

## Seeds de Datos (Demo)

El archivo `seed.sql` debe incluir datos fake de **2-3 meses** para que el profesor pueda:
- Ver gráficas con datos reales
- Navegar entre pantallas con contenido
- Probar filtros por fecha y categoría

### Datos seed incluidos:
- **Gastos**: 30-50 registros variados (supermercado, transporte, ocio, facturas...)
- **Ingresos**: 6-9 registros (nómina, freelance, dividendos...)
- **Empresas**: 5-8 empresas reales (Apple, Microsoft, Inditex, etc.)
- **Valoraciones**: Histórico de 2-3 meses por empresa
- **Acciones en cartera**: 3-5 posiciones abiertas

## Convenciones

- Un Dockerfile por servicio en `infra/docker/`
- Multi-stage builds para reducir tamaño de imagen
- Healthchecks en todos los servicios
- Volúmenes para persistencia de MySQL y Qdrant
- Red interna Docker para comunicación entre servicios
- `.env.example` con todas las variables documentadas (sin valores reales)

## Instrucciones para el Agente

1. Docker Compose debe levantar TODO el entorno con `docker-compose up`
2. No hardcodear secrets — siempre variables de entorno
3. Usar multi-stage builds en Dockerfiles
4. Kubernetes es demostrativo — no necesita ser production-ready
5. Incluir healthcheck endpoints en cada servicio
6. El `init.sql` crea estructura, el `seed.sql` carga datos fake de demo
7. Los seeds deben ser realistas y cubrir 2-3 meses para que el profesor navegue cómodamente
8. Documentar el proceso de setup en el README del proyecto
