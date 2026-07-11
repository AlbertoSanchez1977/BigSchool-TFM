import type { Currency } from './enums'

// GET /users/me → UserProfileDto (Dapper: BaseCurrency es `string` en el DTO C#,
// pero el valor siempre es un código de moneda válido — se tipa como Currency en TS).
export interface UserProfile {
  idUser: number
  email: string
  fullName: string
  baseCurrency: Currency
  lastLoginDate: string | null // ISO, null si nunca ha iniciado sesión
}

// PUT /users/me (body) — UpdateUserRequest del controller.
// email y baseCurrency son inmutables tras el registro: no viajan aquí.
export interface UpdateUserDto {
  fullName: string
  password?: string | null // null/omitido = no cambiar contraseña
}
