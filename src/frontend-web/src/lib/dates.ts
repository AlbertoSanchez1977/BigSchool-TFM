// Utilidades de fecha centralizadas. Las fechas de negocio del backend viajan como
// 'YYYY-MM-DD' (DateOnly) y las de auditoría como timestamps UTC (DateTime.UtcNow).

// Fecha de hoy en 'YYYY-MM-DD' según la zona LOCAL del navegador (no UTC — evita el
// desfase de un día cerca de medianoche que tiene `toISOString()`).
export function todayISO(today = new Date()): string {
  const y = today.getFullYear()
  const m = String(today.getMonth() + 1).padStart(2, '0')
  const d = String(today.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

// Fecha por defecto para "Nueva transacción" según el mes/año visible en la parrilla:
// - si el periodo visible es el mes/año actual → hoy (comportamiento previo).
// - si es otro mes → día 1 de ese mes, para que la transacción nueva caiga DENTRO del
//   filtro visible y no "desaparezca".
export function defaultTransactionDate(
  viewedYear: number,
  viewedMonth: number,
  today = new Date(),
): string {
  const isCurrentMonth =
    viewedYear === today.getFullYear() && viewedMonth === today.getMonth() + 1
  if (isCurrentMonth) return todayISO(today)
  const m = String(viewedMonth).padStart(2, '0')
  return `${viewedYear}-${m}-01`
}

// True si la fecha 'YYYY-MM-DD' NO es futura (hoy o anterior). Comparación lexicográfica,
// válida para el formato ISO fecha. `today` inyectable para tests deterministas.
export function isNotFuture(iso: string, today = new Date()): boolean {
  return iso <= todayISO(today)
}

// Formatea un timestamp UTC del backend (DateTime.UtcNow, serializado a veces SIN sufijo Z
// desde MySQL datetime) mostrando la hora de pared en UTC y etiquetándola. NO convierte a la
// zona local: solución simple y sin ambigüedad para "Último acceso", emails y contactos.
export function formatDateTimeUtc(iso: string): string {
  // Sin 'Z', `new Date` interpretaría el string como hora LOCAL (origen del bug). Lo forzamos a UTC.
  const utc = iso.endsWith('Z') ? iso : `${iso}Z`
  const formatted = new Intl.DateTimeFormat('es-ES', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
    timeZone: 'UTC',
  }).format(new Date(utc))
  return `${formatted} UTC`
}
