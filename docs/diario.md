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
- [ ] Inicializar Git
- [ ] Comenzar diseño detallado del backend (.NET 8)

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
