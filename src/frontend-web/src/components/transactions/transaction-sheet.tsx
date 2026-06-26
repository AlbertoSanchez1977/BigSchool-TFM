'use client'

import { useEffect, useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { Trash2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select'
import {
  Sheet, SheetContent, SheetHeader, SheetTitle, SheetFooter,
} from '@/components/ui/sheet'
import { transactionSchema, type TransactionFormValues } from '@/lib/schemas/transaction'
import {
  useCreateTransaction, useUpdateTransaction, useDeleteTransaction,
} from '@/hooks/useTransactionMutations'
import { useCategories } from '@/hooks/useCategories'
import { useAuth } from '@/hooks/useAuth'
import { TRANSACTION_TYPE_LABEL, MAIN_CATEGORY_LABEL } from '@/lib/transactions/labels'
import type { Transaction } from '@/types/transactions'
import type { MainCategory } from '@/types/enums'

// ── Props ─────────────────────────────────────────────────────────────────────

interface TransactionSheetProps {
  open: boolean
  onClose: () => void
  transaction?: Transaction   // si se pasa → modo edición; si no → modo creación
}

// ── Helpers ───────────────────────────────────────────────────────────────────

const CURRENCIES = ['EUR', 'USD', 'GBP', 'CHF', 'JPY'] as const

function todayISO(): string {
  return new Date().toISOString().split('T')[0]
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

  // Resetear formulario cada vez que se abre el sheet o cambia la transacción a editar.
  // El useEffect se dispara cuando open cambia a true — equivale al OnParametersSet de Blazor.
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
  // El backend asigna 1-7 a gastos y 10-13 a ingresos (ver CATEGORY_MAP en transactionService).
  const filteredCategories = categories.filter((c) =>
    selectedType === 'Expense' ? c.idMainCategory < 10 : c.idMainCategory >= 10
  )

  // category.name = mc.ToString() del backend → coincide con el string union MainCategory.
  const subCategories =
    categories.find((c) => c.name === selectedCategory)?.subCategories ?? []

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
    <Sheet open={open} onOpenChange={(v) => !v && onClose()}>
      <SheetContent className="w-full overflow-y-auto sm:max-w-md">
        <SheetHeader className="mb-6">
          <SheetTitle>
            {isEdit ? 'Editar transacción' : 'Nueva transacción'}
          </SheetTitle>
        </SheetHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-5">

          {/* Tipo */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="type">Tipo</Label>
            <Controller
              control={form.control}
              name="type"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger id="type" data-testid="select-type">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Expense">{TRANSACTION_TYPE_LABEL.Expense}</SelectItem>
                    <SelectItem value="Income">{TRANSACTION_TYPE_LABEL.Income}</SelectItem>
                  </SelectContent>
                </Select>
              )}
            />
          </div>

          {/* Categoría principal */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="idMainCategory">Categoría</Label>
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
                >
                  <SelectTrigger id="idMainCategory" data-testid="select-category">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {filteredCategories.map((c) => (
                      <SelectItem key={c.idMainCategory} value={c.name}>
                        {MAIN_CATEGORY_LABEL[c.name as MainCategory] ?? c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {form.formState.errors.idMainCategory && (
              <p className="text-xs text-destructive">
                {form.formState.errors.idMainCategory.message}
              </p>
            )}
          </div>

          {/* Subcategoría — solo si la categoría tiene subcategorías */}
          {subCategories.length > 0 && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="idSubCategory">
                Subcategoría{' '}
                <span className="text-muted-foreground">(opcional)</span>
              </Label>
              <Controller
                control={form.control}
                name="idSubCategory"
                render={({ field }) => (
                  <Select
                    value={field.value != null ? String(field.value) : '__none__'}
                    onValueChange={(v) =>
                      field.onChange(v === '__none__' ? null : Number(v))
                    }
                  >
                    <SelectTrigger id="idSubCategory">
                      <SelectValue placeholder="Sin subcategoría" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none__">Sin subcategoría</SelectItem>
                      {subCategories.map((sc) => (
                        <SelectItem key={sc.idSubCategory} value={String(sc.idSubCategory)}>
                          {sc.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
          )}

          {/* Fecha */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="transactionDate">Fecha</Label>
            <Input
              id="transactionDate"
              type="date"
              data-testid="input-date"
              {...form.register('transactionDate')}
            />
            {form.formState.errors.transactionDate && (
              <p className="text-xs text-destructive">
                {form.formState.errors.transactionDate.message}
              </p>
            )}
          </div>

          {/* Importe */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="amount">Importe</Label>
            <Input
              id="amount"
              type="number"
              step="0.01"
              min="0.01"
              placeholder="0,00"
              data-testid="input-amount"
              {...form.register('amount', { valueAsNumber: true })}
            />
            {form.formState.errors.amount && (
              <p className="text-xs text-destructive">
                {form.formState.errors.amount.message}
              </p>
            )}
          </div>

          {/* Moneda */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="currency">Moneda</Label>
            <Controller
              control={form.control}
              name="currency"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger id="currency" data-testid="select-currency">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CURRENCIES.map((c) => (
                      <SelectItem key={c} value={c}>{c}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </div>

          {/* Descripción */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="description">
              Descripción{' '}
              <span className="text-muted-foreground">(opcional)</span>
            </Label>
            <Input
              id="description"
              placeholder="Descripción..."
              data-testid="input-description"
              {...form.register('description')}
            />
          </div>

          {/* Footer */}
          <SheetFooter className="mt-2 flex-col gap-2 sm:flex-col">
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
          </SheetFooter>

        </form>
      </SheetContent>
    </Sheet>
  )
}
