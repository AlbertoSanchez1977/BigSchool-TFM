'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { useRenamePortfolio, useDeletePortfolio } from '@/hooks/usePortfolioMutations'
import { ApiError } from '@/lib/apiClient'

// Modales de "Renombrar" y "Eliminar" cartera — compartidos entre el listado
// (investments/page.tsx, con iconos por fila) y el detalle (investments/[id]/page.tsx,
// menú ⋯ de la cabecera). Un solo componente, dos puntos de entrada.

const renamePortfolioSchema = z.object({
  name: z.string().min(1, 'El nombre es obligatorio').max(100, 'Máximo 100 caracteres'),
})
type RenamePortfolioForm = z.infer<typeof renamePortfolioSchema>

// ── Modal: Renombrar cartera ──────────────────────────────────────────────────

export function RenamePortfolioModal({
  portfolioId, currentName, open, onClose,
}: { portfolioId: number; currentName: string; open: boolean; onClose: () => void }) {
  const renameMutation = useRenamePortfolio(portfolioId)

  const form = useForm<RenamePortfolioForm>({
    resolver: zodResolver(renamePortfolioSchema),
    defaultValues: { name: currentName },
  })

  function onSubmit(values: RenamePortfolioForm) {
    renameMutation.mutate(values, {
      onSuccess: () => { toast.success('Cartera renombrada'); onClose() },
      onError:   (e) => toast.error(e instanceof ApiError ? e.message : 'Error al renombrar'),
    })
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="p-6 sm:max-w-sm">
        <DialogHeader className="mb-4">
          <DialogTitle>Renombrar cartera</DialogTitle>
        </DialogHeader>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="rename-portfolio-name">Nombre</Label>
            <Input
              id="rename-portfolio-name"
              data-testid="input-rename-portfolio"
              {...form.register('name')}
            />
            {form.formState.errors.name && (
              <p className="text-xs text-destructive">{form.formState.errors.name.message}</p>
            )}
          </div>
          <Button type="submit" className="w-full" disabled={renameMutation.isPending}
            data-testid="btn-confirm-rename">
            {renameMutation.isPending ? 'Guardando…' : 'Guardar'}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Modal: Eliminar cartera (confirmación) ────────────────────────────────────
// El backend aplica un guard fiscal: 409 si la cartera tiene holdings abiertos.
// Ese mensaje llega tal cual en ApiError.message y se muestra sin reinterpretarlo.
//
// onDeleted es opcional porque el comportamiento post-borrado difiere según quién
// invoca el modal: el detalle navega a /investments (ya no hay nada que mostrar),
// el listado simplemente se queda donde está (la lista se refresca sola vía
// invalidación de query).

export function DeletePortfolioModal({
  portfolioId, open, onClose, onDeleted,
}: { portfolioId: number; open: boolean; onClose: () => void; onDeleted?: () => void }) {
  const deleteMutation = useDeletePortfolio()

  function handleDelete() {
    deleteMutation.mutate(portfolioId, {
      onSuccess: () => {
        toast.success('Cartera eliminada')
        onClose()
        onDeleted?.()
      },
      onError: (e) => toast.error(e instanceof ApiError ? e.message : 'Error al eliminar la cartera'),
    })
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="p-6 sm:max-w-sm">
        <DialogHeader className="mb-2">
          <DialogTitle>Eliminar cartera</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">
          ¿Eliminar esta cartera? Esta acción no se puede deshacer. Si tiene holdings abiertos,
          el backend rechazará el borrado.
        </p>
        <div className="mt-4 flex gap-2">
          <Button type="button" variant="outline" className="flex-1" onClick={onClose}
            disabled={deleteMutation.isPending}>
            Cancelar
          </Button>
          <Button type="button" variant="destructive" className="flex-1" onClick={handleDelete}
            disabled={deleteMutation.isPending} data-testid="btn-confirm-delete-portfolio">
            {deleteMutation.isPending ? 'Eliminando…' : 'Sí, eliminar'}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}
