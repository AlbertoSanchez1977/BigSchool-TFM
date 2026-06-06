# BigSchool-TFM — Visión del Proyecto

**Fecha**: 2026-06-06
**Autor**: Alberto Sánchez

## Objetivo

Desarrollo de una aplicación financiera personal como Trabajo de Fin de Máster que demuestre los conocimientos adquiridos durante el programa:

- Principios SOLID, DDD, CQRS
- Arquitectura del Software (Clean Architecture, microservicios)
- Fundamentos e integración de IA (LLM, RAG)
- Bases de Datos (relacional + vectorial)
- Docker y Kubernetes
- Desarrollo potenciado por IA

## Módulos

1. **Backend API** — .NET 8, Clean Architecture, CQRS, DDD, MySQL
2. **Frontend Web** — Next.js, TypeScript, Tailwind CSS
3. **Mobile** — React Native + Expo (solo lectura)
4. **MCP Server** — TypeScript/Node.js
5. **RAG Service** — Python, FastAPI, LangChain, Azure OpenAI, Qdrant
6. **Infraestructura** — Docker Compose + Kubernetes local

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
- Chat conversacional con contexto RAG
- Escaneo de empresas según criterios configurables
- Gestión del contexto RAG por el usuario (subir/eliminar documentos)

### Mobile
- Visualización de balance y gráficas (solo lectura)

### MCP
- Exposición de datos financieros como herramientas para agentes IA

## Decisiones de Arquitectura

| Decisión | Elección | Motivo |
|----------|----------|--------|
| Monorepo | Sí | Facilita entrega y coherencia del TFM |
| BD | MySQL única | Simplicidad, es un demostrador |
| Vector Store | Qdrant | Ligero, dockerizable, API REST |
| LLM | Azure OpenAI | Acceso corporativo/educativo |
| RAG Service | Python separado | Ecosistema IA maduro, demuestra microservicios heterogéneos |
| Frontend | Next.js | TypeScript, SSR, ecosistema rico |
| Mobile | React Native Expo | Reutiliza React/TS del frontend web |
| MCP | TypeScript | Más ligero que .NET para MCP servers |
| Autenticación | JWT propio sencillo | Es un demostrador, no necesita OAuth complejo |
| Comunicación | HTTP directo | Simplicidad, sin message broker |
| K8s | Solo local (minikube/kind) | Demostración, no producción |

## Datos de Demo
- Seeds con 2-3 meses de datos fake
- Gastos, ingresos, empresas, valoraciones, cartera
- Para que el profesor pueda navegar y evaluar
