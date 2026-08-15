# PLAN DE TRABAJO — Uenta POS

> Documento maestro de ejecución. Es un **merge** de:
> - Roadmap de `PROJECT.md` (§9) — Fase 0/1/2/3
> - Plan del recibo de `design-system/pages/venta.md` (§6.1) — pasos 1–6
> - Deuda técnica y recomendaciones de la revisión de 13-ago-2026
>
> Regla: **cada paso es un entregable verificable** (tests / UIA / build / hardware).
> Nada se da por hecho sin su verificación — igual que la disciplina de commits
> `docs: venta.md — ...` + verificación que ya usa el proyecto.

---

## Principios de ordenamiento

1. **Cimientos antes que features.** El pricing único y la deuda técnica van primero
   porque el próximo feature (impresión del recibo) expondría cualquier descuadre
   entre lo que ve el cajero y lo que se persiste.
2. **Un paso reemplaza una deuda existente o cierra un hueco del plan anterior.**
   Cada paso indica su *Merge/Reemplaza* para no dejar duplicados ni pasos muertos.
3. **Verificación explícita por paso.** Si un paso no cambia el comportamiento visible
   (refactor puro), su verificación es "build 0/0 + tests verdes + UIA sin regresión".
4. **Lo que ya está hecho no se rehace.** Fase 0 y el motor de recibo (pasos 1/2/4 de
   venta.md §6.1) se dan por cerrados; el plan arranca en lo pendiente.

---

## Fase 0 — Cerrada (logrado, no rehacer)

- Esqueleto 4 capas + ejemplo punta a punta (venta → SQLite → `Result<T>`).
- Rediseño de la pantalla de venta al modelo **TICKET-CENTERED** (venta.md §9, pasos 1–3).
- Rediseño de descuentos (%/RD$ en línea + global fijo, commit 79c2fd6).
- Motor de recibo: `ReceiptContentBuilder` ✅ · `EscPosEncoder` ✅ · `ReceiptPdfGenerator` ✅.

## Fase 0.5 — Cerrada ✅ (13-ago-2026)

- **P0.1** ✅ `CartCalculator` en `POS.Application/Sales` (fuente única de pricing: línea →
  neto → global → total → ITBIS). `SaleService`, `CartLineViewModel` y
  `SaleViewModel.RecalculateTotals` delegan en él. Tests golden + test de paridad.
- **P0.2** ✅ `SaleView.xaml` (76KB) partido en 7 UserControls: `TicketLinesView`,
  `TicketLinesView`, `TicketEntryView`, `TotalsPanelView`, `CatalogPopupView`,
  `PaymentModalView`, `ResultModalView`. `SaleView.xaml` = cascarón de 129 líneas.
- **P0.3** ✅ `NavigationService.NavigateToAsync` → `Task`; invocadores con try/catch.
- **P0.4** ✅ Secuencia atómica: entidad `Sequence` + `UPDATE ... RETURNING` en transacción
  (migración `AddSaleSequence`). Números consecutivos sin huecos; test de venta fallida no
  quema número.
- **P0.5** ✅ Estilos/tokens consolidados en App.xaml; grep de hex en vistas = vacío
  (solo App.xaml).
- **Estado actual: build 0/0, 51/51 tests verdes, UIA/smoke OK (app abre sin crash,
  migración aplicada).**

---

## Fase 0.5 — Cimientos técnicos (NUEVA — previa a cualquier feature)

> Razón de ser: deuda identificada en la revisión. Son pasos chicos, de bajo riesgo,
> que destraban la correctitud de Fase 1. El #1 es prerrequisito moral del recibo.

### P0.1 — Motor de pricing único en Application

- **Objetivo:** extraer TODO el cálculo de venta a una clase pura (ej. `CartCalculator`
  en `POS.Application/Sales`) que usen `SaleService` y el ViewModel por igual:
  línea (gross → descuento %/RD$ → total) → subtotal neto → descuento global → total →
  desglose ITBIS. Cero matemática de dinero en el ViewModel.
