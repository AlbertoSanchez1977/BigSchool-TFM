import { api } from '@/lib/api'
import type { UserProfile, UpdateUserDto } from '@/types/users'

export const userService = {
  me: () => api.get<UserProfile>('/users/me'),
  update: (data: UpdateUserDto) => api.put<UserProfile>('/users/me', data),
}
