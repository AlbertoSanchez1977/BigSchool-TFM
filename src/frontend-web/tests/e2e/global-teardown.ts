import { execSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import path from 'node:path'

const REPO_ROOT = path.resolve(__dirname, '../../../..')
const COMPOSE_FILE = path.join(REPO_ROOT, 'infra/docker-compose.e2e.yml')
const ENV_FILE = path.join(REPO_ROOT, 'infra/.env')
const MYSQL_CONTAINER = 'bigschool-mysql'

function readEnvValue(key: string): string {
  const line = readFileSync(ENV_FILE, 'utf8')
    .split(/\r?\n/)
    .find((l) => l.startsWith(`${key}=`))
  if (!line) throw new Error(`No se encontró ${key} en ${ENV_FILE}`)
  return line.slice(key.length + 1).trim()
}

export default async function globalTeardown(): Promise<void> {
  const rootPass = process.env.MYSQL_ROOT_PASSWORD ?? readEnvValue('MYSQL_ROOT_PASSWORD')

  // 1. Bajar front + back (elimina contenedores bigschool-e2e-*).
  execSync(`docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" down --remove-orphans`, {
    stdio: 'inherit',
  })

  // 2. Eliminar la BD efímera.
  execSync(`docker exec -i -e MYSQL_PWD ${MYSQL_CONTAINER} mysql -uroot`, {
    input: 'DROP DATABASE IF EXISTS `bigschool_e2e`;',
    env: { ...process.env, MYSQL_PWD: rootPass },
    stdio: ['pipe', 'inherit', 'inherit'],
  })
}
