'use client'

import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { LineChart } from 'lucide-react'
import { RegisterForm } from '@/components/auth/register-form'

export default function RegisterPage() {
  const router = useRouter()

  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-background px-4 py-12">
      {/* Logo + volver */}
      <div className="mb-8 flex flex-col items-center gap-3">
        <Link href="/" className="flex items-center gap-2" aria-label="Volver a BigSchool">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <LineChart className="h-5 w-5" aria-hidden="true" />
          </span>
          <span className="font-heading text-xl font-semibold tracking-tight">BigSchool</span>
        </Link>
      </div>

      {/* Card con el formulario */}
      <div className="w-full max-w-sm rounded-xl border border-border bg-card p-8 shadow-sm">
        <RegisterForm
          onSwitchMode={() => router.push('/login')}
          onSuccess={() => {}}
        />
      </div>
    </main>
  )
}
