import { test, expect } from '@playwright/test'

/**
 * E2E de autenticación — Task 3.
 *
 * Prerequisito: backend en marcha en http://localhost:5285
 * (NEXT_PUBLIC_API_URL=http://localhost:5285/api/v1)
 *
 * Nota: la navegación post-login va a /dashboard, que no existe hasta Task 4.
 * Estos tests verifican la apertura de formularios, validación y el flujo
 * de auth completo contra el backend real. La comprobación de ruta privada
 * se añade en Task 4.
 */

// Email único por ejecución para no colisionar con registros previos
const uniqueEmail = () => `e2e+${Date.now()}@bigschool-test.com`
const PASSWORD = 'TestPass123!'
const FULL_NAME = 'E2E Test User'

test.describe('Formularios de autenticación (landing page)', () => {

  test.beforeEach(async ({ page }) => {
    await page.goto('/')
    // Esperar a que el navbar esté visible
    await expect(page.getByRole('button', { name: 'Registrarse' })).toBeVisible()
  })

  // ── Apertura de paneles ────────────────────────────────────────────────────

  test('botón "Iniciar sesión" abre el panel con el formulario de login', async ({ page }) => {
    await page.getByRole('button', { name: 'Iniciar sesión' }).click()
    await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible()
    await expect(page.getByLabel('Email')).toBeVisible()
    await expect(page.getByLabel('Contraseña')).toBeVisible()
  })

  test('botón "Registrarse" abre el panel con el formulario de registro', async ({ page }) => {
    await page.getByRole('button', { name: 'Registrarse' }).click()
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).toBeVisible()
    await expect(page.getByLabel('Nombre completo')).toBeVisible()
  })

  test('enlace "Regístrate" dentro del login cambia al formulario de registro', async ({ page }) => {
    await page.getByRole('button', { name: 'Iniciar sesión' }).click()
    await page.getByRole('button', { name: 'Regístrate' }).click()
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).toBeVisible()
  })

  test('enlace "Inicia sesión" dentro del registro cambia al formulario de login', async ({ page }) => {
    await page.getByRole('button', { name: 'Registrarse' }).click()
    await page.getByRole('button', { name: 'Inicia sesión' }).click()
    await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible()
  })

  test('click fuera del panel lo cierra', async ({ page }) => {
    await page.getByRole('button', { name: 'Registrarse' }).click()
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).toBeVisible()
    // Click en el logo (fuera del panel)
    await page.getByRole('link', { name: 'BigSchool inicio' }).click()
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).not.toBeVisible()
  })

  // ── Validación de formularios ──────────────────────────────────────────────

  test('login muestra error con email inválido', async ({ page }) => {
    await page.getByRole('button', { name: 'Iniciar sesión' }).click()
    await page.getByLabel('Email').fill('no-es-email')
    await page.getByLabel('Contraseña').fill('pass123')
    await page.getByRole('button', { name: 'Entrar' }).click()
    await expect(page.getByText('Email inválido')).toBeVisible()
  })

  test('registro muestra error cuando las contraseñas no coinciden', async ({ page }) => {
    await page.getByRole('button', { name: 'Registrarse' }).click()
    await page.getByLabel('Nombre completo').fill(FULL_NAME)
    await page.getByLabel('Email').fill('user@test.com')
    await page.getByLabel('Contraseña').fill('Password1!')
    await page.getByLabel('Repetir contraseña').fill('OtraPassword2!')
    await page.getByRole('button', { name: 'Crear cuenta' }).click()
    await expect(page.getByText('Las contraseñas no coinciden')).toBeVisible()
  })

  // ── Flujo completo contra el backend ──────────────────────────────────────
  // Estos tests requieren el backend en http://localhost:5285

  test('registro exitoso → redirige (requiere backend)', async ({ page }) => {
    const email = uniqueEmail()
    await page.getByRole('button', { name: 'Registrarse' }).click()
    await page.getByLabel('Nombre completo').fill(FULL_NAME)
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Contraseña').fill(PASSWORD)
    await page.getByLabel('Repetir contraseña').fill(PASSWORD)
    await page.getByRole('button', { name: 'Crear cuenta' }).click()

    // Tras registro exitoso el panel se cierra y el router navega a /dashboard
    // (la página /dashboard se implementa en Task 4)
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).not.toBeVisible({ timeout: 5000 })
  })

  test('login exitoso → redirige (requiere backend)', async ({ page }) => {
    // Usar las credenciales registradas justo arriba no es fiable entre runs,
    // así que registramos y luego hacemos login en el mismo test.
    const email = uniqueEmail()

    // 1. Registro
    await page.getByRole('button', { name: 'Registrarse' }).click()
    await page.getByLabel('Nombre completo').fill(FULL_NAME)
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Contraseña').fill(PASSWORD)
    await page.getByLabel('Repetir contraseña').fill(PASSWORD)
    await page.getByRole('button', { name: 'Crear cuenta' }).click()
    await expect(page.getByRole('heading', { name: 'Crear cuenta' })).not.toBeVisible({ timeout: 5000 })

    // 2. Volver a / y hacer login
    await page.goto('/')
    await page.getByRole('button', { name: 'Iniciar sesión' }).click()
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Contraseña').fill(PASSWORD)
    await page.getByRole('button', { name: 'Entrar' }).click()

    // Panel se cierra al completar el login
    await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).not.toBeVisible({ timeout: 5000 })
  })
})