- **Merge/Reemplaza:** la duplicación actual entre `SaleViewModel.RecalculateTotals`
  (SaleViewModel.cs:623) + `CartLineViewModel.LineDiscount` (SaleViewModel.cs:54) y
  `SaleService.CreateSaleAsync` (SaleService.cs:72). El orden documentado
  "línea → neto → global → total" (venta.md) queda implementado en UN solo lugar.
- **Verificación:** tests puros del calculator (golden: sin descuento, % dinámico,
  RD$ fijo, global supera subtotal, ITBIS desglose) + **test de paridad**
  "preview del ViewModel == venta persistida" para el mismo carrito. Build 0/0.
- **Nota:** elimina el riesgo #1 de la revisión (cliente ve un total, recibo muestra otro).

### P0.2 — Dividir `SaleView.xaml` (76KB) en UserControls

- **Objetivo:** partir el monolito en vistas componibles: `CartLineView` (fila + controles
  + panel descuento), `TicketEntryView` (línea de entrada + dropdown + aviso),
  `TotalsPanelView` (totales + descuento global + métodos + COBRAR),
  `PaymentModalView`, `ResultModalView`, `CatalogPopupView`.
- **Merge/Reemplaza:** los 33 estilos/templates del archivo actual; los estilos
  compartidos (MethodChip*, MoneyText, SectionTitle…) migran a App.xaml.
- **Verificación:** build 0/0 + UIA end-to-end sin regresión (agregar → cobrar →
  resultado) + capturas. Sin cambios visuales: refactor puro.
- **Nota:** condición de entrada para escalar Fase 1 sin dolor.

### P0.3 — Eliminar `async void` frágil

- **Objetivo:** `NavigationService.NavigateTo` pasa a `Task` (NavigationService.cs:27) y
  el invocador lo maneja (fire-and-forget solo con try/catch). `async void` queda
  reservado a event handlers con try/catch (el patrón actual de `ScheduleEntrySearch`).
- **Merge/Reemplaza:** el `async void` de `NavigateTo` que tumba la app si
  `OnNavigatedToAsync` lanza.
- **Verificación:** test de arranque con DB rota → mensaje de error, no crash;
  navegación entre pantallas sin excepciones no capturadas.

### P0.4 — Secuencia de numeración de venta robusta

- **Objetivo:** reemplazar `MaxAsync + 1` (SaleRepository.cs:15) por una tabla
  `Sequence` (año, último número) con `UPDATE ... RETURNING` en transacción
  `BEGIN IMMEDIATE` (SQLite). Números consecutivos sin huecos por fallo parcial.
- **Merge/Reemplaza:** la condición de carrera actual (dos ventas concurrentes → mismo
  número, el índice único revienta en runtime).
- **Verificación:** test de concurrencia (N ventas paralelas → N números únicos
  consecutivos); índice único se mantiene; build 0/0.

### P0.5 — Consolidar estilos/tokens en App.xaml

- **Objetivo:** un solo hogar para los recursos. Mover `MethodChipBase/Cash/Card/
  Transfer`, `MoneyText`, `SectionTitle` y otros a App.xaml; cero hex hardcodeado en
  vistas (regla dura de MASTER.md).
- **Merge/Reemplaza:** la dispersión actual (estilos de botón duplicados entre
  App.xaml `PaymentButton` y SaleView.xaml `MethodChip*`).
- **Verificación:** grep de `#[0-9A-Fa-f]{6}` en `*.xaml` (fuera de App.xaml) = vacío;
  build 0/0; UIA sin regresión.

---

## Fase 1A — Recibo real (cierra venta.md §6.1, pasos 3 / 5 / 6)

> ✅ **FASE CERRADA (15-ago-2026): P1.1 + P1.2 + P1.3 completos.** Prerrequisito
> P0.1 (el recibo es exactamente donde se notaría un descuadre) ya estaba.

### P1.1 — Impresora térmica ESC/POS real (P/Invoke winspool.drv)

- **Objetivo:** `RawPrinterHelper` adaptado: enumerar impresoras del sistema y enviar
  bytes por nombre. Nueva implementación de `IReceiptPrinter` (real, configurable) que
  usa `EscPosEncoder` + `ReceiptContentBuilder` (ya hechos).
