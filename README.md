# BigSchool-TFM

Trabajo de Fin de Máster — Aplicación de gestión financiera personal con módulo de inversiones y un esqueleto de IA como base para trabajo futuro.

> **Nota de alcance (cambio de enfoque, 2026-06-23).** La entrega evaluable del TFM se centra en un **MVP**: **Backend API** (completo, Monolito Modular) + **Frontend-Web** funcional. La parte de IA se entrega como **esqueleto** (UI de chat + subida de documentos en el frontend) **sin conexión real al LLM todavía**. El **RAG completo** (Indexer + Qdrant + LLM de Azure), el **MCP** (reorientado a Python) y la **app Mobile** quedan como **trabajo futuro**.
>
> 📄 Contexto y decisiones: [`docs/00-vision.md`](docs/00-vision.md) · [`docs/01-arquitectura.md`](docs/01-arquitectura.md) (ADR-008).

## 🔗 Demo y entregables

| Recurso | Enlace |
|---------|--------|
| 🌐 Demo desplegada (Azure) | https://bstfminvesting-demo-2026-07-03-frontend.azurewebsites.net/ |
| 📽️ Presentación (PPTX) | _pendiente de publicar_ |
| 🎥 Vídeo de defensa | _pendiente de publicar_ |

## 📋 Descripción general

Aplicación completa que combina:
- **Control de gastos e ingresos** con gráficas y filtros
- **Gestión de inversiones** en acciones con valoraciones históricas y carteras (venta FIFO, multimoneda)
- **Esqueleto de IA (MVP)** — UI de chat y subida de documentos en el frontend, preparada pero **sin conexión real al LLM** (activación pendiente como trabajo futuro)
- **Trabajo futuro** — RAG completo con Qdrant + LLM de Azure, MCP en Python (flujos de análisis), app Mobile

> 📄 Visión y alcance funcional: [`docs/00-vision.md`](docs/00-vision.md).

## ✨ Funcionalidades

### Implementadas (MVP)
- **Autenticación JWT** — registro, login y refresco proactivo de token
- **Gastos / Ingresos** — CRUD completo, filtros por mes/año y categoría, gráficas (barras por categoría y serie mensual)
- **Inversiones** — carteras, holdings, valoraciones históricas, rendimiento (PnL) y **venta FIFO a nivel empresa** (multimoneda)
- **Dashboard** — KPIs (balance, ahorro, valor de cartera), gráficas y últimas transacciones
- **Perfil de usuario** — ver y editar datos propios
- **Emails logging** — registro de emails simulados (contacto + bienvenida)

### Esqueleto / roadmap
- **Chat IA + subida de documentos** — UI presente, sin conexión real al LLM 🔜
- **RAG** (Qdrant + LLM Azure), **MCP** (Python), **Mobile** (React Native) 🔜

> 📄 Detalle de módulos y estado: [`docs/diario.md`](docs/diario.md) · specs y planes en [`docs/superpowers/`](docs/superpowers/).

## 🏗️ Arquitectura

**MVP (entrega del TFM)** — línea sólida; **trabajo futuro** — entre corchetes:

```
                 ┌──────────────────────────┐
                 │       Frontend Web       │
                 │  (Next.js) — arquitectura│
                 │  hexagonal ligera; incluye│
                 │  esqueleto de chat +     │
                 │  subida de documentos    │
                 └────────────┬─────────────┘
                              │ HTTP (REST)
                       ┌──────┴───────┐
                       │ Backend API  │
                       │  (.NET 8)    │
                       │ Monolito Mod.│
                       └──────┬───────┘
                              │
                         ┌────┴────┐
                         │ MySQL 8 │
                         └─────────┘

── Trabajo futuro ─────────────────────────────────────────────
   [ LLM de pago externo (Claude / GPT-4o) — activar chat ]
   [ Indexer (Python) ] → [ Qdrant (vectores) ] → [ LLM Azure ]
   [ MCP Server (Python): screener / criterios / revisión cartera ]
   [ Mobile App (React Native, solo lectura) ]
```

