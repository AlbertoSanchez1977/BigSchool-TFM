# BigSchool-TFM — Decisiones de Arquitectura

**Fecha inicio**: 2026-06-06

Este documento registra las decisiones de arquitectura (ADR ligero) tomadas durante el proyecto.

---

## ADR-001: Monorepo

**Fecha**: 2026-06-06
**Estado**: Aceptado

**Contexto**: El proyecto tiene múltiples módulos (backend, frontend, mobile, MCP, RAG, infra).

**Decisión**: Usar un único repositorio con carpetas por módulo.

**Consecuencias**:
- (+) Más fácil de entregar como TFM
- (+) Docker Compose unificado
- (+) Coherencia de versiones
- (-) Repo más grande
- (-) CI/CD más complejo (si se añade)

---

## ADR-002: RAG Service como microservicio Python separado

**Fecha**: 2026-06-06
**Estado**: Aceptado

**Contexto**: Necesitamos integrar LLM con RAG. Las opciones eran: integrar en .NET (Semantic Kernel), servicio separado en Python (LangChain), o servicio separado en .NET.

**Decisión**: Servicio separado en Python con FastAPI + LangChain.

**Consecuencias**:
- (+) Ecosistema de IA más maduro en Python
- (+) Demuestra microservicios heterogéneos
- (+) Más documentación y ejemplos disponibles
- (-) Otro lenguaje en el proyecto
- (-) HTTP extra entre backend y RAG

---

## ADR-003: Qdrant como Vector Store

**Fecha**: 2026-06-06
**Estado**: Aceptado

**Contexto**: MySQL no soporta vectores nativos. Necesitamos un store para embeddings.

**Decisión**: Qdrant (se dockeriza fácil, API REST, ligero).

**Alternativas descartadas**:
- pgvector (requeriría cambiar a PostgreSQL)
- ChromaDB (más orientado a Python, menos robusto en producción)

---

## ADR-004: Vitest + Playwright para Frontend Web

**Fecha**: 2026-06-06
**Estado**: Aceptado

**Contexto**: Necesitamos framework de testing para Next.js.

**Decisión**: Vitest para tests unitarios/componentes, Playwright para E2E.

**Motivo**: Mayor comodidad del desarrollador con Vitest vs Jest.

---

## ADR-005: Azure OpenAI (GPT-4o-mini) como proveedor LLM

**Fecha**: 2026-06-06
**Estado**: Aceptado

**Contexto**: Necesitamos un proveedor de LLM para el módulo conversacional y embeddings. Presupuesto disponible: ~50€/mes en Azure.

**Decisión**: Azure OpenAI con modelo GPT-4o-mini para chat y ada-002 para embeddings. Rate limit implementado en el RAG service.

**Motivo**: 
- Acceso disponible con crédito personal
- GPT-4o-mini es suficiente para demo y mucho más económico
- 50€/mes cubre sobradamente desarrollo + evaluación del profesor
- Rate limit protege contra consumo accidental

**Alternativas descartadas**:
- OpenAI directo (sin ventaja, mismo coste)
- Ollama local (limitado en calidad)

---

*Añadir nuevas decisiones al final del documento siguiendo el mismo formato.*