- **Merge/Reemplaza:** `ConsoleReceiptPrinter` como printer por defecto pasa a
  segundo plano (dev). Paso 3 de venta.md §6.1.
- **Verificación:** imprimir recibo de prueba en impresora física (dev); error claro si
  el nombre no existe; build 0/0 + tests de encoder intactos.
- **Estado:** ✅ HECHO (15-ago-2026). `RawPrinterHelper` (P/Invoke winspool.drv:
  OpenPrinter/StartDoc/StartPage/Write/End… + enumeración por `PrinterSettings`) en
  `POS.Infrastructure/Printing`. `ThermalReceiptPrinter` = `IReceiptPrinter` real
  (nombre desde Ajustes, N copias, `EscPosEncoder` + `ReceiptContentBuilder`).
  Verificado en **impresora virtual "Microsoft Print to PDF"** (flujo winspool completo,
  530 bytes) + error claro con nombre inexistente. Los tests usan `ConsoleReceiptPrinter`
  explícito (nunca tocan impresoras del sistema). **Pendiente hardware: probar en
  impresora física ESC/POS real cuando llegue** (anotado; el flujo ya está validado).

### P1.2 — Conectar impresión al cobro + modal de resultado completo

- **Objetivo:** `ConfirmPaymentAsync` imprime tras crear la venta y el modal de resultado
  gana `[Imprimir]` `[PDF]` `[Nueva venta (Enter)]]`. PDF usa `ReceiptPdfGenerator`
  (SaveFileDialog).
- **Merge/Reemplaza:** el modal actual ("Venta completada" sin botones de recibo);
  paso 5 de venta.md §6.1.
- **Regla dura:** la impresión **nunca falla la venta** — si la impresora falla, la venta
  ya quedó persistida; se muestra un aviso no bloqueante (offline-first).
- **Verificación:** UIA flujo completo (efectivo → modal → reimprimir → PDF → nueva
  venta); simular fallo de impresora → aviso, venta intacta.
- **Estado:** ✅ HECHO (15-ago-2026). Modal de resultado con `[Imprimir]` (icono
  impresora) `[PDF]` (SaveFileDialog → `ReceiptPdfGenerator.Generate`) `[Nueva venta]`
  (Enter sigue mapeado). `ConfirmPaymentAsync` dispara auto-impresión tras persistir.
  Regla dura probada por UIA: sin impresora configurada → aviso rojo "No hay impresora
  configurada. Ábrala en Ajustes." + venta intacta + modal abierto. Verificado con
  ui-reviewer (botones alineados, texto no cortado).

### P1.3 — Ajustes: impresora + datos del negocio

- **Objetivo:** pantalla de Ajustes (primera de Fase 1): selector de impresora,
  auto-imprimir, nº de copias + datos del negocio (nombre, RNC, dirección, pie de
  recibo). Se persiste en `Setting` (clave/valor) y los recibos lo reflejan.
- **Merge/Reemplaza:** paso 6 de venta.md §6.1 y el hueco de "Ajustes" del scope de Fase 1.
- **Verificación:** selección persiste entre reinicios; el pie de recibo aparece en
  térmico/PDF; build 0/0.
- **Estado:** ✅ HECHO (15-ago-2026). Entidad `Setting` (clave/valor) + `SettingRepository`
  (upsert por clave) + migración `AddSettings`. `SettingsService` con lectura tipada
  (Get/GetBool/GetInt + `GetReceiptSettingsAsync` con clamp de copias 1-9). Pantalla
  `SettingsView` accesible desde la barra de título (⚙) y el sidebar; `SettingsViewModel`
  enumera impresoras del sistema (`RawPrinterHelper.GetInstalledPrinters`). `ReceiptContentBuilder`
  acepta `ReceiptSettingsDto` opcional → encabezado negocio (nombre/RNC/dirección) + pie
  personalizado, compartido por térmica y PDF (comportamiento idéntico sin settings).
  Verificado: persistencia entre reinicios por UIA (nombre + pie sobreviven),
  recibo renderizado con datos reales de la DB, build 0/0, 79/79 tests.