> 📄 ADRs globales: [`docs/01-arquitectura.md`](docs/01-arquitectura.md) · Backend (modelo de datos, Bounded Contexts, monolito modular): [`docs/02-backend-design.md`](docs/02-backend-design.md), [`docs/superpowers/specs/005-2026-07-01-backend-modular-monolith-design.md`](docs/superpowers/specs/005-2026-07-01-backend-modular-monolith-design.md) · Frontend (capas, flujo de datos): [`docs/05-frontend-arquitectura.md`](docs/05-frontend-arquitectura.md) · Diseño visual: [`docs/03-frontend-design.md`](docs/03-frontend-design.md).

## 🛠️ Stack Tecnológico

### MVP (entrega del TFM)
| Componente | Tecnología |
|-----------|-----------|
| Backend API | .NET 8 C#, **Monolito Modular** (Clean Architecture, CQRS, MediatR, DDD) |
| BD Principal | MySQL 8 |
| Frontend Web | Next.js, TypeScript, Tailwind CSS — **arquitectura hexagonal ligera** |
| Infra (dev) | **Docker Compose** |

### Roadmap / trabajo futuro
| Componente | Tecnología (prevista) |
|-----------|-----------|
| LLM (chat) | LLM de pago externo (Claude / GPT-4o) vía Backend API |
| Indexer / RAG | Python, Qdrant (vector store), LLM de Azure para embeddings e indexación |
| MCP Server | Python — herramienta de flujos (screener, criterios, revisión de cartera) |
| Mobile | React Native + Expo (solo lectura) |
| Orquestación | Kubernetes (local) — manifiestos en `infra/k8s/` |

> 📄 Backend: [`docs/02-backend-design.md`](docs/02-backend-design.md) · Frontend: [`docs/05-frontend-arquitectura.md`](docs/05-frontend-arquitectura.md) · Deuda técnica backend: [`docs/04-backend-tech-debt.md`](docs/04-backend-tech-debt.md).

## 📁 Estructura del Proyecto

```
BigSchool-TFM/
├── src/
│   ├── backend/            → API RESTful (.NET 8, Monolito Modular) — MVP · AGENTS.md
│   ├── frontend-web/       → Aplicación web (Next.js) — MVP · AGENTS.md
│   ├── mobile/             → App móvil (React Native) — roadmap
│   ├── mcp-server/         → Servidor MCP (se reorienta a Python) — roadmap
│   └── rag-service/        → Indexer/RAG (Python) — roadmap
├── infra/
│   ├── docker/                    → Dockerfiles
│   ├── docker-compose.yml         → Definición base de servicios
│   ├── docker-compose.override.yml→ Config operativa dev (puertos, env, volúmenes)
│   └── k8s/                       → Manifiestos Kubernetes — roadmap
├── docs/                   → Documentación del TFM (visión, arquitectura, specs/planes)
├── tests/                  → Tests E2E globales
└── AGENTS.md              → Instrucciones para agentes IA
```

> 📄 Cada módulo tiene su propio `AGENTS.md`: [`src/backend/AGENTS.md`](src/backend/AGENTS.md) · [`src/frontend-web/AGENTS.md`](src/frontend-web/AGENTS.md) · Infra: [`infra/README.md`](infra/README.md).

## 🚀 Inicio Rápido

### Prerrequisitos
- Docker Desktop
- .NET 8 SDK
- Node.js 20+ (con `pnpm`)
- _(Roadmap)_ Clave de API de un LLM de pago (Claude / OpenAI) para activar el chat de IA
- _(Roadmap)_ Python 3.11+ y cuenta Azure con acceso a Azure OpenAI para el RAG completo

### Opción A — Entorno completo con Docker Compose

> La configuración operativa (puertos, variables de entorno, volúmenes, healthchecks) vive en
> `docker-compose.override.yml`, que Compose **fusiona automáticamente** con el fichero base al
> ejecutar desde la carpeta `infra/`. Por eso los comandos se lanzan desde ahí.

```bash
git clone <url-del-repo>
cd BigSchool-TFM/infra

cp .env.example .env      # editar .env con tus credenciales

docker compose up -d --build   # fusiona docker-compose.yml + docker-compose.override.yml
```

