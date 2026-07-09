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
