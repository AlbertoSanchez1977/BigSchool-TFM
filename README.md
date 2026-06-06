# BigSchool-TFM

Trabajo de Fin de Máster — Aplicación de gestión financiera personal con módulo de inversiones potenciado por IA.

## 📋 Descripción

Aplicación completa que combina:
- **Control de gastos e ingresos** con gráficas y filtros
- **Gestión de inversiones** en acciones con valoraciones históricas
- **Módulo de IA** con RAG para escaneo y análisis de empresas usando LLM
- **App Mobile** para consulta en modo lectura
- **MCP Server** para acceso a datos financieros desde agentes IA

## 🏗️ Arquitectura

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│ Frontend Web│     │  Mobile App  │     │ MCP Server  │
│  (Next.js)  │     │(React Native)│     │ (TypeScript)│
└──────┬──────┘     └──────┬───────┘     └──────┬──────┘
       │                   │                     │
       └───────────────────┼─────────────────────┘
                           │ HTTP (REST)
                    ┌──────┴──────┐
                    │ Backend API │
                    │  (.NET 8)   │
                    └──┬───────┬──┘
                       │       │ HTTP
                 ┌─────┴──┐ ┌──┴──────────┐
                 │ MySQL  │ │ RAG Service │
                 │   8    │ │  (Python)   │
                 └────────┘ └──────┬──────┘
                                   │
                              ┌────┴────┐
                              │ Qdrant  │
                              │(Vectors)│
                              └─────────┘
                                   │
                          ┌────────┴────────┐
                          │  Azure OpenAI   │
                          └─────────────────┘
```

## 🛠️ Stack Tecnológico

| Componente | Tecnología |
|-----------|-----------|
| Backend API | .NET 8 C#, Clean Architecture, CQRS, DDD |
| BD Principal | MySQL 8 |
| Vector Store | Qdrant |
| LLM Provider | Azure OpenAI |
| Frontend Web | Next.js 14+, TypeScript, Tailwind CSS |
| Mobile | React Native + Expo |
| MCP Server | TypeScript, Node.js |
| Infra | Docker Compose + Kubernetes (local) |

## 📁 Estructura del Proyecto

```
BigSchool-TFM/
├── src/
│   ├── backend/            → API RESTful (.NET 8)
│   ├── frontend-web/       → Aplicación web (Next.js)
│   ├── mobile/             → App móvil (React Native)
│   ├── mcp-server/         → Servidor MCP (TypeScript)
│   └── rag-service/        → Servicio RAG (Python/FastAPI)
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
- Python 3.11+
- Cuenta Azure con acceso a Azure OpenAI

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
| RAG Service | http://localhost:8000/docs |
| Qdrant Dashboard | http://localhost:6333/dashboard |

## 🎓 Contenidos Académicos Demostrados

| Área del Máster | Implementación |
|----------------|----------------|
| Principios SOLID | Arquitectura del backend |
| DDD | Bounded Contexts, Entities, Value Objects |
| CQRS | Separación Commands/Queries con MediatR |
| Arquitectura del Software | Clean Architecture, microservicios |
| Fundamentos de IA | Integración con LLM (Azure OpenAI) |
| Aplicaciones potenciadas por IA | Chat conversacional, escaneo de empresas |
| Bases de Datos | MySQL relacional + Qdrant vectorial |
| Docker y Kubernetes | Contenedorización completa + K8s local |
| RAGs | Retrieval Augmented Generation con contexto personalizable |
| Desarrollo potenciado por IA | MCP Server, uso de agentes en desarrollo |

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
