import { execSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import path from 'node:path'

// tests/e2e → ../../../.. = raíz del repo
const REPO_ROOT = path.resolve(__dirname, '../../../..')
const COMPOSE_FILE = path.join(REPO_ROOT, 'infra/docker-compose.e2e.yml')
const ENV_FILE = path.join(REPO_ROOT, 'infra/.env')
const INIT_SQL = path.join(REPO_ROOT, 'infra/docker/mysql/init.sql')
const MYSQL_CONTAINER = 'bigschool-mysql'

function readEnvValue(key: string): string {
  const line = readFileSync(ENV_FILE, 'utf8')
    .split(/\r?\n/)
    .find((l) => l.startsWith(`${key}=`))
  if (!line) throw new Error(`No se encontró ${key} en ${ENV_FILE}`)
  return line.slice(key.length + 1).trim()
}

async function waitForHttp(url: string, attempts = 40): Promise<void> {
  for (let i = 0; i < attempts; i++) {
    try {
      const res = await fetch(url)
      if (res.ok) return
    } catch {
      /* aún no responde */
    }
    await new Promise((r) => setTimeout(r, 2000))
  }
  throw new Error(`Timeout esperando ${url}`)
}

export default async function globalSetup(): Promise<void> {
  const rootPass = process.env.MYSQL_ROOT_PASSWORD ?? readEnvValue('MYSQL_ROOT_PASSWORD')

  // 1. El MySQL dev debe estar en marcha
  try {
    execSync(`docker inspect -f "{{.State.Running}}" ${MYSQL_CONTAINER}`, { stdio: 'pipe' })
  } catch {
    throw new Error(
      `El contenedor ${MYSQL_CONTAINER} no está en marcha.\n` +
        'Ejecuta antes: cd infra && docker compose --env-file .env up -d mysql && cd -',
    )
  }

  // 2. (Re)crear bigschool_e2e y aplicar el esquema base (init.sql con USE reescrito).
  //    DROP+CREATE garantiza idempotencia aunque un run previo dejara restos.
  const schemaSql =
    'DROP DATABASE IF EXISTS `bigschool_e2e`;\n' +
    'CREATE DATABASE `bigschool_e2e` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;\n' +
    readFileSync(INIT_SQL, 'utf8').replace(/USE `bigschool`/g, 'USE `bigschool_e2e`')

  execSync(`docker exec -i -e MYSQL_PWD ${MYSQL_CONTAINER} mysql -uroot`, {
    input: schemaSql,
    env: { ...process.env, MYSQL_PWD: rootPass },
    stdio: ['pipe', 'inherit', 'inherit'],
  })

  // 3. Levantar front + back dockerizados.
  execSync(`docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" up -d --build`, {
    stdio: 'inherit',
    env: { ...process.env, MYSQL_ROOT_PASSWORD: rootPass },
  })

  // 4. Esperar a que ambos respondan.
  await waitForHttp('http://localhost:8081/health')
  await waitForHttp('http://localhost:3001')
}
