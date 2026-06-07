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
- ⬜ Implementación del backend (siguiente)
- ⬜ Diseño del frontend web
- ⬜ Diseño del RAG service
- ⬜ Diseño del MCP server
- ⬜ Diseño del mobile

**Siguiente paso:**
- [ ] Añadir sección de documentación de referencia al AGENTS.md raíz
- [ ] Comenzar implementación del backend: solución .NET, capas Domain y Application

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
