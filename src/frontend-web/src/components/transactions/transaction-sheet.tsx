'use client'

import { useEffect, useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { Trash2 } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { transactionSchema, type TransactionFormValues } from '@/lib/schemas/transaction'
import {
  useCreateTransaction, useUpdateTransaction, useDeleteTransaction,
} from '@/hooks/useTransactionMutations'
import { useCategories } from '@/hooks/useCategories'
import { useAuth } from '@/hooks/useAuth'
import { TRANSACTION_TYPE_LABEL, MAIN_CATEGORY_LABEL } from '@/lib/transactions/labels'
import type { Transaction } from '@/types/transactions'
import { CURRENCIES } from '@/types/enums'
import type { MainCategory, TransactionType } from '@/types/enums'

// ── Props ─────────────────────────────────────────────────────────────────────

interface TransactionSheetProps {
  open: boolean
  onClose: () => void
  transaction?: Transaction   // si se pasa → modo edición; si no → modo creación
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function todayISO(): string {
  return new Date().toISOString().split('T')[0]
}

// Campo de formulario reutilizable: label + control + error con espaciado consistente.
// Centraliza el "aire" vertical para que ningún input quede pegado a otro.
function Field({
  label, htmlFor, error, className, children,
}: {
  label: React.ReactNode
  htmlFor?: string
  error?: string
  className?: string
  children: React.ReactNode
}) {
  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  )
}

// ── Componente ────────────────────────────────────────────────────────────────

export function TransactionSheet({ open, onClose, transaction }: TransactionSheetProps) {
  const isEdit = Boolean(transaction)

  // user.currency estará disponible cuando el backend incluya currency en AuthResponseDto.
  // Hasta entonces será undefined y usamos 'EUR' como fallback.
  const { user } = useAuth()
  const defaultCurrency = user?.currency ?? 'EUR'

  const { data: categories = [] } = useCategories()

  const createMutation = useCreateTransaction()
  const updateMutation = useUpdateTransaction()
  const deleteMutation = useDeleteTransaction()

  // Estado local para la confirmación inline de borrado (evita necesitar AlertDialog)
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  const form = useForm<TransactionFormValues>({
    resolver: zodResolver(transactionSchema),
    defaultValues: transaction
      ? {
          type:            transaction.type,
          idMainCategory:  transaction.idMainCategory,
          idSubCategory:   transaction.idSubCategory,
          description:     transaction.description ?? '',
          transactionDate: transaction.transactionDate,
          amount:          transaction.originalAmount,
          currency:        transaction.originalCurrency,
        }
      : {
          type:            'Expense',
          idMainCategory:  'EssentialExpenses',
          transactionDate: todayISO(),
          currency:        defaultCurrency,
        },
  })

  const { watch, setValue, reset } = form
  const selectedType     = watch('type')
  const selectedCategory = watch('idMainCategory')

  // Resetear formulario cada vez que se abre el modal o cambia la transacción a editar.
  useEffect(() => {
    if (open) {
      reset(
        transaction
          ? {
              type:            transaction.type,
              idMainCategory:  transaction.idMainCategory,
              idSubCategory:   transaction.idSubCategory,
              description:     transaction.description ?? '',
              transactionDate: transaction.transactionDate,
              amount:          transaction.originalAmount,
              currency:        transaction.originalCurrency,
            }
          : {
              type:            'Expense',
              idMainCategory:  'EssentialExpenses',
              transactionDate: todayISO(),
              currency:        defaultCurrency,
            },
      )
      setConfirmingDelete(false)
    }
  }, [open, transaction, reset, defaultCurrency])

  // Al cambiar el tipo (Income ↔ Expense), resetear la categoría a su valor por defecto
  // para evitar que quede activa una categoría del tipo contrario.
  useEffect(() => {
    const defaults: Record<string, MainCategory> = {
      Expense: 'EssentialExpenses',
      Income:  'Salary',
    }
    setValue('idMainCategory', defaults[selectedType] as MainCategory)
    setValue('idSubCategory', null)
  }, [selectedType, setValue])

  // Categorías filtradas por tipo: Income → idMainCategory >= 10; Expense → < 10.
  const filteredCategories = categories.filter((c) =>
    selectedType === 'Expense' ? c.idMainCategory < 10 : c.idMainCategory >= 10
  )

  // category.name = mc.ToString() del backend → coincide con el string union MainCategory.
  const subCategories =
    categories.find((c) => c.name === selectedCategory)?.subCategories ?? []
  const hasSubCategories = subCategories.length > 0

  // ── Handlers ───────────────────────────────────────────────────────────────

  function onSubmit(values: TransactionFormValues) {
    const payload = {
      type:            values.type,
      idMainCategory:  values.idMainCategory,
      idSubCategory:   values.idSubCategory ?? null,
      description:     values.description || null,
      transactionDate: values.transactionDate,
      amount:          values.amount,
      currency:        values.currency,
    }

    if (isEdit && transaction) {
      updateMutation.mutate(
        { id: transaction.idTransaction, data: payload },
        {
          onSuccess: () => { toast.success('Transacción actualizada'); onClose() },
          onError:   (e) => toast.error(e instanceof Error ? e.message : 'Error al actualizar'),
        },
      )
    } else {
      createMutation.mutate(payload, {
        onSuccess: () => { toast.success('Transacción creada'); onClose() },
        onError:   (e) => toast.error(e instanceof Error ? e.message : 'Error al crear'),
      })
    }
  }

  function handleDelete() {
    if (!transaction) return
    deleteMutation.mutate(transaction.idTransaction, {
      onSuccess: () => { toast.success('Transacción eliminada'); onClose() },
      onError:   (e) => toast.error(e instanceof Error ? e.message : 'Error al eliminar'),
    })
  }

  const isBusy =
    createMutation.isPending || updateMutation.isPending || deleteMutation.isPending

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto p-6 sm:max-w-md">
        <DialogHeader className="mb-2">
          <DialogTitle>
            {isEdit ? 'Editar transacción' : 'Nueva transacción'}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">

          {/* Tipo — ancho completo (gobierna las categorías disponibles) */}
          {/* Los 4 <Select> de este formulario llevan modal={false}: Select es modal por
              defecto (bloquea scroll + interacción fuera de él); anidado dentro de este
              Dialog (también modal) provocaba un parpadeo al cerrarse el Select — su propio
              desbloqueo de scroll pisaba momentáneamente el del Dialog padre, más visible en
              móvil. El Dialog ya bloquea la interacción exterior. */}
          <Field label="Tipo" htmlFor="type">
            <Controller
              control={form.control}
              name="type"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange} modal={false}>
                  <SelectTrigger id="type" className="w-full" data-testid="select-type">
                    <SelectValue>
                      {(v: TransactionType) => TRANSACTION_TYPE_LABEL[v]}
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent alignItemWithTrigger={false}>
                    <SelectItem value="Expense">{TRANSACTION_TYPE_LABEL.Expense}</SelectItem>
                    <SelectItem value="Income">{TRANSACTION_TYPE_LABEL.Income}</SelectItem>
                  </SelectContent>
                </Select>
              )}
            />
          </Field>

          {/* Fila: Fecha | Importe */}
          <div className="grid grid-cols-2 gap-3">
            <Field label="Fecha" htmlFor="transactionDate"
              error={form.formState.errors.transactionDate?.message}>
              <Input
                id="transactionDate"
                type="date"
                data-testid="input-date"
                {...form.register('transactionDate')}
              />
            </Field>

            <Field label="Importe" htmlFor="amount"
              error={form.formState.errors.amount?.message}>
              <Input
                id="amount"
                type="number"
                step="0.01"
                min="0.01"
                placeholder="0,00"
                data-testid="input-amount"
                {...form.register('amount', { valueAsNumber: true })}
              />
            </Field>
          </div>

          {/* Fila: Categoría | Subcategoría (subcategoría solo si la categoría tiene) */}
          <div className="grid grid-cols-2 gap-3">
            <Field
              label="Categoría"
              htmlFor="idMainCategory"
              error={form.formState.errors.idMainCategory?.message}
              className={!hasSubCategories ? 'col-span-2' : undefined}
            >
              <Controller
                control={form.control}
                name="idMainCategory"
                render={({ field }) => (
                  <Select
                    value={field.value}
                    onValueChange={(v) => {
                      field.onChange(v)
                      setValue('idSubCategory', null)
                    }}
                    modal={false}
                  >
                    <SelectTrigger id="idMainCategory" className="w-full" data-testid="select-category">
                      <SelectValue>
                        {(v: MainCategory) => MAIN_CATEGORY_LABEL[v] ?? v}
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent alignItemWithTrigger={false}>
                      {filteredCategories.map((c) => (
                        <SelectItem key={c.idMainCategory} value={c.name}>
                          {MAIN_CATEGORY_LABEL[c.name as MainCategory] ?? c.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </Field>

            {hasSubCategories && (
              <Field
                label={<>Subcat.{' '}<span className="text-muted-foreground">(opc.)</span></>}
                htmlFor="idSubCategory"
              >
                <Controller
                  control={form.control}
                  name="idSubCategory"
                  render={({ field }) => (
                    <Select
                      value={field.value != null ? String(field.value) : '__none__'}
                      onValueChange={(v) =>
                        field.onChange(v === '__none__' ? null : Number(v))
                      }
                      modal={false}
                    >
                      <SelectTrigger id="idSubCategory" className="w-full">
                        <SelectValue placeholder="Ninguna">
                          {(v: string) =>
                            v === '__none__'
                              ? 'Ninguna'
                              : (subCategories.find((s) => String(s.idSubCategory) === v)?.name ?? 'Ninguna')
                          }
                        </SelectValue>
                      </SelectTrigger>
                      <SelectContent alignItemWithTrigger={false}>
                        <SelectItem value="__none__">Ninguna</SelectItem>
                        {subCategories.map((sc) => (
                          <SelectItem key={sc.idSubCategory} value={String(sc.idSubCategory)}>
                            {sc.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
              </Field>
            )}
          </div>

          {/* Moneda — ancho completo */}
          <Field label="Moneda" htmlFor="currency">
            <Controller
              control={form.control}
              name="currency"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange} modal={false}>
                  <SelectTrigger id="currency" className="w-full" data-testid="select-currency">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent alignItemWithTrigger={false}>
                    {CURRENCIES.map((c) => (
                      <SelectItem key={c} value={c}>{c}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </Field>

          {/* Descripción — ancho completo */}
          <Field
            label={<>Descripción{' '}<span className="text-muted-foreground">(opcional)</span></>}
            htmlFor="description"
          >
            <Textarea
              id="description"
              placeholder="Descripción..."
              rows={3}
              className="resize-none overflow-y-auto"
              data-testid="input-description"
              {...form.register('description')}
            />
          </Field>

          {/* Acciones */}
          <div className="flex flex-col gap-2 pt-2">
            <Button
              type="submit"
              disabled={isBusy}
              className="w-full"
              data-testid="btn-submit"
            >
              {isEdit ? 'Guardar cambios' : 'Crear transacción'}
            </Button>

            {/* Borrar con confirmación inline */}
            {isEdit && !confirmingDelete && (
              <Button
                type="button"
                variant="ghost"
                className="w-full text-destructive hover:text-destructive"
                disabled={isBusy}
                onClick={() => setConfirmingDelete(true)}
                data-testid="btn-delete"
              >
                <Trash2 className="mr-2 h-4 w-4" />
                Eliminar
              </Button>
            )}

            {isEdit && confirmingDelete && (
              <div className="flex flex-col gap-2 rounded-lg border border-destructive/30 bg-destructive/5 p-3">
                <p className="text-center text-sm text-destructive">
                  ¿Seguro? Esta acción no se puede deshacer.
                </p>
                <div className="flex gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    className="flex-1"
                    onClick={() => setConfirmingDelete(false)}
                    disabled={isBusy}
                  >
                    Cancelar
                  </Button>
                  <Button
                    type="button"
                    variant="destructive"
                    className="flex-1"
                    onClick={handleDelete}
                    disabled={isBusy}
                    data-testid="btn-confirm-delete"
                  >
                    Sí, eliminar
                  </Button>
                </div>
              </div>
            )}
          </div>

        </form>
      </DialogContent>
    </Dialog>
  )
}
