export interface LoginDto {
  email: string
  password: string
}

export interface RegisterDto {
  fullName: string
  email: string
  password: string
}

export interface AuthResponse {
  token: string
  refreshToken: string
  expiresIn: number
  user: UserProfile
}

export interface UserProfile {
  idUser: number
  fullName: string
  email: string
  baseCurrency: string
}
