# AGENTS.md — RAG Service

## Tecnología
- Python 3.11+
- FastAPI como framework web
- LangChain para orquestación de RAG
- Azure OpenAI (embeddings + chat completion)
- Qdrant como vector store
- Pydantic para validación de datos
- uvicorn como servidor ASGI

## Estructura del Proyecto

```
rag-service/
├── app/
│   ├── main.py             → Punto de entrada FastAPI
│   ├── api/
│   │   ├── routes/
│   │   │   ├── chat.py     → Endpoints de conversación
│   │   │   └── documents.py → Gestión de documentos RAG
│   │   └── dependencies.py → Inyección de dependencias
│   ├── core/
│   │   ├── config.py       → Configuración (env vars)
│   │   └── rag_engine.py   → Motor RAG principal
│   ├── models/
│   │   ├── schemas.py      → Pydantic models (request/response)
│   │   └── documents.py    → Modelos de documentos
│   ├── services/
│   │   ├── embedding.py    → Servicio de embeddings (Azure OpenAI)
│   │   ├── vector_store.py → Cliente Qdrant
│   │   └── llm.py         → Cliente Azure OpenAI chat
│   └── utils/
│       └── text_splitter.py → Chunking de documentos
├── tests/
├── requirements.txt
├── Dockerfile
└── README.md
```

## Endpoints API

### Chat
- `POST /api/chat` — Enviar mensaje, recibir respuesta con contexto RAG
- `GET /api/chat/history` — Historial de conversaciones (opcional)

### Documentos (Gestión del RAG)
- `POST /api/documents` — Subir documento al vector store
- `GET /api/documents` — Listar documentos indexados
- `DELETE /api/documents/{id}` — Eliminar documento del contexto
- `POST /api/documents/reindex` — Reindexar todos los documentos

## Flujo del RAG

1. Usuario envía pregunta (ej: "¿Qué empresas del sector tech tienen buen PER?")
2. Se genera embedding de la pregunta con Azure OpenAI
3. Se buscan chunks relevantes en Qdrant (similarity search)
4. Se construye prompt con contexto recuperado
5. Se envía a Azure OpenAI (chat completion)
6. Se devuelve respuesta al usuario

## Contexto del RAG

El usuario puede cargar documentos que forman el "conocimiento" del sistema:
- Criterios de inversión personales
- Análisis de sectores
- Ratios financieros de referencia
- Artículos de webs especializadas
- Notas personales sobre empresas

Formatos soportados: PDF, TXT, Markdown (mínimo viable)

## Convenciones de Código

### Nombrado
- Módulos y funciones: snake_case (`vector_store.py`, `get_relevant_chunks`)
- Clases: PascalCase (`RagEngine`, `DocumentSchema`)
- Constantes: UPPER_SNAKE_CASE (`MAX_CHUNK_SIZE`)

### Estilo
- Type hints obligatorios en todas las funciones
- Docstrings en funciones públicas
- Pydantic models para toda entrada/salida de API
- Async/await en endpoints y operaciones IO

### Testing
- pytest + httpx (TestClient de FastAPI)
- Mocking de Azure OpenAI y Qdrant en tests unitarios
- Tests de integración con Qdrant en Docker (Testcontainers)

## Instrucciones para el Agente

1. No reinventar la rueda — usar LangChain para la orquestación
2. Configuración por variables de entorno (nunca hardcodear API keys)
3. El servicio debe ser stateless (estado en Qdrant y Azure)
4. Chunks de 500-1000 tokens con overlap de 100 tokens (ajustable)
5. Implementar rate limiting básico para llamadas a Azure OpenAI
6. Logs claros para debugging del pipeline RAG
7. El desarrollador tiene experiencia baja en Python — mantener código simple y legible
