import { test, expect, type Page } from '@playwright/test'

/**
 * E2E de autenticación — Task 3 (selectores revisados en Task 4).
 *
 * Prerequisito: backend en marcha en http://localhost:5285
 * (NEXT_PUBLIC_API_URL=http://localhost:5285/api/v1)
 *
 * Nota: la navegación post-login va a /dashboard, que existe desde Task 4.
 *
 * Importante sobre selectores:
 * La landing tiene botones "Registrarse" / "Iniciar sesión" en VARIAS secciones
 * (navbar, hero y sección de contacto). El panel flotante de login/registro solo
 * se abre desde los botones del NAVBAR, así que acotamos siempre con
 * `navbar(page)` para evitar el strict-mode violation de Playwright.
 */

// Email único por ejecución para no colisionar con registros previos
const uniqueEmail = () => `e2e+${Date.now()}@bigschool-test.com`
const PASSWORD = 'TestPass123!'
const FULL_NAME = 'E2E Test User'

// Acota los botones de auth al navbar (hay otros "Registrarse"/"Iniciar sesión"
// en el hero y la sección de contacto que NO abren el panel flotante).
const navbar = (page: Page) => page.getByRole('navigation')

test.describe('Formularios de autenticación (landing page)', () => {

  test.beforeEach(async ({ page }) => {
    await page.goto('/')
    // Esperar a que el navbar (botón que abre el panel) esté visible
    await expect(navbar(page).getByRole('button', { name: 'Registrarse' })).toBeVisible()
  })

  // ── Apertura de paneles ────────────────────────────────────────────────────

  test('botón "Iniciar sesión" abre el panel con el formulario de login', async ({ page }) => {
    await navbar(page).getByRole('button', { name: 'Iniciar sesión' }).click()
    await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible()
    await expect(page.getByLabel('Email')).toBeVisible()
    await expect(page.getByLabel('Contraseña', { exact: true })).toBeVisible()
  })

  test('botón "Registrarse" abre el panel con el formulario de registro', async ({ page }) => {
    await navbar(page).getByRole('button', { name: 'Registrarse' }).click()
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).toBeVisible()
    await expect(page.getByLabel('Nombre completo')).toBeVisible()
  })

  test('enlace "Regístrate" dentro del login cambia al formulario de registro', async ({ page }) => {
    await navbar(page).getByRole('button', { name: 'Iniciar sesión' }).click()
    await page.getByRole('button', { name: 'Regístrate' }).click()
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).toBeVisible()
  })

  test('enlace "Inicia sesión" dentro del registro cambia al formulario de login', async ({ page }) => {
    await navbar(page).getByRole('button', { name: 'Registrarse' }).click()
    await page.getByRole('button', { name: 'Inicia sesión' }).click()
    await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible()
  })

  test('click fuera del panel lo cierra', async ({ page }) => {
    await navbar(page).getByRole('button', { name: 'Registrarse' }).click()
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).toBeVisible()
    // Click en el logo (fuera del panel)
    await page.getByRole('link', { name: 'BigSchool inicio' }).click()
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).not.toBeVisible()
  })

  // ── Validación de formularios ──────────────────────────────────────────────

  test('login muestra error con email inválido', async ({ page }) => {
    await navbar(page).getByRole('button', { name: 'Iniciar sesión' }).click()
    await page.getByLabel('Email').fill('no-es-email')
    await page.getByLabel('Contraseña', { exact: true }).fill('pass123')
    await page.getByRole('button', { name: 'Entrar' }).click()
    await expect(page.getByText('Email inválido')).toBeVisible()
  })

  test('registro muestra error cuando las contraseñas no coinciden', async ({ page }) => {
    await navbar(page).getByRole('button', { name: 'Registrarse' }).click()
    await page.getByLabel('Nombre completo').fill(FULL_NAME)
    await page.getByLabel('Email').fill('user@test.com')
    // exact:true para no colisionar con "Repetir contraseña"
    await page.getByLabel('Contraseña', { exact: true }).fill('Password1!')
    await page.getByLabel('Repetir contraseña').fill('OtraPassword2!')
    await page.getByRole('button', { name: 'Crear cuenta' }).click()
    await expect(page.getByText('Las contraseñas no coinciden')).toBeVisible()
  })

  // ── Flujo completo contra el backend ──────────────────────────────────────
  // Estos tests requieren el backend en http://localhost:5285

  test('registro exitoso → redirige (requiere backend)', async ({ page }) => {
    const email = uniqueEmail()
    await navbar(page).getByRole('button', { name: 'Registrarse' }).click()
    await page.getByLabel('Nombre completo').fill(FULL_NAME)
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Contraseña', { exact: true }).fill(PASSWORD)
    await page.getByLabel('Repetir contraseña').fill(PASSWORD)
    await page.getByRole('button', { name: 'Crear cuenta' }).click()

    // Tras registro exitoso el panel se cierra y el router navega a /dashboard
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).not.toBeVisible({ timeout: 5000 })
  })

  test('login exitoso → redirige (requiere backend)', async ({ page }) => {
    // Usar las credenciales registradas justo arriba no es fiable entre runs,
    // así que registramos y luego hacemos login en el mismo test.
    const email = uniqueEmail()

    // 1. Registro
    await navbar(page).getByRole('button', { name: 'Registrarse' }).click()
    await page.getByLabel('Nombre completo').fill(FULL_NAME)
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Contraseña', { exact: true }).fill(PASSWORD)
    await page.getByLabel('Repetir contraseña').fill(PASSWORD)
    await page.getByRole('button', { name: 'Crear cuenta' }).click()
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).not.toBeVisible({ timeout: 5000 })

    // 2. Cerrar la sesión que el registro acaba de crear: si no limpiamos el
    // token de localStorage, al volver a "/" el navbar mostraría "Ir al
    // Dashboard" (estado autenticado) y no existiría el botón "Iniciar sesión".
    await page.evaluate(() => window.localStorage.clear())

    // 3. Volver a / (ya sin sesión) y hacer login
    await page.goto('/')
    await navbar(page).getByRole('button', { name: 'Iniciar sesión' }).click()
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Contraseña', { exact: true }).fill(PASSWORD)
    await page.getByRole('button', { name: 'Entrar' }).click()

    // Panel se cierra al completar el login
    await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).not.toBeVisible({ timeout: 5000 })
  })
})
