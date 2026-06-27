import { test, expect, type Page } from '@playwright/test'

/**
 * E2E hito Task 10 — Vender un holding (FIFO) y verificar que los datos se actualizan.
 *
 * Prerequisito: backend en marcha en http://localhost:5285 con al menos una empresa
 * y su cotización registrada (necesaria para calcular marketValue).
 *
 * Flujo:
 *  1. Registrar usuario nuevo (email único por run).
 *  2. Crear una cartera desde /investments.
 *  3. Navegar a la cartera y añadir un holding (10 acciones a 100).
 *  4. Abrir el modal de venta y vender 5 acciones a 120.
 *  5. Verificar toast de éxito con "Venta registrada".
 *  6. Verificar que el holding muestra acciones actualizadas (5 / 10).
 */

const uniqueEmail = () => `e2e+sell+${Date.now()}@bigschool-test.com`
const PASSWORD    = 'TestPass123!'
const FULL_NAME   = 'E2E Sell User'

const navbar = (page: Page) => page.getByRole('navigation')

async function registerAndLogin(page: Page, email: string) {
  await page.goto('/')
  await navbar(page).getByRole('button', { name: 'Registrarse' }).click()
  await page.getByLabel('Nombre completo').fill(FULL_NAME)
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Contraseña', { exact: true }).fill(PASSWORD)
  await page.getByLabel('Repetir contraseña').fill(PASSWORD)
  await page.getByRole('button', { name: 'Crear cuenta' }).click()
  await page.waitForURL('**/dashboard', { timeout: 10_000 })
}

test.describe('Inversiones — Vender holding FIFO (Task 10)', () => {

  test('vender holding → toast de éxito + acciones actualizadas', async ({ page }) => {
    const email = uniqueEmail()
    await registerAndLogin(page, email)

    // 1. Navegar a Inversiones
    await page.goto('/investments')

    // 2. Crear una cartera
    await page.getByTestId('btn-nueva-cartera').click()
    await expect(page.getByRole('dialog')).toBeVisible()
    await page.getByTestId('input-portfolio-name').fill('Cartera E2E Sell')
    await page.getByTestId('btn-create-portfolio').click()

    // El modal de creación navega automáticamente a /investments/{id}
    await page.waitForURL('**/investments/**', { timeout: 10_000 })

    // 3. Añadir un holding (10 acciones a 100)
    await page.getByTestId('btn-add-holding').click()
    await expect(page.getByRole('dialog')).toBeVisible()

    // Seleccionar la primera empresa disponible en el Select
    await page.getByTestId('select-company').click()
    await page.getByRole('option').first().click()

    await page.getByTestId('input-shares').fill('10')
    await page.getByTestId('input-buy-price').fill('100')

    const today = new Date().toISOString().split('T')[0]
    await page.getByTestId('input-buy-date').fill(today)

    // Confirmar la adición (el botón submit dentro del dialog)
    await page.getByRole('dialog').getByTestId('btn-add-holding').click()
    await expect(page.getByText('Holding añadido')).toBeVisible({ timeout: 8_000 })

    // 4. Verificar que el holding aparece en la lista
    await expect(page.getByTestId('holding-card').first()).toBeVisible({ timeout: 8_000 })

    // 5. Abrir el modal de venta
    await page.getByTestId('btn-sell-holding').first().click()
    await expect(page.getByRole('dialog')).toBeVisible()

    // 6. Rellenar el formulario de venta (5 acciones a 120)
    await page.getByTestId('input-sell-shares').fill('5')
    await page.getByTestId('input-sell-price').fill('120')
    await page.getByTestId('input-sell-date').fill(today)

    // 7. Confirmar la venta
    await page.getByTestId('btn-confirm-sell').click()

    // 8. Verificar el toast de éxito
    await expect(page.getByText('Venta registrada')).toBeVisible({ timeout: 8_000 })

    // 9. Verificar que las acciones abiertas se actualizaron (5 / 10)
    await expect(page.getByTestId('holding-card').first()).toContainText('5 / 10', { timeout: 8_000 })
  })

})
