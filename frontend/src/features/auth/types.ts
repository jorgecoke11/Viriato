export interface UserDto {
  id: string
  email: string
  displayName: string
  baseCurrency: string
  timeZone: string
  locale: string
  roles: string[]
  permissions: string[]
  createdAt: string
}

export interface AuthResponse {
  accessToken: string
  expiresIn: number
  user: UserDto
}