---

## Fase 1B — Usuarios, permisos y caja (núcleo operativo)

> Decisiones tomadas (15-ago-2026): gestión de usuarios SOLO Admin (UI mínima en P2.1);
> auditoría = datos + tests (vista va en Fase 1D con reportes); retiro de caja libre con
> motivo obligatorio; cierre con conteo de EFECTIVO (tarjeta/transferencia se listan como
> referencia, no se cuentan — van al banco); seed `admin/admin123`, `supervisor/super123`,
> `cajero/cajero123` sin forzar cambio (demo).

### P2.1 — Login local + roles + auditoría

- **Objetivo:** usuarios (Admin/Supervisor/Cajero) con contraseña local, login al
  arranque, permisos por rol (costos, cierre de caja, tope de descuento) y `AuditLog`
  (quién hizo qué y cuándo: ventas, ajustes, cambios de precio).
- **Merge/Reemplaza:** `DemoUserId = 1` (SaleViewModel.cs:198) y la ausencia de
  auditoría.
- **Verificación:** login OK/fallo, permisos por rol aplicados en comandos (CanExecute),
  entradas de audit log tras vender/ajustar.

#### Matriz de permisos (decisión 15-ago-2026)

| Permiso | Admin | Supervisor | Cajero |
|---|---|---|---|
| Vender / cobrar | ✅ | ✅ | ✅ |
| Ver costos | ✅ | ❌ | ❌ |
| Cerrar caja | ✅ | ✅ | ❌ |
| Gestionar productos + categorías | ✅ | ✅ | ❌ |
| Ajustar stock | ✅ | ✅ | ❌ |
| Gestionar usuarios | ✅ | ❌ | ❌ |
| Ver auditoría | ✅ | ❌ | ❌ (vista en Fase 1D) |
| Tope descuento global | ∞ | 25% | 10% (configurable `Setting`) |

#### Pasos

- **P2.1a — Dominio + auth:** `User` (Username único CI, DisplayName, PasswordHash
  PBKDF2+salt, Role, IsActive, CreatedAt), `Role` enum, `AuditLog` (UserId, Action,
  Detail, CreatedAt), `IPasswordHasher`, `AuthService.ValidateAsync`. Seed 3 usuarios.
  Tests: hash→verifica, password incorrecta falla.
- **P2.1b — Sesión:** `ICurrentSession` singleton (User activo) reemplaza
  `DemoUserId = 1`; `MainWindowViewModel` expone `CurrentUser`.
- **P2.1c — Login UI:** `LoginView` (usuario, contraseña, Enter, error inline);
  arranque → login → venta. Header real (nombre + rol). UIA login OK/fallo.
- **P2.1d — Permisos en comandos:** `CanExecute` por rol + sidebar dinámico
  (Cajero: solo Ventas) + tope de descuento en `SaleService` y UI. Tests.
- **P2.1e — Gestión de usuarios (Admin):** pantalla mínima — lista, crear
  (username/display/rol/contraseña), activar/desactivar, reset de contraseña.
- **P2.1f — Auditoría (datos + tests):** `AuditService` inyectado en `SaleService`
  (venta), `InventoryService` (ajuste), `ProductService` (cambio de precio),
  `AuthService` (login OK/fallo). Vista → Fase 1D.
- **Estado:** ✅ HECHO (15-ago-2026). Entidades `User`/`Role`/`AuditLog` + `Permissions`
  (matriz central: Admin/Supervisor/Cajero + `ManageSettings` nuevo, usado en P2.1d).
  `PasswordHasher` PBKDF2+salt, `AuthService.ValidateAsync` (login OK/fallo con audit),
  `ICurrentSession` (sesión activa) + `SignInAsync/SignOutAsync` con notificación
  `SessionChanged`. `LoginView`/`LoginViewModel` (arranque → login → venta; header real
  con nombre + rol + avatar inicial; logout). Sidebar dinámico por rol (Cajero solo
  Ventas) + header/footer con usuario real + botones de caja por permiso
  (`CanCloseCash`). `UsersView`/`UsersViewModel` (Admin): lista, crear/editar
  (username/display/rol/contraseña), activar/desactivar, reset contraseña, no borra al
  propio usuario. Tope de descuento por rol aplicado (∞/25%/10%). Build 0/0, 106/106
  tests, smoke UIA (login admin/cajero, sidebar, permisos, logout) OK.

