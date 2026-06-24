// ApiError es análogo a una excepción de dominio tipada en C#.
// Llevamos code + message + field para poder mostrar errores inline en formularios.
export class ApiError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly field?: string,
    public readonly status?: number,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

// Envelope que devuelve el backend en todas las respuestas.
// { data, errors[], meta } — igual al envelope pattern definido en AGENTS.md del backend.
interface Envelope<T> {
  data: T | null
  errors: Array<{ code: string; message: string; field?: string }>
  meta: unknown
}

// Opciones de configuración — inyectadas al crear el cliente.
// getToken y onUnauthorized se conectarán al store de auth en la Task 2.
interface ApiClientOptions {
  baseUrl: string
  getToken: () => string | null
  onUnauthorized: () => void
}

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

async function request<T>(
  options: ApiClientOptions,
  method: HttpMethod,
  path: string,
  body?: unknown,
): Promise<T> {
  const token = options.getToken()
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers['Authorization'] = `Bearer ${token}`

  let response: Response
  try {
    response = await fetch(`${options.baseUrl}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    })
  } catch {
    throw new ApiError('NETWORK_ERROR', 'Error de red — el servidor no responde')
  }

  if (response.status === 401) {
    options.onUnauthorized()
    throw new ApiError('UNAUTHORIZED', 'Sesión expirada', undefined, 401)
  }

  let envelope: Envelope<T>
  try {
    envelope = (await response.json()) as Envelope<T>
  } catch {
    throw new ApiError(
      'PARSE_ERROR',
      `Error inesperado del servidor (HTTP ${response.status})`,
      undefined,
      response.status,
    )
  }

  // Si el backend mandó errors[], lanzamos el primero (el más relevante).
  // En C# sería equivalente a lanzar la primera ValidationException de la lista.
  if (!response.ok || (envelope.errors && envelope.errors.length > 0)) {
    const err = envelope.errors?.[0]
    throw new ApiError(
      err?.code ?? 'SERVER_ERROR',
      err?.message ?? `Error del servidor (HTTP ${response.status})`,
      err?.field,
      response.status,
    )
  }

  return envelope.data as T
}

// Interfaz pública del cliente — métodos HTTP tipados.
export interface ApiClient {
  get<T>(path: string): Promise<T>
  post<T>(path: string, body: unknown): Promise<T>
  put<T>(path: string, body: unknown): Promise<T>
  patch<T>(path: string, body: unknown): Promise<T>
  delete<T>(path: string): Promise<T>
}

export function createApiClient(options: ApiClientOptions): ApiClient {
  return {
    get: <T>(path: string) => request<T>(options, 'GET', path),
    post: <T>(path: string, body: unknown) => request<T>(options, 'POST', path, body),
    put: <T>(path: string, body: unknown) => request<T>(options, 'PUT', path, body),
    patch: <T>(path: string, body: unknown) => request<T>(options, 'PATCH', path, body),
    delete: <T>(path: string) => request<T>(options, 'DELETE', path),
  }
}
