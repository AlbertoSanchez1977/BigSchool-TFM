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

## ADR-006: Soporte multimoneda con moneda base por usuario

**Fecha**: 2026-06-17
**Estado**: Aceptado

**Contexto**: El modelo inicial era monomoneda implícita: las transacciones no tenían moneda y las inversiones no contemplaban conversión. En el BC de Inversiones (Plan 3) las empresas cotizan en distintas monedas y cada valoración es un punto temporal con su propio tipo de cambio. Sin un modelo multimoneda, `Transaction` nacería monomoneda y habría que rehacerlo.

**Decisión**: Introducir soporte multimoneda transversal desde el Plan 2 (BC Finanzas Personales):

- **Moneda base por usuario** (`Users.BaseCurrency`, EUR por defecto). Balances y carteras se consolidan en ella.
- **Value Objects de dominio reutilizables**: `Currency` (enum), `Money` (importe + moneda) y `MoneyConversion` (snapshot: original + tipo + convertido + fecha).
- **Conversión por snapshot** en el momento del registro: se persiste el importe original, su moneda, el tipo aplicado, el importe convertido a base y la fecha del tipo. El balance histórico no cambia aunque cambien los tipos.
- **Tipos de cambio vía API externa** (Frankfurter/ECB) encapsulada en un anti-corruption layer (`ExchangeRateApiClient`), con tabla cache `ExchangeRates`: EF posee el esquema, el cliente lee/escribe con Dapper (UPSERT) desacoplado del `UnitOfWork` de negocio. El dominio recibe el tipo ya resuelto y **no realiza llamadas externas**.

**Consecuencias**:
- (+) `Transaction`, `Holding` y `Valuation` comparten el mismo modelo `Money`/`MoneyConversion`.
- (+) Balances y valoraciones consolidados en la moneda base del usuario; auditables.
- (+) Determinismo y resiliencia: el cache permite seeds reproducibles y operar sin red.
- (+) Dominio puro (sin dependencias HTTP); la llamada externa vive en Application/Infrastructure.
- (-) Más columnas por fila (snapshot) y una tabla/servicio adicionales.
- (-) Dependencia de una API externa de tipos (mitigada por el cache y un fallback).

**Alternativas descartadas**:
- Guardar solo el importe convertido a base (pierde el importe/moneda original).
- Conversión on-the-fly en queries (el balance histórico cambiaría al cambiar los tipos).
- Moneda base global única (no permite usuarios con bases distintas).
- Tipos introducidos manualmente por el usuario (carga al usuario, menos realista).

**Referencia**: `docs/superpowers/specs/2026-06-17-backend-finanzas-multicurrency-design.md`

---

## ADR-007: BC Inversiones — value investing multimoneda (Plan 3B)

**Fecha**: 2026-06-21
**Estado**: Aceptado

**Contexto**: El BC Inversiones necesita modelar una cartera por usuario con posiciones en empresas que cotizan en distintas monedas, soporte para ventas FIFO (obligatorio IRPF) y cálculo de performance realizada/no realizada en la moneda base del usuario.

**Decisión**: Tres entidades en un único agregado jerárquico de tres niveles:

- **`Portfolio` (AR por-usuario)** → **`Holding` (lote de compra)** → **`Disposal` (venta)**.
- **Catálogo global** (`Company`/`Valuation`) separado del agregado por-usuario: `Valuation.Price` es `Money` en moneda de la empresa; la conversión a base de usuario se realiza en las queries (no se snapshotea en el catálogo).
- **Modelo de compra**: `AddHolding` registra un nuevo lote siempre (recompra ≠ promedio); `AvgBuyPrice` es un `MoneyConversion` snapshot congelado a `BuyDate`.
- **Venta FIFO obligatoria** a nivel `(Portfolio, Company)`: `SellShares` ordena lotes por `BuyDate asc, IdHolding asc` y genera un `Disposal` por lote tocado. Motivo: obligación legal IRPF en España (criterio FIFO para cálculo de plusvalías).
- **`SellPrice` como `MoneyConversion` snapshot** congelado a `SellDate`: igual que `AvgBuyPrice`, garantiza que el cálculo de `RealizedPnL` por disposal sea inmutable e independiente de variaciones posteriores de tipos.
- **`RealizedPnL` persistido en `Portfolio`**: columna acumulada actualizada por el agregado en cada `SellShares` y revertida en `DeleteHolding`. Evita recalcular desde `Disposals` en tiempo de query; es el valor canónico.
- **Hijas/nietas accedidas solo a través del AR**: sin `InternalsVisibleTo`; `Holding.Create` y `Disposal.Create` son `internal`; los tests del agregado usan únicamente la API pública de `Portfolio`. Consecuencia: el AR debe cargar `Holdings` + `Disposals` en el mismo query (`GetByIdWithHoldingsAsync`).
- **Performance consolidada en base** mediante query Dapper con fallback de tipo (`HOLDING_VALUATION` fragment): última valoración de la empresa convertida al tipo cuya fecha ≤ fecha de valoración.

**Consecuencias**:
- (+) Invariantes FIFO y de realizado garantizados por el AR; imposible crear `Disposal` fuera del flujo `SellShares`.
- (+) `RealizedPnL` siempre consistente; sin posibilidad de inconsistencia entre columna y suma de disposals.
- (+) Sin `InternalsVisibleTo` — tests del agregado son verdaderamente de caja negra.
- (+) Snapshot multimoneda: ni el `AvgBuyPrice` ni el `SellPrice` cambian si cambian los tipos de cambio históricos.
- (-) El AR debe cargar todos los `Holdings` + `Disposals` activos para ejecutar FIFO — aceptable para colecciones pequeñas por usuario.
- (-) Bug de Pomelo MySQL con shared owned-entity instances en batch INSERT (workaround: `Money.Create` fresco por disposal en el loop FIFO).

**Alternativas descartadas**:
- FIFO calculado en query (sin persistir `RealizedPnL`): recalcular en tiempo real complica las queries y rompe el modelo de snapshot.
- `Holding` como AR independiente (sin `Portfolio` como padre): pierde el control de invariantes FIFO y la consolidación del realizado.
- Promediar precio en recompra (PEPS promediado): no es el criterio IRPF obligatorio en España.

---

*Añadir nuevas decisiones al final del documento siguiendo el mismo formato.*