### P2.2 — Caja: apertura/cierre

- **Objetivo:** apertura (efectivo inicial), retiros, cierre (conteo + diferencia) y la
  regla de "una caja abierta por usuario". La venta se asocia a la caja.
- **Merge/Reemplaza:** `CashSessionId = null` (SaleViewModel.cs:769) y la regla 4.4 de
  venta.md (badge "Caja # · abierta/cerrada" en el header + COBRAR bloqueado si caja
  cerrada + hint "Abra la caja para cobrar").
- **Verificación:** UIA apertura → venta → cierre → diferencia; COBRAR deshabilitado con
  caja cerrada; test de cierre con conteo correcto.

#### Reglas (PROJECT §7.3 + venta.md §4.4)

- Solo se cobra con caja **abierta** del usuario activo (`CASH_CLOSED`).
- Una caja abierta por usuario (validación + índice).
- Retiro: libre, **motivo obligatorio**.
- Cierre: conteo de efectivo; esperado = inicial + Σ(ventas efectivo) − Σ(retiros);
  diferencia = conteo − esperado. Tarjeta/transferencia se listan como referencia.

#### Pasos

- **P2.2a — Dominio + servicio:** `CashSession` (UserId, OpenedAt, InitialCash,
  ClosedAt?, FinalCount?, Difference?, Status) + `CashWithdrawal` (Amount, Reason,
  CreatedAt) + `Sale.CashSessionId`. `CashSessionService`: Open (rechaza si abierta),
  Withdraw (motivo), Close (conteo + diferencia). Tests.
- **P2.2b — Conectar venta:** `CreateSaleRequest.CashSessionId` desde sesión activa;
  `SaleService` rechaza sin caja abierta. Tests.
- **P2.2c — UI caja:** badge "Caja # · abierta" en header + modales apertura (efectivo
  inicial) / retiro (monto + motivo) / cierre (conteo + diferencia en vivo). COBRAR
  bloqueado + banner "Abra la caja para cobrar" (regla 4.4).
- **P2.2d — Verificación UIA:** apertura → venta → retiro → cierre → diferencia;
  recibo muestra "Caja #".
