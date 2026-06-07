# BigSchool-TFM — Diario del Proceso

Registro cronológico del desarrollo del proyecto siguiendo un ciclo ligero:
**Diseñar → Analizar → Implementar → Revisar**

---

## 2026-06-06 — Inicio del Proyecto

### Fase: Diseño

**Actividades realizadas:**
- Definición de la visión y alcance del proyecto
- Selección del stack tecnológico completo
- Decisiones de arquitectura documentadas (ADR)
- Creación de estructura de monorepo
- Redacción de AGENTS.md (raíz + por módulo)
- Creación del README.md

**Decisiones clave:**
- Monorepo para facilitar entrega del TFM
- RAG en Python/FastAPI separado del backend .NET
- Qdrant como vector store
- Azure OpenAI como LLM provider
- Next.js + Vitest + Playwright para frontend web
- React Native Expo para mobile (solo lectura)
- TypeScript para MCP Server
- Seeds con datos fake de 2-3 meses

**Siguiente paso:**
- [x] Inicializar Git
- [x] Comenzar diseño detallado del backend (.NET 8)

---

## 2026-06-06 — Diseño detallado del Backend

### Fase: Diseño

**Módulo**: backend

**Actividades realizadas:**
- Diseño completo del modelo de datos (Users, Transactions, Categories, SubCategories, Companies, Portfolios, Holdings, Valuations, RagDocuments)
- Convenciones de datos: nomenclatura de IDs (`Id{Tabla}`), borrado lógico con `IdStatus` (Enum), auditoría (`CreatedAt`/`UpdatedAt`), seguridad (Argon2)
- Definición de Bounded Contexts: Finanzas Personales, Inversiones, IA/RAG
- Estructura CQRS detallada con ejemplos de Commands/Queries
- Convenciones de API REST: versionado, envelope pattern, paginación

**Resultado / Estado:**
- Documento `docs/02-backend-design.md` aprobado
- Commit: `5378412`

---

## 2026-06-06 — Infraestructura base Docker Compose

### Fase: Diseño

**Módulo**: infra

**Actividades realizadas:**
- Docker Compose base con servicios MySQL 8 y Qdrant
- Servicios de aplicación (backend, rag, frontend, mcp) definidos pero comentados (pendientes de Dockerfile)
- Docker Compose override para desarrollo local
- `.env.example` con todas las variables de entorno documentadas
- AGENTS.md de infraestructura con convenciones, puertos y estructura k8s

**Resultado / Estado:**
- Infraestructura mínima lista para levantar BD y vector store
- Commit: `c9644df` (HEAD en develop)

---

## 2026-06-07 — Revisión de estado y mejora de referencias

### Fase: Revisión

**Módulo**: docs

**Actividades realizadas:**
- Revisión del estado global del proyecto
- Identificación de problema: los AGENTS.md de cada módulo no referencian los documentos de diseño consolidados en `docs/`
- Solución: añadir sección "Documentación de referencia" al AGENTS.md raíz que enlace a los diseños aprobados

**Estado actual del proyecto:**
- ✅ Visión y alcance definidos (`docs/00-vision.md`)
- ✅ ADRs de arquitectura (`docs/01-arquitectura.md`)
- ✅ Diseño detallado del backend (`docs/02-backend-design.md`)
- ✅ Estructura de monorepo creada con AGENTS.md por módulo
- ✅ Docker Compose base (MySQL + Qdrant)
- ✅ Scaffolding del backend (Fase 1 completada)
- ⬜ Implementación del backend — BC Finanzas Personales (siguiente)
- ⬜ Diseño del frontend web
- ⬜ Diseño del RAG service
- ⬜ Diseño del MCP server
- ⬜ Diseño del mobile

**Siguiente paso:**
- [x] Añadir sección de documentación de referencia al AGENTS.md raíz
- [x] Comenzar implementación del backend: solución .NET, capas Domain y Application

---

## 2026-06-07 — Scaffolding completo del backend .NET 8

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Plan detallado de scaffolding con 8 tareas incrementales (`docs/superpowers/plans/2026-06-07-backend-scaffolding.md`)
- Flujo de trabajo: `develop` → `feature/backend-scaffolding-taskN` → PR → revisión humana → merge
- **Task 1** (PR #1): Solución .NET 8 con 4 proyectos fuente + 3 de tests (formato `.slnx` de SDK 10)
- **Task 2** (PR #2): `Directory.Build.props` con versiones centralizadas (AutoMapper 15.1.3 por vulnerabilidad en 13/14)
- **Task 3** (PR #3): Capa Domain — `BaseEntity`, `IAggregateRoot`, `IUnitOfWork`, `IDomainEvent`, Enums, 3 tests unitarios
- **Task 4** (PR #4): Capa Application — `IRepository<T,Y>`, repos específicos, `AppSettings`, `DomainEventNotification`, `IDbConnectionFactory`, `IRagServiceClient`
- **Task 5** (PR #5): Capa Infrastructure — `EFRepository<T,Y>` abstracto, `BigSchoolDbContext` con dispatch condicional de eventos, `DbConnectionMySqlFactory`
- **Task 6** (PR #6): Capa WebApi — `Program.cs` con Autofac simplificado, `CustomMediatR` (SyncContinueOnException), Serilog (file + console), Swagger, Health Check, CORS
- **Task 7** (PR #7): `.editorconfig` con convenciones C#
- **Task 8** (PR #8): Verificación quickstart — MySQL Docker, `/health` OK, Swagger OK, Serilog file sink OK

**Decisiones clave tomadas durante la implementación:**
- `IUnitOfWork` en Domain, `IRepository<T,Y>` en Application (Domain permanece persistence-agnostic)
- `SaveChangesAsync(bool dispatchEvents = true)` sin `CancellationToken` → evita recursión infinita con overrides de EF Core
- `IAggregateRoot` como marcador — solo Aggregate Roots tienen repositorio
- `CustomMediatR` en Application/Infrastructure (no WebApi) — handlers pueden elegir estrategia de publicación
- Autofac simplificado: un `RegisterAssemblyTypes` por capa en `Program.cs`
- Sin `appsettings.Development.json` — usar `dotnet user-secrets` para overrides locales

**Resultado / Estado:**
- Backend scaffolding 100% completado (8/8 tareas, PRs #1-#8)
- Build: 0 errores, 3 tests unitarios pasando
- API arrancable con `dotnet run`, verificada con MySQL Docker

**Siguiente paso:**
- [ ] Fase 2: Implementación BC Finanzas Personales (entidades completas, CQRS, endpoints)

---

*Añadir nuevas entradas al final del documento con fecha y fase.*

### Plantilla para nuevas entradas:

```markdown
## YYYY-MM-DD — [Título descriptivo]

### Fase: [Diseño | Análisis | Implementación | Revisión]

**Módulo**: [backend | frontend-web | mobile | mcp-server | rag-service | infra]

**Actividades realizadas:**
- ...

**Decisiones / Problemas encontrados:**
- ...

**Resultado / Estado:**
- ...

**Siguiente paso:**
- ...
```