**Acceso a los servicios (Docker):**

| Servicio | URL |
|----------|-----|
| Frontend Web | http://localhost:3002 |
| Backend API | http://localhost:8082/api/v1 |
| Swagger API | http://localhost:8082/swagger |
| MySQL | localhost:3306 |
| _(roadmap)_ Qdrant Dashboard | http://localhost:6333/dashboard |

### Opción B — Desarrollo local (backend + frontend fuera de Docker)

Modo de trabajo diario: solo MySQL en contenedor; backend y frontend se ejecutan en local.

```bash
# 1) Solo la base de datos en Docker
cd infra && docker compose up -d mysql

# 2) Backend (.NET 8) — escucha en el puerto 5285
cd ../src/backend && dotnet run --project src/BigSchool.WebApi

# 3) Frontend (Next.js) — escucha en el puerto 3000
cd ../frontend-web && pnpm install && pnpm dev
```

**Acceso a los servicios (local):**

| Servicio | URL |
|----------|-----|
| Frontend Web | http://localhost:3000 |
| Backend API | http://localhost:5285/api/v1 |
| Swagger API | http://localhost:5285/swagger |

## 🧪 Metodología de desarrollo

- **TDD (test primero)** — ciclo Red-Green-Refactor: el test se escribe antes que la implementación.
  Backend con **xUnit + FluentAssertions + Moq**; frontend con **Vitest + React Testing Library** y
  **Playwright** (E2E de flujos críticos).
- **Desarrollo asistido por IA con [superpowers](https://github.com/obra/superpowers)** siguiendo un
  **SDD ligero** (Spec-Driven Development): cada bloque de trabajo nace de una **spec** de diseño y un
  **plan** numerado, que se ejecuta por tareas con checkpoints de revisión humana entre ellas.

> 📄 Specs de diseño y planes de ejecución versionados: [`docs/superpowers/specs/`](docs/superpowers/specs/) · [`docs/superpowers/plans/`](docs/superpowers/plans/). Guía de pruebas manuales del frontend: [`docs/guia-pruebas-manuales-frontend.md`](docs/guia-pruebas-manuales-frontend.md).

## 🎓 Contenidos Académicos Demostrados

| Área del Máster | Implementación | Estado |
|----------------|----------------|--------|
| Principios SOLID | Arquitectura del backend | ✅ MVP |
| DDD | Bounded Contexts, Entities, Value Objects | ✅ MVP |
| CQRS | Separación Commands/Queries con MediatR | ✅ MVP |
| Arquitectura del Software | Clean Architecture, **Monolito Modular** | ✅ MVP |
| Bases de Datos | MySQL relacional | ✅ MVP |
| Docker / Contenedorización | Docker Compose (dev) | ✅ MVP |
| Desarrollo potenciado por IA | superpowers + SDD ligero (specs/planes), TDD test-primero | ✅ MVP |
| Aplicaciones potenciadas por IA | Esqueleto de chat + subida en frontend — **planteado, sin conexiones reales** | 🔜 Roadmap |
| Kubernetes | Manifiestos `infra/k8s/` (no desplegado en local) | 🔜 Roadmap |
| Fundamentos de IA / RAGs | Indexer + Qdrant + LLM Azure (diseño y roadmap) | 🔜 Roadmap |
| Vector Store | Qdrant para Retrieval Augmented Generation | 🔜 Roadmap |

## 📊 Datos de Demo

El proyecto incluye seeds con datos fake de 2-3 meses para demostración:
- Gastos variados (supermercado, transporte, ocio, facturas)
- Ingresos (nómina, freelance)
- Empresas y acciones en cartera (Apple, Microsoft, Inditex, etc.)
- Valoraciones históricas

> 📄 Seeds y modelo de datos: [`docs/02-backend-design.md`](docs/02-backend-design.md) · [`infra/README.md`](infra/README.md).

## 📝 Licencia

Proyecto académico (Trabajo de Fin de Máster) bajo licencia **MIT** — ver [`LICENSE`](LICENSE).

## 👤 Autor

Alberto Sánchez Moya — Arquitecto de Software
