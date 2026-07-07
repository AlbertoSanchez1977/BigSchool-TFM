import { test, expect, type Page } from '@playwright/test'

/**
 * E2E hito Task 6 — Crear un gasto y verificar que aparece en la lista.
 *
 * Prerequisito: backend en marcha en http://localhost:5285
 * Playwright abre el dev server de Next.js automáticamente (playwright.config.ts).
 *
 * Flujo:
 *  1. Registrar un usuario nuevo (email único para no colisionar con runs anteriores).
 *  2. Navegar a /expenses.
 *  3. Abrir el Sheet con "Nueva transacción".
 *  4. Rellenar tipo, categoría, fecha, importe.
 *  5. Enviar y verificar que el toast de éxito aparece.
 *  6. Verificar que la tabla contiene al menos una fila (tx-row).
 */

const uniqueEmail = () => `e2e+expense+${Date.now()}@bigschool-test.com`
const PASSWORD    = 'TestPass123!'
const FULL_NAME   = 'E2E Expense User'

// Helper: clic en el botón del navbar (evita strict-mode en páginas con múltiples botones)
const navbar = (page: Page) => page.getByRole('navigation')

// Helper: registrar y quedar autenticado
async function registerAndLogin(page: Page, email: string) {
  await page.goto('/')
  await navbar(page).getByRole('button', { name: 'Registrarse' }).click()
  await page.getByLabel('Nombre completo').fill(FULL_NAME)
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.getByLabel('Contraseña', { exact: true }).fill(PASSWORD)
  await page.getByLabel('Repetir contraseña').fill(PASSWORD)
  await page.getByTestId('select-baseCurrency').click()
  await page.getByRole('option', { name: 'EUR' }).click()
  await page.getByRole('button', { name: 'Crear cuenta' }).click()
  // Esperar redirección post-registro al dashboard
  await page.waitForURL('**/dashboard', { timeout: 10_000 })
}

test.describe('Gastos/Ingresos — CRUD (Task 6)', () => {

  test('crear un gasto y verificar que aparece en la lista', async ({ page }) => {
    const email = uniqueEmail()

    // 1. Autenticación
    await registerAndLogin(page, email)

    // 2. Navegar a /expenses
    await page.goto('/expenses')
    await expect(page.getByRole('heading', { name: 'Gastos e ingresos' })).toBeVisible()

    // 3. Abrir el Sheet
    await page.getByTestId('btn-nueva-transaccion').click()
    await expect(page.getByRole('heading', { name: 'Nueva transacción' })).toBeVisible()

    // 4. Rellenar el formulario
    // Tipo: Gasto (valor por defecto, no hace falta cambiarlo)
    // Categoría: primer valor disponible del Select de categorías
    // Si las categorías cargaron, el select ya debería tener "Gastos esenciales" por defecto.

    // Fecha: hoy
    const today = new Date().toISOString().split('T')[0]
    await page.getByTestId('input-date').fill(today)

    // Importe
    await page.getByTestId('input-amount').fill('99.99')

    // 5. Enviar
    await page.getByTestId('btn-submit').click()

    // 6. Toast de éxito
    await expect(page.getByText('Transacción creada')).toBeVisible({ timeout: 8_000 })

    // 7. El Sheet se cierra y aparece al menos una fila en la tabla
    await expect(page.getByTestId('tx-row').first()).toBeVisible({ timeout: 8_000 })
  })

})
