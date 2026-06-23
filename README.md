# BigSchool-TFM

Trabajo de Fin de Máster — Aplicación de gestión financiera personal con módulo de inversiones potenciado por IA.

> **Nota de alcance (cambio de enfoque, 2026-06-23).** La entrega evaluable del TFM se centra en un **MVP**: **Backend API** (completo) + **Frontend-Web** con una **demo de IA mínima** (chat + subida de documentos) que pasa por el Backend hacia un **LLM de pago externo** (no Azure). El **RAG completo** (Indexer + Qdrant + LLM de Azure para indexar/buscar), el **MCP** (reorientado a Python como herramienta de flujos) y la **app Mobile** quedan como **trabajo futuro**. Ver `docs/01-arquitectura.md` (ADR-008) y `docs/00-vision.md`.

## 📋 Descripción

Aplicación completa que combina:
- **Control de gastos e ingresos** con gráficas y filtros
- **Gestión de inversiones** en acciones con valoraciones históricas y carteras (FIFO, multimoneda)
- **Módulo de IA (MVP)** — chat y subida de documentos vía Backend API hacia un LLM de pago externo
- **Trabajo futuro** — RAG completo con Qdrant + LLM de Azure, MCP en Python (flujos de análisis), app Mobile

## 🏗️ Arquitectura

**MVP (entrega del TFM)** — línea sólida; **trabajo futuro** — entre corchetes:

```
                 ┌──────────────────────────┐
                 │       Frontend Web       │
                 │  (Next.js) — incluye     │
                 │  zona de chat + subida   │
                 │  de documentos           │
                 └────────────┬─────────────┘
                              │ HTTP (REST)
                       ┌──────┴──────┐
                       │ Backend API │
                       │  (.NET 8)   │
                       └──┬───────┬──┘
                          │       │ HTTP
                    ┌─────┴──┐ ┌──┴───────────────────┐
                    │ MySQL  │ │  LLM de pago externo │
                    │   8    │ │  (Claude / GPT-4o)   │
                    └────────┘ └──────────────────────┘

── Trabajo futuro ─────────────────────────────────────────────
   [ Indexer (Python) ] → [ Qdrant (vectores) ] → [ LLM Azure ]
   [ MCP Server (Python): screener / criterios / revisión cartera ]
   [ Mobile App (React Native, solo lectura) ]
```

## 🛠️ Stack Tecnológico

### MVP (entrega del TFM)
| Componente | Tecnología |
|-----------|-----------|
| Backend API | .NET 8 C#, Clean Architecture, CQRS, DDD |
| BD Principal | MySQL 8 |
| Frontend Web | Next.js 14+, TypeScript, Tailwind CSS |
| LLM (chat MVP) | LLM de pago externo (Claude / GPT-4o), vía Backend API |
| Infra | Docker Compose + Kubernetes (local) |

### Trabajo futuro
| Componente | Tecnología (prevista) |
|-----------|-----------|
| Indexer / RAG | Python, Qdrant (vector store), LLM de Azure para embeddings e indexación |
| MCP Server | Python — herramienta de flujos (screener, criterios, revisión de cartera) |
| Mobile | React Native + Expo (solo lectura) |

## 📁 Estructura del Proyecto

```
BigSchool-TFM/
├── src/
│   ├── backend/            → API RESTful (.NET 8) — MVP
│   ├── frontend-web/       → Aplicación web (Next.js) — MVP
│   ├── mobile/             → App móvil (React Native) — trabajo futuro
│   ├── mcp-server/         → Servidor MCP — se reorienta a Python (trabajo futuro)
│   └── rag-service/        → Indexer/RAG (Python) — trabajo futuro
├── infra/
│   ├── docker/             → Dockerfiles
│   ├── docker-compose.yml  → Entorno de desarrollo
│   └── k8s/               → Manifiestos Kubernetes
├── docs/                   → Documentación del TFM
├── tests/                  → Tests E2E globales
└── AGENTS.md              → Instrucciones para agentes IA
```

## 🚀 Inicio Rápido

### Prerrequisitos
- Docker Desktop
- .NET 8 SDK
- Node.js 20+
- Clave de API de un LLM de pago (Claude / OpenAI) para la demo de IA del MVP
- _(Trabajo futuro)_ Python 3.11+ y cuenta Azure con acceso a Azure OpenAI para el RAG completo

### Levantar el entorno completo

```bash
# Clonar el repositorio
git clone <url-del-repo>
cd BigSchool-TFM

# Configurar variables de entorno
cp infra/.env.example infra/.env
# Editar infra/.env con tus credenciales

# Levantar todos los servicios
docker-compose -f infra/docker-compose.yml up -d
```

### Acceso a los servicios

| Servicio | URL |
|----------|-----|
| Frontend Web | http://localhost:3000 |
| Backend API | http://localhost:5000 |
| Swagger API | http://localhost:5000/swagger |
| _(futuro)_ Indexer/RAG | http://localhost:8000/docs |
| _(futuro)_ Qdrant Dashboard | http://localhost:6333/dashboard |

## 🎓 Contenidos Académicos Demostrados

| Área del Máster | Implementación | Estado |
|----------------|----------------|--------|
| Principios SOLID | Arquitectura del backend | ✅ MVP |
| DDD | Bounded Contexts, Entities, Value Objects | ✅ MVP |
| CQRS | Separación Commands/Queries con MediatR | ✅ MVP |
| Arquitectura del Software | Clean Architecture, microservicios | ✅ MVP |
| Aplicaciones potenciadas por IA | Chat conversacional vía LLM de pago externo (Backend API) | ✅ MVP (mínimo) |
| Bases de Datos | MySQL relacional | ✅ MVP |
| Docker y Kubernetes | Contenedorización + K8s local | ✅ MVP |
| Desarrollo potenciado por IA | Uso de agentes IA en el propio desarrollo | ✅ MVP |
| Fundamentos de IA / RAGs | Indexer + Qdrant + LLM Azure (diseño y roadmap) | 🔜 Futuro |
| Vector Store | Qdrant para Retrieval Augmented Generation | 🔜 Futuro |

## 📊 Datos de Demo

El proyecto incluye seeds con datos fake de 2-3 meses para demostración:
- Gastos variados (supermercado, transporte, ocio, facturas)
- Ingresos (nómina, freelance)
- Empresas y acciones en cartera (Apple, Microsoft, Inditex, etc.)
- Valoraciones históricas

## 📝 Licencia

Proyecto académico — Trabajo de Fin de Máster.

## 👤 Autor

Alberto Sánchez — Arquitecto de Software
