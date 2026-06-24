# Frontend Web — BigSchool TFM

Aplicación web Next.js 14+ para gestión de finanzas personales e inversiones.

## Stack

- **Framework**: Next.js 14+ (App Router)
- **Lenguaje**: TypeScript (strict mode)
- **Estilos**: Tailwind CSS
- **Componentes**: shadcn/ui
- **Gráficas**: Recharts
- **Estado servidor**: TanStack Query (React Query)
- **Testing**: Vitest (unitarios) + Playwright (E2E)

---

## Comandos del día a día

Todos los comandos se ejecutan desde `src/frontend-web/`.

### 1. Instalar dependencias (restore)

```powershell
pnpm install
```

**Analogía .NET:** `dotnet restore`. Descarga todo lo declarado en `package.json` a `node_modules/`.

**¿Cuándo ejecutarlo?**
- Tras clonar el repo
- Tras `git pull` si cambiaron dependencias
- Si VS Code dice "Cannot find module"

---

### 2. Reset nuclear (si algo va mal)

```powershell
Remove-Item -Recurse -Force node_modules, .next
pnpm install
```

**Analogía .NET:** Borrar `bin/`, `obj/` y hacer `dotnet restore` limpio.

**Si el lockfile está corrupto:**
```powershell
pnpm clean --lockfile
pnpm install
```

---

### 3. Arrancar el servidor de desarrollo

```powershell
pnpm dev
```

→ Abre **http://localhost:3000**

**Características:**
- Primera carga: 15-30s (compila todo)
- Siguientes: instantáneas
- **Hot reload** automático (guardas un archivo → recarga solo)
- Compilación incremental (solo lo que cambió)

**Parar:** `Ctrl+C` en el terminal

**Matar si quedó en background:**
```powershell
# Ver qué proceso ocupa el puerto
Get-NetTCPConnection -LocalPort 3000

# Matar el proceso (reemplazar <PID> con el OwningProcess)
Stop-Process -Id <PID> -Force

# O directamente (one-liner)
Get-Process -Id (Get-NetTCPConnection -LocalPort 3000).OwningProcess | Stop-Process -Force
```

---

### 4. Build de producción

```powershell
pnpm build
```

Compila todo a JavaScript optimizado en `.next/`. Luego arrancar con:

```powershell
pnpm start
```

---

## Debug desde VS Code

Crear `.vscode/launch.json` en la **raíz del workspace** con:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Next.js: debug (frontend)",
      "type": "node",
      "request": "launch",
      "cwd": "${workspaceFolder}/src/frontend-web",
      "runtimeExecutable": "pnpm",
      "runtimeArgs": ["dev"],
      "port": 9229,
      "console": "integratedTerminal",
      "serverReadyAction": {
        "pattern": "- Local:.+(https?://.+)",
        "uriFormat": "%s",
        "action": "debugWithChrome"
      }
    }
  ]
}
```

Luego `F5` para arrancar con debugger adjunto.

**Client Components** (`"use client"`): usa Chrome DevTools (`F12` → Sources) para poner breakpoints en el navegador.

---

## Estructura del proyecto

```
frontend-web/
├── package.json          ← Dependencias + scripts (como .csproj)
├── tsconfig.json         ← Config TypeScript (compartida app+tests)
├── node_modules/         ← Paquetes instalados (como NuGet + bin/)
├── .next/                ← Build cache (como bin/Debug)
├── public/               ← Assets estáticos (como wwwroot): favicon, imágenes
│   └── ⚠️ NO poner código aquí
├── src/                  ← CÓDIGO de la aplicación
│   ├── app/              ← Páginas (estructura = URLs)
│   │   ├── page.tsx      ← URL: / (landing)
│   │   ├── (public)/     ← Route group: sin JWT (landing, login, registro)
│   │   └── (private)/    ← Route group: con JWT (dashboard, gastos, inversiones)
│   ├── components/       ← Componentes reutilizables
│   │   ├── ui/           ← Componentes base shadcn
│   │   ├── charts/       ← Envoltorios Recharts
│   │   └── forms/        ← Formularios
│   ├── lib/              ← Utilidades (apiClient, helpers)
│   ├── services/         ← Llamadas al Backend API
│   ├── hooks/            ← Custom React hooks
│   └── types/            ← Interfaces TypeScript (DTOs)
└── test/                 ← Tests
    ├── unit/             ← Vitest
    └── e2e/              ← Playwright
```

---

## Path alias

El alias `@/` resuelve a `./src/*`. Configurado en `tsconfig.json`:

```json
"paths": {
  "@/*": ["./src/*"]
}
```

**Ejemplo:**
```tsx
import { Button } from "@/components/ui/button"
// Se resuelve a: ./src/components/ui/button.tsx
```

---

## Troubleshooting

### "Cannot find module '@/components/...'" o errores de imports

**Solución rápida (90% de los casos):**

1. **Reiniciar TypeScript Server en VS Code**:
   - `Ctrl+Shift+P` (abrir paleta de comandos)
   - Escribir: `TypeScript: Restart TS Server`
   - `Enter`
   
   Esto fuerza a TypeScript a refrescar su caché de módulos sin reiniciar VS Code.

2. **Si no funciona, borrar caché de Next.js**:
   ```powershell
   Remove-Item -Recurse -Force .next
   pnpm dev
   ```

3. **Última opción — verificar `tsconfig.json`**:
   Debe tener `"@/*": ["./src/*"]` en la sección `paths`

### El servidor no arranca / puerto ocupado

```powershell
Get-NetTCPConnection -LocalPort 3000
Stop-Process -Id <PID> -Force
```

### Errores raros tras `git pull`

```powershell
Remove-Item -Recurse -Force node_modules, .next
pnpm install
```

---

## Enlaces útiles

- [Next.js Docs](https://nextjs.org/docs)
- [shadcn/ui](https://ui.shadcn.com/)
- [Recharts](https://recharts.org/)
- [TanStack Query](https://tanstack.com/query/latest)
- [Tailwind CSS](https://tailwindcss.com/)

---

## Notas para desarrolladores .NET

| Next.js | .NET | Qué es |
|---------|------|--------|
| `pnpm install` | `dotnet restore` | Instalar dependencias |
| `node_modules/` | NuGet packages + `bin/` | Paquetes + compilado |
| `.next/` | `bin/Debug/` | Cache de build |
| `public/` | `wwwroot/` | Assets estáticos |
| `src/app/page.tsx` | `Controller` con ruta `/` | Página raíz |
| `@/components` | `using` alias | Path alias |
| `"use client"` | Blazor WebAssembly | Código que corre en el navegador |
| Server Component | Razor Page | Código que corre en el servidor |
