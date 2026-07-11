# Guía de pruebas manuales — Frontend Web + integración Backend

> **Objetivo.** Recorrido paso a paso de **todo** el frontend y su enlace real con el backend, para
> validar manualmente que cada flujo funciona, detectar bugs y decidir cambios. Marca cada casilla a
> medida que lo pruebas y usa la columna de notas / el registro de bugs del final para apuntar lo que
> encuentres.
>
> **Cómo leer cada bloque:** cada prueba indica **Precondición → Pasos → Resultado esperado**. Si el
> resultado real no coincide, anótalo en el [Registro de bugs](#registro-de-bugs-y-observaciones).

---

## 0. Preparar el entorno

Este proyecto corre en local con MySQL en Docker, el backend arrancado desde el IDE y el frontend en
modo dev. Config real: `NEXT_PUBLIC_API_URL=http://localhost:5285/api/v1` (ver `.env.local`).

- [x] **0.1 — MySQL levantado.** Contenedor `bigschool-mysql` en marcha:
  ```bash
  cd infra && docker compose --env-file .env up -d mysql
  ```
  Verifica: `docker ps` muestra `bigschool-mysql` como `healthy`.
- [x] **0.2 — Backend en marcha en `:5285`.** Arráncalo desde el IDE (o `dotnet run` en
  `src/backend/src/BigSchool.WebApi`). Verifica: `http://localhost:5285/health` responde `200` y
  Swagger carga en `http://localhost:5285/swagger`.
- [x] **0.3 — Frontend en marcha en `:3000`.**
  ```bash
  cd src/frontend-web && npm run dev
  ```
  Verifica: abre `http://localhost:3000` y carga la landing.
- [x] **0.4 — AI Scanner activado (opcional).** En `.env.local`,
  `NEXT_PUBLIC_AI_SCANNER_ENABLED=true` muestra la pantalla completa del scanner; `false` (o ausente)
  muestra "Próximamente". Decide con cuál quieres probar (§14 cubre ambos).
- [x] **0.5 — Base de datos con seeds.** Confirma que existen las categorías (Finance) y, si quieres
  probar Mercado/Valoraciones con datos previos, alguna empresa. Si arrancas de cero, los estados
  vacíos son parte de la prueba (§4).

> **Consejo:** ten abierta la consola del navegador (F12 → Network + Console) durante todo el
> recorrido. Cualquier `4xx`/`5xx` o error rojo en consola es un bug a registrar aunque la UI
> parezca funcionar.

---

## 1. Landing pública y páginas estáticas (sin autenticar)

- [x] **1.1 — Landing carga.** Ve a `http://localhost:3000`. Se ve el navbar público (Logo, Gastos e
  ingresos, Inversiones, AI Scanner, Contacto, Alcance) y los botones **Iniciar sesión** /
  **Registrarse** a la derecha.
- [x] **1.2 — Secciones ancla.** Los enlaces del navbar (`#gastos`, `#inversiones`, `#ai-scanner`,
  `#contacto`) hacen scroll a su sección. La landing muestra Hero, Gastos, Inversiones, AI Scanner y
  CTA + formulario de contacto.
- [x] **1.3 — Página de Alcance.** Enlace **Alcance** → navega a `/scope`. Se ve el contenido
  estático de alcance y trabajos futuros. El logo vuelve a la landing.
- [x] **1.4 — Panel de auth flotante (desktop).** Clic en **Iniciar sesión** → se abre un panel
  flotante bajo el botón con el formulario de login. Clic en **Registrarse** → el panel muestra el
  formulario de registro. Enlaces internos ("Regístrate" / "Inicia sesión") alternan entre ambos.
- [x] **1.5 — Cerrar el panel.** Un clic **fuera** del panel lo cierra. (Ojo: elegir una opción del
  `Select` de moneda **no** debe cerrarlo — es un caso especial ya contemplado.)
- [x] **1.6 — Rutas protegidas sin sesión.** Con la sesión cerrada, escribe directamente
  `http://localhost:3000/dashboard` en la URL. Debe **redirigir** a la landing/login, no mostrar el
  dashboard.

---

## 2. Registro

**Precondición:** sesión cerrada. Usa un email nuevo (p. ej. `test+YYYYMMDD@bigschool.local`).

- [x] **2.1 — Validaciones del formulario.** En Registrarse, intenta enviar con:
  - Nombre de 1 carácter → error "al menos 2 caracteres".
  - Email mal formado → error "Email inválido".
  - Contraseña < 8 caracteres → error "al menos 8 caracteres".
  - Contraseñas que no coinciden → error "Las contraseñas no coinciden".
  - Sin moneda base → error "Selecciona una moneda".
- [x] **2.2 — Selección de moneda base.** El `Select` de **Moneda base** ofrece EUR, USD, GBP, CHF,
  JPY. Elige una (recomiendo la que quieras como base para el resto de pruebas).
- [x] **2.3 — Registro correcto.** Rellena todo bien y pulsa **Crear cuenta**. El botón pasa a
  "Creando cuenta…" y al terminar **redirige a `/dashboard`**.
- [x] **2.4 — Evento de bienvenida (email simulado).** El registro dispara en backend un email de
  bienvenida simulado. Lo verificarás luego en §13.3 (pantalla de Emails debe listar uno de tipo
  "Bienvenida" para tu usuario).
- [x] **2.5 — Email duplicado.** Cierra sesión (§3.1) e intenta registrarte otra vez con el **mismo
  email**. Debe mostrar un error controlado (mensaje del backend), no un crash.

---

## 3. Cerrar sesión e iniciar sesión

- [x] **3.1 — Logout.** Con sesión iniciada, abre el avatar (arriba a la derecha) → **Cerrar
  sesión**. Vuelve a la landing/estado no autenticado (los botones Iniciar sesión / Registrarse
  reaparecen).
- [x] **3.2 — Login con credenciales incorrectas.** En Iniciar sesión, email correcto + contraseña
  incorrecta → mensaje de error controlado, sin redirigir.
- [x] **3.3 — Login correcto.** Credenciales válidas → botón "Entrando…" y redirección a
  `/dashboard`.
- [x] **3.4 — Persistencia de sesión.** Con sesión iniciada, **recarga** (F5) una página privada. La
  sesión se mantiene (no te expulsa al login).
- [x] **3.5 — "Último acceso".** Tras un login, en Perfil (§5) el campo "Último acceso" debe reflejar
  una fecha/hora reciente.

---

## 4. Estados vacíos de las páginas privadas (cuenta recién creada)

**Precondición:** cuenta nueva sin datos. Recorre cada página vía navbar y confirma el **estado
vacío** (no un error ni una pantalla en blanco).

- [x] **4.1 — Dashboard.** KPIs a 0 / "—", gráficas vacías o planas, y bloque de Inversiones con
  "No tienes carteras aún" + botón para crear la primera.
- [x] **4.2 — Gastos e ingresos.** Pestaña Listado con estado vacío ("No hay transacciones en
  [mes] [año]") y botón para crear. KPIs de resumen a 0.
- [x] **4.3 — Inversiones.** Estado vacío "No tienes carteras aún" + botón "Crear mi primera
  cartera".
- [x] **4.4 — Mercado.** Si no hay empresas: "No hay empresas en el catálogo aún" + botón para crear
  la primera. (Si el seed trae empresas, verás el listado — igualmente válido.)
- [x] **4.5 — AI Scanner.** Según flag (§14).
- [x] **4.6 — Perfil** (avatar → Editar perfil): carga tus datos (email, moneda base, último acceso).
- [x] **4.7 — Contacto** (avatar → Contacto): estado vacío "Aún no hay mensajes".
- [x] **4.8 — Emails** (avatar → Emails): debería mostrar **al menos** el email de bienvenida de tu
  registro (§2.4). Si tu cuenta es totalmente nueva y no aparece, es un bug a registrar.

---

## 5. Perfil (GET/PUT /users/me)

- [x] **5.1 — Datos de solo lectura.** Email, Moneda base y Último acceso se muestran correctamente.
- [x] **5.2 — Cambiar nombre.** Cambia "Nombre completo" y **Guardar cambios**. Aparece "Datos
  guardados" y el **avatar/nombre del navbar se actualiza** sin recargar.
- [x] **5.3 — Validación de contraseña.** Escribe una contraseña nueva < 8 caracteres → error. Deja
  ambos campos de contraseña vacíos → guarda solo el nombre sin tocar la contraseña.
- [x] **5.4 — Cambio de contraseña efectivo.** Cambia la contraseña, cierra sesión y vuelve a entrar
  con la **nueva**. Debe funcionar; la antigua debe fallar.

---

## 6. Gastos e ingresos — CRUD y filtros

**Precondición:** en `/expenses`, pestaña **Listado**.

- [x] **6.1 — Abrir modal de creación.** Botón **Nueva transacción** abre el modal centrado.
- [x] **6.2 — Crear un gasto.** Tipo = Gasto, elige Categoría (y Subcategoría si aparece), Importe,
  Fecha (hoy por defecto), Moneda, Descripción opcional → **Crear transacción**. Toast "Transacción
  creada" y aparece en la lista.
- [x] **6.3 — Crear un ingreso.** Cambia Tipo a Ingreso → las **categorías cambian** (Salario,
  Alquileres, Dividendos, Otros…). Crea uno. El importe se muestra en verde y el gasto en rojo.
- [x] **6.4 — Validaciones.** Importe vacío o ≤ 0 → error. Fecha inválida → error.
- [x] **6.5 — Editar.** Clic en una fila (desktop) / tarjeta (móvil) → modal en modo edición con los
  datos cargados. Cambia el importe → **Guardar cambios** → toast "Transacción actualizada" y la
  lista refleja el nuevo valor.
- [x] **6.6 — Borrar con confirmación.** En modo edición, **Eliminar** → aparece confirmación inline
  → **Sí, eliminar** → toast "Transacción eliminada" y desaparece de la lista.
- [x] **6.7 — Selector mes/año.** Con las flechas ‹ ›, cambia de mes. La lista y los KPIs se
  recalculan al mes seleccionado. Un mes sin datos muestra el estado vacío correspondiente.
- [x] **6.8 — KPIs de resumen.** Ingresos / Gastos / Balance del mes cuadran con las transacciones
  visibles (balance = ingresos − gastos, color según signo).
- [x] **6.9 — Icono de descripción.** Una transacción con descripción muestra el icono de nota
  (StickyNote); al pasar el ratón, el `title` muestra el texto.

---

## 7. Paginación (>20 transacciones)

**Objetivo:** validar la paginación real (page size = 20).

- [x] **7.1 — Crear ≥ 21 transacciones en el mismo mes.** Da de alta al menos 21 transacciones con
  fecha dentro del **mismo mes** (para que caigan en el mismo filtro). Puedes variar importe y
  categoría para distinguirlas.
- [x] **7.2 — Aparece el control de paginación.** Con >20 en el mes, aparece el control "1–20 de N" +
  botones **Anterior / Siguiente** al pie de la lista.
- [x] **7.3 — Navegar páginas.** **Siguiente** carga la página 2 (transacciones 21…N). **Anterior**
  vuelve. En la página 1, "Anterior" está deshabilitado; en la última, "Siguiente" está
  deshabilitado.
- [x] **7.4 — Coherencia del contador.** El texto "X–Y de N" coincide con lo que se ve y con el total
  real de ese mes.
- [x] **7.5 — Paginación no aparece con ≤20.** Cambia a un mes con ≤20 transacciones → el control de
  paginación **no** se muestra.

---

## 8. Gastos e ingresos — Pestaña Gráficas

**Precondición:** ten datos repartidos en **varios años** para que las gráficas de 4 años tengan
sentido (puedes crear transacciones con fechas de años anteriores).

- [x] **8.1 — Cambiar de pestaña.** En `/expenses`, pestaña **Gráficas**. Se cargan dos gráficas
  (por categoría y serie mensual).
- [x] **8.2 — Conmutador Gastos / Ingresos.** Alterna entre Gastos e Ingresos → ambas gráficas
  cambian de dataset.
- [x] **8.3 — Selector de año.** Con las flechas, mueve la ventana de 4 años. El año actual es el
  máximo (flecha "siguiente" deshabilitada en el año en curso).
- [x] **8.4 — Estados de carga/vacío.** Un tipo/año sin datos muestra la gráfica vacía sin romperse.

---

## 9. Dashboard

- [x] **9.1 — KPIs del mes.** Ingresos, Gastos, Balance y Tasa de ahorro del mes en curso cuadran con
  los datos de §6.
- [x] **9.2 — Gráfica Ingresos vs Gastos.** Barras por mes del año en curso, hasta el mes actual (sin
  meses futuros).
- [x] **9.3 — Balance acumulado.** Área acumulada del año, coherente con las transacciones.
- [x] **9.4 — Mini-resumen de Inversiones.** Tras crear carteras (§10), muestra Valor de mercado,
  Coste base, PnL total y Rentabilidad agregados + lista de carteras con su PnL. Enlace "Ver
  carteras" → `/investments`.
- [x] **9.5 — Últimas transacciones.** Muestra las más recientes (últimas 5).
- [x] **9.6 — Estado vacío de inversiones.** Sin carteras, muestra el bloque "No tienes carteras aún"
  con enlace para crear.

---

## 10. Inversiones — Carteras y Holdings

### 10.1 Carteras (listado)

- [x] **10.1.1 — Crear cartera.** `/investments` → **Nueva cartera** → nombre → **Crear cartera**.
  Toast "Cartera creada" y **redirige a la cartera nueva** (`/investments/{id}`).
- [x] **10.1.2 — Listado como cards.** Vuelve a `/investments`. Cada cartera es una card con KPIs
  (Valor mercado, No realizado, PnL total). Clic en la card (no en los iconos) → navega al detalle.
- [x] **10.1.3 — Renombrar desde el listado.** Icono lápiz en la card → modal → cambia el nombre →
  guardar. El nombre se actualiza en el listado. (El clic en el icono **no** debe navegar al
  detalle.)
- [x] **10.1.4 — Borrar desde el listado.** Icono papelera → modal de confirmación. Si la cartera
  tiene holdings abiertos, el backend responde **409** (guard fiscal) → debe verse un error
  controlado, no un crash. Una cartera vacía sí se borra.
- [x] **10.1.5 — Paginación de carteras.** Crea >20 carteras y verifica que aparece el control de
  paginación (page size = 20), igual que en §7.

### 10.2 Holdings (dentro de una cartera)

**Precondición:** al menos una empresa en el catálogo (créala en §11 si hace falta) y una cotización
para poder calcular valor de mercado.

- [x] **10.2.1 — Añadir holding.** En `/investments/{id}` → **Añadir holding** → **Empresa** vía
  combobox con búsqueda por ticker/nombre → Acciones, Precio compra, Fecha (hoy), Notas opcional →
  **Añadir holding**. Toast de éxito y aparece la card del holding.
- [x] **10.2.2 — Combobox de empresa.** Al abrir, muestra las primeras ~10 empresas; al escribir,
  filtra en cliente por ticker/nombre. (Deuda conocida: filtra sobre un máximo de 100 empresas — sin
  búsqueda server-side.)
- [x] **10.2.3 — Card de holding.** Muestra ticker, moneda, acciones abiertas/totales, Valor mercado,
  Coste base, PnL no realizado (con color e icono según signo), fecha de compra y nota.
- [x] **10.2.4 — Editar notas.** Botón de editar notas → modal → cambia el texto → guardar → toast y
  la nota se actualiza.
- [x] **10.2.5 — Vender (FIFO a nivel empresa).** Botón vender → modal. Vende **menos** acciones de
  las abiertas (p. ej. 5 de 10) → **Confirmar venta** → toast "Venta registrada" y las acciones
  abiertas se actualizan (5 / 10). **Nota de diseño:** la venta consume lotes FIFO de esa empresa;
  si vendes más que un lote, consume varios automáticamente.
- [x] **10.2.6 — Validación de venta.** Intenta vender **más** acciones de las abiertas → error /
  bloqueo (el input tiene `max = openShares`).
- [x] **10.2.7 — Borrar holding.** Botón eliminar → confirmación → toast "Holding eliminado" y
  desaparece.
- [x] **10.2.8 — Breadcrumb.** El detalle tiene cabecera `‹ Inversiones · [Nombre cartera]`. El
  enlace vuelve al listado (funciona incluso si entras por URL directa).

---

## 11. Mercado — Empresas (Companies)

- [x] **11.1 — Crear empresa.** `/market` → **Nueva empresa** → Nombre, Ticker (se fuerza a
  mayúsculas), Sector (opcional), Bolsa (opcional), Moneda (obligatoria) → **Crear empresa**. Toast
  "Empresa creada" y **redirige a `/market/{id}`**.
- [x] **11.2 — Validaciones.** Nombre/Ticker vacíos → error. Ticker > 10 caracteres → bloqueado.
- [x] **11.3 — Listado como cards.** En `/market`, cada empresa muestra ticker, nombre, sector
  (traducido) y último precio. Clic → detalle.
- [x] **11.4 — Paginación de empresas.** Con >20 empresas aparece el control de paginación (page
  size = 20).
- [x] **11.5 — Detalle de empresa.** `/market/{id}`: cabecera con ticker/nombre, Sector, Bolsa,
  Moneda, Último precio. Breadcrumb `‹ Mercado`.
- [x] **11.6 — Sin editar/borrar (deuda conocida).** El detalle **no** tiene botones de editar ni
  borrar empresa (el backend no expone `PUT`/`DELETE /companies/{id}`). Confirma que **no** hay
  botones muertos.

---

## 12. Valoraciones de empresa (dentro de `/market/{id}`)

**Precondición:** una empresa recién creada (sin valoraciones) para ver el estado vacío, y luego
añadir valoraciones.

- [x] **12.1 — Estado vacío del gráfico.** Sección "Serie de cotización" muestra "Esta empresa aún no
  tiene valoraciones".
- [x] **12.2 — Estado vacío del listado.** Sección "Valoraciones" muestra "Aún no hay valoraciones
  registradas".
- [x] **12.3 — Crear valoración.** Botón **Nueva valoración** → Precio, Fecha (hoy), Origen opcional
  → **Añadir valoración**. Toast "Valoración añadida". (Nota: no se pide moneda — la hereda la
  empresa.)
- [x] **12.4 — Aparece en la tabla.** La valoración aparece en el listado (Fecha, Origen, Precio con
  la moneda de la empresa).
- [x] **12.5 — El gráfico y el summary se actualizan.** Tras añadir 1+ valoraciones, la "Serie de
  cotización" muestra la línea y la fila de summary (Mínimo, Máximo, Último, Variación %). La
  variación va en verde/rojo según signo.
- [x] **12.6 — Selector de periodo.** Botones 3M / 6M / 1A / 3A / 5A cambian la ventana de la serie.
  Con pocos datos, todos pueden mostrar el mismo punto — lo importante es que **no** rompa y que el
  botón activo se marque.
- [x] **12.7 — Varias valoraciones + paginación.** Añade >20 valoraciones y verifica el control de
  paginación en el listado. Comprueba que la línea del gráfico dibuja la evolución.
- [x] **12.8 — Sin ver/borrar valoración (deuda conocida).** No hay acción de abrir el detalle de una
  valoración ni de borrarla (el backend no expone `GET{id}` ni `DELETE`). Confirma que no hay
  botones muertos.

---

## 13. Contacto y Emails (simulados)

- [X] **13.1 — Formulario de contacto (landing).** Sin autenticar (o desde `/#contacto`), rellena
  Nombre, Email y Mensaje → **Enviar mensaje**. Muestra el estado "¡Mensaje enviado!" con opción de
  enviar otro. (El envío se guarda en backend como registro simulado.)
- [x] **13.2 — Listado de contactos (privado).** Con sesión iniciada, avatar → **Contacto**
  (`/contacts`). El mensaje enviado en §13.1 aparece en la lista (Nombre, email, mensaje, fecha).
- [x] **13.3 — Registro de emails (privado).** Avatar → **Emails** (`/emails`). Debe listar:
  - Un email de **Bienvenida** (por tu registro, §2.4).
  - Un email de **Contacto** por cada mensaje enviado desde el formulario.
- [x] **13.4 — Validaciones del formulario de contacto.** Campos vacíos o email inválido → errores de
  validación antes de enviar.

---

## 14. AI Scanner

- [x] **14.1 — Modo "Próximamente".** Con `NEXT_PUBLIC_AI_SCANNER_ENABLED=false` (o sin la variable),
  `/ai-scanner` muestra la pantalla "Próximamente".
- [ ] **14.2 — Modo activado.** Con el flag en `true`, se ve el chat + panel de documentos RAG.
- [ ] **14.3 — Chat demo.** Escribe un mensaje → aparece tu mensaje y una respuesta "Demo — sin
  backend de IA conectado" (no hay backend de IA aún, es esperado).
- [ ] **14.4 — Selector de modelo LLM.** El desplegable de modelos cambia la etiqueta seleccionada.
- [ ] **14.5 — Panel RAG (local).** Subir un fichero lo añade a la lista con estado "Pendiente de
  indexar"; el botón de papelera lo elimina. (Solo estado local, sin backend.)
- [ ] **14.6 — Ajustes.** El icono de configuración lleva a `/ai-scanner/settings`.

---

## 15. Sesión, rutas y robustez

- [x] **15.1 — Refresco proactivo de token.** El token de acceso dura ~60 min y se refresca en
  cliente. Prueba a dejar la sesión abierta un rato y seguir navegando: no debería expulsarte de
  forma inesperada. (No hay refresh token; tras el límite → re-login.)
- [x] **15.2 — Acceso directo por URL a detalle.** Copia la URL de un `/investments/{id}` o
  `/market/{id}` y ábrela en una pestaña nueva (con sesión). Carga correctamente y el breadcrumb
  funciona.
- [x] **15.3 — Rutas privadas sin sesión.** Cierra sesión y prueba a entrar por URL directa a
  `/dashboard`, `/expenses`, `/investments`, `/market`, `/profile`, `/contacts`, `/emails` → todas
  redirigen a login.
- [x] **15.4 — Errores del backend.** Si paras el backend a mitad de navegación, las páginas deben
  mostrar su **estado de error** ("Error al cargar…"), no una pantalla en blanco ni un crash.
- [x] **15.5 — Formato multimoneda.** Los importes se muestran con el formato de su moneda (€, $, £,
  Fr, ¥) y separadores es-ES (coma decimal).

---

## 16. Responsive / móvil

Repite los flujos clave con el navegador en tamaño móvil (DevTools → ~375px) **y**, si puedes, en un
dispositivo real:

- [x] **16.1 — Navbar móvil.** El menú hamburguesa abre/cierra la navegación. En móvil, los botones
  de auth navegan a `/login` y `/register` (páginas físicas, no el panel flotante).
- [x] **16.2 — Tablas → tarjetas.** En Gastos y en Valoraciones, la tabla de desktop se convierte en
  tarjetas de 2 líneas en móvil.
- [x] **16.3 — Modales.** Los modales (transacción, holding, venta, valoración, cartera) se ven
  centrados, con scroll interno si no caben, y la X cierra.
- [ ] **16.4 — Selects dentro de modales.** Abrir un `Select` dentro de un modal en móvil no debe
  provocar parpadeos raros que hagan desaparecer el formulario. *(Síntoma vigilado en iteraciones
  previas — reportar si reaparece.)*
- [x] **16.5 — Combobox de empresa en móvil.** El typeahead de "Añadir holding" funciona y es usable
  con teclado en pantalla.

---

## Costuras y deudas conocidas (NO son bugs)

Estas limitaciones están documentadas y son esperadas — no las registres como bugs salvo que se
comporten peor de lo descrito:

1. **Venta FIFO a nivel empresa** — vender un holding consume lotes de esa empresa en orden FIFO
   (comportamiento permanente, no una carencia).
2. **Empresas sin editar/borrar** — el catálogo se lista, ve y crea, pero no se edita ni borra
   (backend sin `PUT`/`DELETE /companies/{id}`).
3. **Valoraciones sin ver-detalle ni borrar** — se listan, crean y grafican, pero no hay
   `GET{id}` ni `DELETE`.
4. **Sin búsqueda server-side de empresas** — el combobox de "añadir holding" filtra en cliente
   sobre un máximo de 100 empresas.
5. **AI Scanner sin backend** — chat y RAG son demo local; no hay `POST /ai/chat` ni ingesta real.
6. **Contacto / Emails simulados** — el envío de emails no sale de verdad; se guarda un registro en
   BD que alimenta las pantallas de Contacto y Emails.
7. **`npm run lint` no ejecutable** — `eslint` no está declarado en `package.json` (deuda de
   tooling, no afecta al runtime).

---

## Registro de bugs y observaciones

Apunta aquí cualquier desviación, idea de mejora o cambio que quieras. Prioridad sugerida:
**Alta** (bloquea/rompe) · **Media** (funciona mal pero hay workaround) · **Baja** (cosmético/mejora).

| # | Sección / Paso | Qué esperabas | Qué pasó | Prioridad | ¿Bug o cambio? |
|---|----------------|---------------|----------|-----------|----------------|
| 1 |  1 / 3         | Hemos de cambiar Alcance | Nos olvidamos | Alta | cambio |
| 2 |  1 / X         | Sin Errores | Errores menores vercel y icons | Baja | Bug |
| 3 |  3 / 5         | Fechas Regionales o UTC indicado | Fechas UTC | Baja | Bug |
| 4 |  6 /3          | 01 / Fecha / Año de la seleccionada en parrilla | fecha actual (solo si parrilla en Mes/Año actual) | Media | Bug |
| 5 |   10 / 2.5     | No permitir fechas futuras | Fechas futuras insertadas | Media | Bug |
| 6 |   12 / 3       | No permitir fechas futuras | Fechas futuras insertadas | Media | Bug |
| 7 |                |               |          |           |                |
| 8 |                |               |          |           |                |

---

## Resumen de cobertura

| Área | Secciones |
|------|-----------|
| Entorno / arranque | §0 |
| Público (landing, alcance, auth panel, rutas protegidas) | §1 |
| Auth (registro, login/logout, sesión) | §2, §3, §15 |
| Estados vacíos | §4 |
| Perfil | §5 |
| Gastos e ingresos (CRUD, filtros, paginación, gráficas) | §6, §7, §8 |
| Dashboard | §9 |
| Inversiones (carteras + holdings + venta FIFO) | §10 |
| Mercado (empresas) | §11 |
| Valoraciones (serie + alta + listado) | §12 |
| Contacto / Emails | §13 |
| AI Scanner | §14 |
| Responsive / móvil | §16 |
| Deudas conocidas | — |