- **Estado:** ✅ HECHO (15-ago-2026). Entidades `CashSession` (UserId, OpenedAt,
  InitialCash, ClosedAt?, FinalCount?, Difference?, Status) + `CashWithdrawal` (Amount,
  Reason obligatorio) + `Sale.CashSessionId`. `CashSessionService`: Open (rechaza si
  hay abierta — `CASH_ALREADY_OPEN`), Withdraw (motivo), Close (conteo + esperado =
  inicial + Σ efectivo − Σ retiros + diferencia), GetOpenByUserAsync. `SaleService`
  rechaza sin caja abierta (`CASH_CLOSED`) y con caja de otro usuario
  (`CASH_NOT_OWNED`). `CashSessionTracker` singleton compartido (badge "Caja # · 
  abierta/cerrada" + "Fondo RD$ X · Efectivo RD$ Y" en header) entre
  `MainWindowViewModel` (comandos de caja globales) y `SaleViewModel` (bloqueo COBRAR
  con caja cerrada + `CashSessionId` en la venta + refresh del badge tras cobrar).
  `CashModalsView` global: apertura (efectivo inicial), retiro (monto + motivo),
  cierre (conteo → diferencia + toast con resultado). Build 0/0, 106/106 tests, UIA
  completo OK: abrir (500) → venta (100) → badge Efectivo 100.00 → retiros (2×20) →
  cierre conteo 555 → "Caja #1 cerrada · Diferencia RD$ -5.00" → COBRAR/EFECTIVO
  deshabilitados con caja cerrada → logout/login cajero con sidebar restringido.**
  P2.2d completo: `SaleDto.CashSessionId` propagado y el recibo muestra "Caja #: N"
  (térmica, PDF y consola comparten el motor `ReceiptContentBuilder`). Build 0/0,
  108/108 tests (2 nuevos: con/sin caja).

---

## Fase 1C — Catálogo completo + inventario

### P3.1 — Catálogo completo (variantes + alertas)

- **Objetivo:** afinación del CRUD existente: variantes simples (duplicar y editar,
  regla P9), activo/inactivo en ficha, alerta de stock mínimo (badge), y consistencia
  de SKU/código de barras.
- **Merge/Reemplaza:** el CRUD básico ya existente (ProductList/ProductEdit) se
  extiende, no se reescribe.
- **Verificación:** crear producto → variante duplicada → SKU único; producto inactivo
  no aparece en venta (ya lo rechaza `SaleService`); UIA + tests.
- **Estado:** ✅ HECHO (14-ago-2026). Incluye: búsqueda separada venta (solo activos) /
  gestión (incluye inactivos + reactivar), validación de barcode único con SKU/barcode
  normalizados (trim + mayúsculas), ficha con toggle activo + duplicar (regla P9:
  nombre con "(copia)", SKU/barcode null, stock 0) + margen en vivo, gestor de
  categorías (crear/renombrar inline) en el catálogo, y rediseño de la lista con el
  estilo de card del ticketing. Build 0/0, 59/59 tests, smoke UIA (catálogo →
  categorías → ficha) OK. Catálogo rediseñado se entrega con iconos `Geometry`
  temporales (trazabilidad #11).

### P3.1b — Categorías: desactivación + nombre único + conteo

- **Objetivo:** completar el ciclo de vida de las categorías. La eliminación es
  **soft-delete** (desactivar/reactivar, `IsActive` ya existía en la entidad sin uso):
  se ocultan de la navegación en venta pero el historial y los productos quedan
  intactos. Nombre único case-insensitive en crear/renombrar. Conteo de productos por
  categoría en el gestor.
- **Merge/Reemplaza:** el gestor inline de P3.1 (crear + renombrar) → ahora también
  desactiva/reactiva y muestra cuántos productos usa cada categoría. `GetAllAsync` pasa
  a devolver `IsActive` + `ProductCount`; venta y ficha usan `GetAllActiveAsync`
  (venta nunca muestra categorías inactivas; la ficha solo muestra la inactiva si ya
  está asignada al producto, para poder reasignar).
- **Reglas de negocio:** desactivar una categoría NO desactiva sus productos (siguen
  vendibles por código/escáner; solo desaparecen de los chips de categoría en venta).
  Al desactivar con productos, el diálogo avisa el conteo antes de confirmar.
- **Decisión de permisos (para P2.1):** gestionar categorías **hereda** el permiso de
  gestionar productos (mismo rol, sin permiso extra). Rol Cajero no ve gestión de
  productos ni de categorías (sidebar dinámico por rol en P2.1).
- **Verificación:** desactivar → no aparece en `GetAllActiveAsync` ni en venta;
  reactivar → vuelve; crear/renombrar con nombre duplicado (distinta capitalización)
  falla `NAME_DUPLICATED`; conteo correcto; build 0/0; UIA gestor + diálogo de
  confirmación.
- **Estado:** ✅ HECHO (14-ago-2026). Build 0/0, 65/65 tests, smoke UIA (desactivar
  con confirmación → botón reactivar visible → reactivar → vuelve a 3 activas) OK.

### P3.2 — Movimientos de inventario

- **Objetivo:** movimientos de entrada/salida/ajuste con motivo y usuario; el stock
  refleja los movimientos y todo queda en `AuditLog`.
- **Merge/Reemplaza:** la desactivación sin registro actual; el inventario v1 que hoy
  entra "por ajustes manuales" queda formalizado.
- **Verificación:** ajuste de stock → cantidad cambia → audit log con motivo/usuario;
  tests.
- **Estado:** ✅ HECHO (15-ago-2026). Entidad `StockMovement` (tipo, cantidad, motivo,
  usuario, `StockAfter`, fecha) + `InventoryService.AdjustStockAsync` (Entry suma, Exit
  resta — permite negativo P3 —, Adjustment fija el valor) y `GetByProductAsync`
  (historial). Persistencia atómica: movimiento + stock en el mismo contexto. En la
  ficha del producto (edición) el stock ya NO se edita directo: se muestra en un badge
  y se ajusta con el panel "Ajustar stock" (tipo/cantidad/motivo, motivo obligatorio).
  `UpdateAsync` dejó de tocar `Stock`. Build 0/0, 73/73 tests, smoke UIA (catálogo →
  ficha → panel de ajuste → validación de motivo) OK.

---

## Fase 1D — Clientes, reportes y operación

### P4.1 — Clientes (CRM básico) + historial

- **Objetivo:** registro de clientes (nombre, teléfono, RNC/cédula, correo), selector
  en la venta y pantalla de historial de compras.
- **Merge/Reemplaza:** `CustomerId = null` (SaleViewModel.cs:769); el modo "Anónimo
  fijo" de venta.md §6 decisión 3.
- **Verificación:** registrar cliente → vender asociado → historial muestra la compra;
  UIA + tests.

### P4.2 — Reportes / Dashboard

- **Objetivo:** ventas del día (total, # tickets, promedio), por periodo/producto/
  cajero y productos más vendidos.
- **Merge/Reemplaza:** nada (feature nueva de Fase 1).
- **Verificación:** datos de la DB demo → cifras correctas verificadas por consulta SQL
  independiente.

### P4.3 — Backup/restore SQLite

- **Objetivo:** exportar/importar la base (archivo) desde Ajustes, con aviso de
  confirmación y protección ante corrupción.
- **Merge/Reemplaza:** el hueco de "Operación" del scope de Fase 1.
- **Verificación:** exportar → borrar DB → importar → datos intactos; restaurar con
  archivo inválido → error claro, DB actual intacta.

---

## Fase 2 — (roadmap, sin detalle)

Devoluciones / notas de crédito · compras y proveedores · e-CF (DGII). Se planifican
con el mismo formato por pasos al entrar.

## Fase 3 — (roadmap, sin detalle)

Fidelización · reportes avanzados · multi-sucursal · `POS.Api` + app móvil.

---

## Trazabilidad: deuda de la revisión → paso del plan

| # | Deuda / recomendación (revisión 13-ago) | Se resuelve en |
|---|------------------------------------------|----------------|
| 1 | Lógica de precios duplicada (UI vs SaleService) — riesgo de descuadre silencioso | **P0.1** |
| 2 | `SaleView.xaml` monolito 76KB (33 estilos) | **P0.2** |
| 3 | `async void` en `NavigationService` y handlers | **P0.3** |
| 4 | Numeración de venta `MaxAsync+1` (carrera) | **P0.4** |
| 5 | ViewModel con lógica crítica sin tests (verificación manual UIA) | **P0.1 + P0.3** |
| 6 | Estilos dispersos (PaymentButton vs MethodChip*) y hex en vistas | **P0.5** |
| 7 | Recibo no conectado al cobro (venta.md §6.1 pasos 3/5/6) | **P1.1–P1.3** |
| 8 | `DemoUserId`, `CashSessionId=null`, `CustomerId=null` | **P2.1 / P2.2 / P4.1** |
| 9 | Semántica de guardado inconsistente entre repos | **P0.1** (un solo flujo de persistencia) |
| 10 | Backup/restore y Ajustes pendientes del scope | **P1.3 / P4.3** |
| 11 | Iconos: `Geometry` inline (estilo feather) → archivos SVG reales (los provee Bryan) | **Transversal** — Fase 1D o antes de publicar |

---

## Reglas transversales (vigentes desde Fase 0)

- Cada paso terminado → commit con su `docs:` correspondiente (patrón del proyecto).
- Design system: nada de hex hardcodeado, estilos en App.xaml, checklist pre-entrega de
  `uenta-pos/MASTER.md` y `pages/venta.md`.
- Cero lógica de negocio en code-behind/ViewModel que no pase por Application.
- Montos `decimal` → `N2` tabular; fechas `DateTimeOffset` vía `IClock`.