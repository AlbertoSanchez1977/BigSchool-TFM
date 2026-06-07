# AGENTS.md — BigSchool-TFM

## Descripción del Proyecto

BigSchool-TFM es un Trabajo de Fin de Máster compuesto por una aplicación financiera personal con los siguientes módulos:

1. **Backend API** — API RESTful en .NET 8 C# con Clean Architecture, CQRS y DDD
2. **Frontend Web** — Aplicación Next.js con TypeScript para gestión completa
3. **Mobile** — App React Native (Expo) de solo lectura
4. **MCP Server** — Servidor MCP en TypeScript/Node.js para datos financieros
5. **RAG + LLM** — Módulo de IA con Azure OpenAI y Qdrant para escaneo de empresas
6. **Infraestructura** — Docker Compose para desarrollo local, Kubernetes para despliegue

## Stack Tecnológico

| Componente | Tecnología |
|-----------|-----------|
| Backend API | .NET 8 C#, Clean Architecture, CQRS, MediatR, DDD |
| BD Principal | MySQL 8 |
| Vector Store | Qdrant |
| LLM Provider | Azure OpenAI |
| Frontend Web | Next.js 14+, TypeScript, Tailwind CSS |
| Mobile | React Native + Expo |
| MCP Server | TypeScript, Node.js |
| Contenedores | Docker, Docker Compose |
| Orquestación | Kubernetes (local) |
| IDE | VS Code |

## Estructura del Monorepo

```
BigSchool-TFM/
├── src/
│   ├── backend/            → .NET 8 API (Clean Architecture)
│   ├── frontend-web/       → Next.js + TypeScript
│   ├── mobile/             → React Native Expo
│   ├── mcp-server/         → MCP Server TypeScript
│   └── rag-service/        → Servicio RAG (integrado o separado del backend)
├── infra/
│   ├── docker/             → Dockerfiles por servicio
│   ├── docker-compose.yml  → Entorno de desarrollo local
│   └── k8s/               → Manifiestos Kubernetes
├── docs/                   → Documentación del TFM
├── tests/                  → Tests de integración end-to-end
└── AGENTS.md              → Este archivo
```

Cada módulo en `src/` tiene su propio `AGENTS.md` con instrucciones específicas.

## Documentación de Referencia (Diseños Aprobados)

**IMPORTANTE**: Antes de implementar cualquier módulo, el agente DEBE consultar los documentos de diseño relevantes en `docs/`. Estos contienen decisiones ya consolidadas que no deben contradecirse.

| Documento | Contenido | Aplica a |
|-----------|-----------|----------|
| `docs/00-vision.md` | Alcance funcional, módulos, decisiones de alto nivel | Todos los módulos |
| `docs/01-arquitectura.md` | ADRs (monorepo, RAG separado, stack, comunicación) | Todos los módulos |
| `docs/02-backend-design.md` | Modelo de datos, convenciones BD, Bounded Contexts, API REST | backend, infra (init.sql) |
| `docs/diario.md` | Estado actual del proyecto, qué está hecho y qué falta | Planificación |

### Regla para los agentes
- Si vas a implementar el **backend**: lee `docs/02-backend-design.md` completo antes de escribir código
- Si vas a crear **seeds o init.sql**: usa el modelo de datos de `docs/02-backend-design.md`
- Si vas a diseñar un **nuevo módulo**: lee `docs/00-vision.md` para el alcance y `docs/01-arquitectura.md` para restricciones
- Si dudas del **estado actual**: consulta `docs/diario.md`

## Convenciones Generales

### Idioma
- **Documentación y commits**: Español
- **Código** (variables, funciones, clases): Inglés
- **Comentarios en código**: Español cuando sea necesario

### Git
- **Ramas**: Git Flow estándar
  - `main` — Producción estable
  - `develop` — Integración
  - `feature/xxx` — Nuevas funcionalidades
  - `hotfix/xxx` — Correcciones urgentes
  - `release/xxx` — Preparación de versiones
- **Commits**: Conventional Commits en español
  - `feat: añadir endpoint de gastos`
  - `fix: corregir cálculo de balance mensual`
  - `docs: actualizar documentación de API`
  - `test: añadir tests de integración para inversiones`
  - `refactor: extraer servicio de valoraciones`
  - `infra: añadir manifiesto k8s para qdrant`

### Testing
- Tests unitarios y de integración **obligatorios** antes de merge
- Backend: xUnit + FluentAssertions + Moq
- Frontend Web: Jest + React Testing Library
- Mobile: Jest + React Native Testing Library
- Seguir TDD (Red-Green-Refactor) cuando sea posible

### Principios de Diseño
- **SOLID** en todo el código
- **DDD** en el dominio del backend (Entities, Value Objects, Aggregates)
- **CQRS** para separar lecturas y escrituras
- **Clean Architecture** en el backend (Domain → Application → Infrastructure → Presentation)
- **YAGNI** — No implementar lo que no se necesita aún
- **DRY** — No repetir lógica de negocio

## Instrucciones para el Agente

### Antes de escribir código
1. Verificar que existe un diseño o spec aprobado para la funcionalidad
2. Identificar qué módulo(s) se ven afectados
3. Leer el `AGENTS.md` específico del módulo
4. Comprobar que los tests existentes pasan antes de modificar nada

### Al escribir código
1. Seguir TDD: escribir test primero, ver que falla, implementar, ver que pasa
2. Respetar las convenciones del módulo específico
3. No mezclar lógica de diferentes bounded contexts
4. Commits pequeños y frecuentes con Conventional Commits en español

### Antes de completar
1. Todos los tests pasan (unitarios + integración)
2. El código sigue los principios SOLID
3. No hay código comentado ni TODOs sin resolver
4. Docker Compose sigue funcionando correctamente
5. La documentación se actualiza si es necesario

## Contexto del Desarrollador

- Experto en .NET 8, C#, Backend, BD Relacionales, Docker
- Experiencia media en TypeScript, MVC, Kubernetes
- Experiencia baja en CSS, HTML, Mobile, UI/UX
- Usar v0.app o herramientas similares para generar componentes UI cuando sea necesario
- Priorizar claridad y mantenibilidad sobre optimización prematura

## Módulos — Resumen de Responsabilidades

### Backend API (`src/backend/`)
- CRUD de gastos e ingresos
- CRUD de empresas/acciones y valoraciones
- Autenticación y autorización (JWT)
- Endpoints para gráficas y reportes
- Integración con el servicio RAG

### Frontend Web (`src/frontend-web/`)
- Dashboard con gráficas mensuales de gastos/ingresos
- Gestión de inversiones y valoraciones históricas
- Chat con LLM para escaneo de empresas
- Configuración del RAG (subir/modificar contexto)
- Filtros y exportación de datos

### Mobile (`src/mobile/`)
- Visualización de balance financiero (solo lectura)
- Gráficas de gastos/ingresos
- Estado de inversiones
- Notificaciones (opcional)

### MCP Server (`src/mcp-server/`)
- Exposición de datos financieros como herramientas MCP
- Consultas de balance, gastos, inversiones
- Generación de gráficas bajo demanda

### RAG Service (`src/rag-service/`)
- Ingesta de documentos al vector store (Qdrant)
- Búsqueda semántica de empresas
- Integración con Azure OpenAI para respuestas
- API para gestionar el contexto del RAG (CRUD de documentos)

### Infraestructura (`infra/`)
- Docker Compose con todos los servicios
- Kubernetes manifests para despliegue
- Scripts de inicialización de BD
- Variables de entorno y secrets
