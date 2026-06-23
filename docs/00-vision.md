# BigSchool-TFM — Visión del Proyecto

**Fecha**: 2026-06-06
**Autor**: Alberto Sánchez

> **Cambio de enfoque (2026-06-23).** Por un blocker de Azure (la suscripción no permite crear recursos de IA) y para priorizar un producto mínimo viable, la entrega del TFM se reorienta a un **MVP**: Backend API (completo) + Frontend-Web con una **demo de IA mínima** (chat + subida de documentos) que pasa por el Backend hacia un **LLM de pago externo** (no Azure). El **RAG completo**, el **MCP** (reorientado a Python) y la **app Mobile** pasan a **trabajo futuro**. Detalle de la decisión en `docs/01-arquitectura.md` (ADR-008).

## Objetivo

Desarrollo de una aplicación financiera personal como Trabajo de Fin de Máster que demuestre los conocimientos adquiridos durante el programa:

- Principios SOLID, DDD, CQRS
- Arquitectura del Software (Clean Architecture, microservicios)
- Fundamentos e integración de IA (LLM, RAG)
- Bases de Datos (relacional + vectorial)
- Docker y Kubernetes
- Desarrollo potenciado por IA

## Módulos

### MVP (entrega del TFM)
1. **Backend API** — .NET 8, Clean Architecture, CQRS, DDD, MySQL
2. **Frontend Web** — Next.js, TypeScript, Tailwind CSS; incluye zona de chat y subida de documentos
3. **IA mínima** — chat vía Backend API hacia un LLM de pago externo (Claude / GPT-4o)
4. **Infraestructura** — Docker Compose + Kubernetes local

### Trabajo futuro
5. **Indexer / RAG** — Python, Qdrant y LLM de Azure para indexar y buscar documentos
6. **MCP Server (Python)** — herramienta de flujos de análisis de inversión (ver más abajo)
7. **Mobile** — React Native + Expo (solo lectura)

## Alcance Funcional

### Control Financiero
- CRUD de gastos e ingresos con categorías
- Gráficas mensuales y filtros por periodo/categoría
- Balance y evolución temporal

### Inversiones
- Gestión de cartera de acciones
- Valoraciones históricas por empresa
- Rentabilidad y evolución de cartera

### IA / LLM
- **MVP**: chat conversacional vía Backend API hacia un LLM de pago externo; subida de documentos como alimentador del futuro indexer.
- **Futuro**: contexto RAG completo (Indexer + Qdrant + LLM de Azure), gestión del contexto por el usuario.

### Trabajo futuro

**Indexer / RAG** — indexación y búsqueda semántica de documentos sobre Qdrant, con LLM de Azure para embeddings, alimentado desde la subida de documentos del frontend.

**MCP Server (Python)** — reorientado desde la idea original de widget para ChatGPT (TypeScript) hacia un **MCP integrado** consumible por cualquier LLM de pago (Claude, etc.). Expone **flujos de análisis de inversión** como herramientas, a alto nivel:
- **Screener** — revisar y filtrar empresas candidatas según métricas.
- **Criterios** — dada una empresa, revisión a fondo según criterios de value investing.
- **Revisión de cartera** — análisis del estado y la salud de la cartera del usuario.

Por detrás compartiría la capa de IA: LLM de Azure para indexar/buscar en el RAG y un modelo económico (p. ej. GPT-4o) para tareas auxiliares, con el LLM de pago del usuario por encima.

**Mobile** — visualización de balance y gráficas (solo lectura).

## Decisiones de Arquitectura

| Decisión | Elección | Motivo |
|----------|----------|--------|
| Monorepo | Sí | Facilita entrega y coherencia del TFM |
| BD | MySQL única | Simplicidad, es un demostrador |
| Vector Store | Qdrant | Ligero, dockerizable, API REST |
| LLM (MVP) | LLM de pago externo (Claude / GPT-4o) | Azure no permite crear recursos de IA en la suscripción actual |
| LLM (futuro RAG) | Azure OpenAI | Previsto para indexación/búsqueda cuando se habilite |
| RAG Service | Python separado (futuro) | Ecosistema IA maduro, demuestra microservicios heterogéneos |
| Frontend | Next.js | TypeScript, SSR, ecosistema rico |
| Mobile | React Native Expo (futuro) | Reutiliza React/TS del frontend web |
| MCP | Python (futuro) | MCP integrado consumible por LLM de pago; antes se planteó como widget TypeScript para ChatGPT |
| Autenticación | JWT propio sencillo | Es un demostrador, no necesita OAuth complejo |
| Comunicación | HTTP directo | Simplicidad, sin message broker |
| K8s | Solo local (minikube/kind) | Demostración, no producción |

## Datos de Demo
- Seeds con 2-3 meses de datos fake
- Gastos, ingresos, empresas, valoraciones, cartera
- Para que el profesor pueda navegar y evaluar
