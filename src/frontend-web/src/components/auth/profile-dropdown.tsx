'use client'

import { useRouter } from 'next/navigation'
import { LogOut, UserPen } from 'lucide-react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useAuth } from '@/hooks/useAuth'

// Extrae las dos primeras iniciales del nombre completo: "Alberto Sánchez" → "AS"
function getInitials(fullName: string): string {
  return fullName
    .split(' ')
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? '')
    .join('')
}

export function ProfileDropdown() {
  const { user, logout } = useAuth()
  const router = useRouter()

  if (!user) return null

  const initials = getInitials(user.fullName)

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label={user.fullName}
        className="rounded-full outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <Avatar>
          <AvatarFallback className="bg-primary text-primary-foreground text-xs font-semibold">
            {initials}
          </AvatarFallback>
        </Avatar>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" sideOffset={8}>
        {/* Cabecera con nombre y email — DropdownMenuLabel requiere DropdownMenuGroup padre */}
        <DropdownMenuGroup>
          <DropdownMenuLabel className="flex flex-col gap-0.5 px-2 py-1.5">
            <span className="text-sm font-semibold text-foreground">{user.fullName}</span>
            <span className="text-xs font-normal text-muted-foreground">{user.email}</span>
          </DropdownMenuLabel>
        </DropdownMenuGroup>

        <DropdownMenuSeparator />

        <DropdownMenuItem onClick={() => router.push('/profile')}>
          <UserPen className="mr-2 h-4 w-4" aria-hidden="true" />
          Editar perfil
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        <DropdownMenuItem variant="destructive" onClick={logout}>
          <LogOut className="mr-2 h-4 w-4" aria-hidden="true" />
          Cerrar sesión
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
