# Pantalla de Venta (POS) — Page Override

> Este archivo **sobrescribe** a `MASTER.md` para la pantalla de venta del POS.
> Regla de la skill: si `pages/venta.md` existe, sus reglas mandan sobre el Master.
> Aplicable a: `POS.Desktop` (WPF, .NET 9) — pantalla principal del cajero.

---

## 0. Qué es esta pantalla

Núcleo operativo del sistema: el cajero arma el ticket (escáner, escritura
directa de código/nombre o catálogo visual), ve los totales y cobra.
Velocidad y cero fricción: cada venta debe tomar segundos, no minutos.

**Modelo (aprobado por Bryan 10-ago-2026): TICKET-CENTERED (modelo B).**
El catálogo ya NO está siempre visible. La pantalla es de pago completo:
el ticket ocupa el protagonismo y el catálogo se consulta **a demanda**:
1. **Escáner / código directo** — escribe el código y se rellena solo.
2. **Búsqueda por nombre** — dropdown de sugerencias mientras escribes.
3. **Catálogo visual (popup)** — búsqueda guiada con el grid de siempre.

Los 3 modos conviven; la cajera elige el que le ahorre tiempo (tras 50 ventas
del mismo producto, el código directo es lo más rápido).

**No aplican del MASTER:** el patrón de landing page, el motion GSAP (scroll
reveal) y los breakpoints responsive web. Esta es una app desktop de alta
densidad con ventana única maximizada.
**Línea estética (10-ago-2026):** Minimalismo Funcional (Swiss) — ver
`estetica-minimalista.md` y `uenta-pos/MASTER.md`.

---

## 1. Layout (wireframe)

Ventana única, mínimo 1280×800, maximizada por defecto. Header (48px) + cuerpo
con dos zonas: **ticket** (izquierda, ~60%) y **totales/cobro** (derecha, ~40%).

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ ● Uenta POS        Caja #12 · abierta        Cajero: Juan       12:45  ☰  │
├───────────────────────────────────────────────┬──────────────────────────────┤
│  VENTA #1024     Cliente: [Anónimo ▾]  [3]   │  SUBTOTAL         50,500.00  │
│                                               │  ITBIS 18%         7,703.39  │
│  ┌─────────────────────────────────────────┐ │  DESCUENTO           -0.00  │
│  │ Café 12oz                  x2    180.00 │ │  ────────────────────────    │
│  │   [−] [+] [✕]                          │ │  TOTAL        RD$ 58,203.39  │
│  ├─────────────────────────────────────────┤ │                              │
│  │ Empanada de pollo            x1     40.00│ │  [% Descuento] [👤 Cliente] │
│  │   [−] [+] [✕]                          │ │  ────────────────────────    │
│  ├─────────────────────────────────────────┤ │  [EFECTIVO] [TARJETA]       │
│  │ Refresco 12oz                x1     35.00│ │  [TRANSFERENCIA] [MIXTO]    │
│  │   [−] [+] [✕]                          │ │  ────────────────────────    │
│  ├─────────────────────────────────────────┤ │  [      COBRAR  F8      ]   │
│  │ ▸ [Escribe código o nombre…        ]    │ │                              │
│  │   (dropdown de sugerencias ↓)          │ │                              │
│  └─────────────────────────────────────────┘ │                              │
│                                               │                              │
│  [+ Agregar item]        [Catálogo  F2]      │                              │
└───────────────────────────────────────────────┴──────────────────────────────┘
```

### Distribución

| Zona | Ancho | Contenido |
|------|-------|-----------|
| Header | 100% (48px) | Marca, estado de caja, usuario, reloj, menú |
| Izquierda (ticket) | ~60% | Líneas del ticket + línea de entrada + acciones |
| Derecha (cobro) | ~40% | Totales, métodos de pago, COBRAR |

### Jerarquía visual

1. **TOTAL** (derecha, tamaño display, bold) — lo que el cliente debe pagar.
2. **COBRAR** — botón más grande de la pantalla, color accent, F8.
3. **Línea de entrada activa** (la línea vacía del ticket) — siempre enfocada
   cuando el cajero agrega; es el corazón del modelo B.
4. Métodos de pago — visibles siempre (sin menús ocultos).

---

## 2. Tokens (adaptados a WPF)

### Colores (resources de App.xaml)

| Token | Hex | Uso WPF |
|-------|-----|---------|
| `PrimaryBrush` | `#059669` | Elementos activos, foco, selección (configurable) |
| `PrimaryDarkBrush` | `#047857` | Hover de primario / texto sobre Primary |
| `AccentBrush` | `#EA580C` | **COBRAR** (configurable) |
| `AccentDarkBrush` | `#C2410C` | Hover de COBRAR |
| `BackgroundBrush` | `#F8FAFC` | Fondo ventana (neutro, sin tinte) |
| `SurfaceBrush` | `#FFFFFF` | Cards, carrito, modales |
| `MutedBrush` | `#F1F5F9` | Superficies secundarias, hover |
| `BorderBrush` | `#E2E8F0` | Bordes y separadores (hairlines) |
| `TextPrimaryBrush` | `#0F172A` | Texto principal (contraste 12.5:1 sobre blanco ✓) |
| `TextSecondaryBrush` | `#64748B` | Texto secundario (4.76:1 sobre blanco ✓) |
| `DangerBrush` | `#DC2626` | Errores, eliminar, stock negativo |
| `WarningBrush` | `#D97706` | Avisos de stock (no bloqueante) |

Regla dura: **nada de hex hardcodeado en las vistas** — todo por `DynamicResource`
desde App.xaml.

### Tipografía

- **Números/montos:** `Rubik` (fallback `Segoe UI Variable Display`). Tabs numéricas.
- **Texto general:** `Nunito Sans` (fallback `Segoe UI Variable`).
- Escala (px): 11 UI secundaria · 13 cuerpo · 15 énfasis · 18 subtítulo ·
  24 título de sección · **32 total** · 20 botón COBRAR.
- Montos SIEMPRE `N2` con miles (RD$ 58,203.39), tabular para que no bailen.

### Espaciado (densidad 6/10)

`4 / 8 / 12 / 16 / 24 / 32` px. Padding estándar: 12px. Margen interno del
ticket: 16px. Fila de ticket: 48px mín. (target táctil).

### Sombras (mínimas — Minimalismo Funcional)

- Superficies de contenido (líneas, panel de totales, botones): **SIN sombra**.
- **Única excepción: modales/popups** (dropdown de sugerencias y catálogo:
  sombra suave + hairline; modales: sombra + scrim `#99000000`).

### Radio de esquinas

- Botones, inputs, líneas y cards: **6px**. Modales: 12px. Chips: 999px (píldora).

---

## 3. Componentes

### 3.1 Línea del ticket (item ya agregado)

- Fila de 48px mín.: `nombre (2 líneas máx.) · cantidad · precio de línea`.
- Controles en hover/selección: `[−] [+] [✕]` (quitar línea). Target ≥44px.
- Cantidad y precio alineados a la derecha, tabulares.
- Línea seleccionada: fondo `MutedBrush` + borde izquierdo 2px `PrimaryBrush`.
- **Sin campos editables inline** (decisión Bryan): editar precio final/descuento
  se afina sobre la marcha (ver §"Decisiones abiertas").

### 3.2 Línea de entrada (línea vacía — el input del modelo B)

- Última línea del ticket, con placeholder **"Escribe código o nombre…"**.
- **Mientras escribes:**
  - Entrada numérica (código/SKU/barras) → al coincidir UN producto, se rellena
    solo: nombre, precio, cantidad 1. Enter fuerza el match exacto.
  - Entrada de texto (nombre) → **dropdown de sugerencias** debajo de la línea
    (mismo mecanismo que el autocompletado de un IDE): muestra `nombre —
    precio — stock`. Debounce 250ms.
  - Coincidencia ambigua (varios productos con el mismo prefijo) → dropdown con
    todas las opciones; el cajero elige con ↑/↓ + Enter o click.
- **Reglas de línea vacía (aprobadas Bryan + Theo):**
  1. Solo puede existir **UNA** línea de entrada a la vez (la última).
  2. Si pierde el foco **sin contenido** → se descarta sola (nada de fantasmas).
  3. La venta **no avanza** (COBRAR deshabilitado) mientras exista una línea
     vacía o a medio llenar — red de seguridad ante cualquier caso borde.
     La línea pendiente se marca con borde `WarningBrush` + hint
     "Línea sin completar".
- **Escáner:** el foco SIEMPRE vive en la línea de entrada; el escáner (wedge
  de teclado) llena el producto completo y **el foco avanza solo a una nueva
  línea de entrada al final** (ver 4.1).

### 3.3 Dropdown de sugerencias

- Popup anclado bajo la línea de entrada: superficie blanca + hairline + sombra
  suave (excepción permitida). Items: `nombre — precio (— stock si bajo)`.
- Navegación ↑/↓ + Enter; Esc cierra sin seleccionar (la línea queda vacía y se
  descarta al perder foco). Max ~8 items visibles, scroll interno.
- Sin foco visible: NO. Con foco: item seleccionado con fondo `MutedBrush`.

### 3.4 Catálogo visual (popup de búsqueda guiada — F2)

- Modal (centrado, ~70% de la ventana, max 900×600): mismo grid de cards que
  el catálogo actual + su propio buscador + chips de categoría.
- **Click en card (o Enter con selección) agrega la línea al ticket SIN cerrar
  el popup** → permite agregar varios productos de corrido (caja llena, tanda
  de 5 productos: un solo popup).
- Cada card muestra precio siempre (+ badge de stock bajo si aplica).
- El popup muestra un contador "Agregados: N" para feedback.
- Esc cierra. F2 alterna abrir/cerrar. Al cerrar, el foco vuelve a la línea
  de entrada.

### 3.5 Panel de totales (derecha)

- Subtotal, ITBIS 18% desglosado, Descuento, separador, **TOTAL** (32px, bold).
- Aviso ámbar si algún ítem tiene stock insuficiente (P3: se vende igual, avisa).
- `[% Descuento]` y `[Cliente]` como botones ghost sobre los totales.

### 3.6 Métodos de pago (4, siempre visibles)

- EFECTIVO · TARJETA · TRANSFERENCIA · MIXTO — superficie blanca + hairline,
  44px+ alto. Hover: `MutedBrush` + borde `PrimaryBrush`.

### 3.7 COBRAR

- 100% del ancho del panel derecho, 52px, fondo **AccentBrush**, texto blanco,
  bold, F8. Hover: `AccentDarkBrush`. Sin sombra ni layout shift.
- **Disabled si:** ticket vacío, línea de entrada vacía/pendiente (regla 3.2),
  caja cerrada, sin permisos.

### 3.8 Header

- Izquierda: "Uenta POS". Centro: badge "Caja #12 · abierta" / "Caja cerrada".
- Derecha: usuario + rol, reloj, menú ☰ (navegación). A la derecha del todo:
  contador de líneas del ticket actual ("[3]") junto al folio de la venta.

### 3.9 Iconos

- **Prohibido emojis como iconos.** WPF: `Path`/`Geometry` vectoriales o
  `Segoe Fluent Icons` (Windows 11). Un solo set, un peso. Target visual ≥32px,
  área de click ≥44px.

---

## 4. Flujos de interacción

### 4.1 Agregar producto (3 vías, coexisten)

1. **Escáner (vía principal):** foco en línea de entrada → escanea → se rellena
   solo → **foco avanza a nueva línea de entrada al final** → loop continuo.
   Si el código ya está en el ticket: incrementa cantidad de esa línea (en vez
   de duplicar) — decisión a confirmar (ver abiertas).
2. **Código a mano / nombre:** escribe en la línea de entrada → match único se
   rellena solo; ambiguo → dropdown → ↑/↓ + Enter o click.
3. **Catálogo visual:** `F2` (o botón "Catálogo") → popup → click en cards →
   se agregan líneas → Esc. Foco vuelve a la línea de entrada.

Todas las vías terminan con el foco en la línea de entrada (loop de escaneo).

### 4.2 Cobrar (F8)

1. Modal de pago (centrado, 480px): método preseleccionado según último usado.
2. **Efectivo:** campo "Monto recibido" autofocus → **VUELTO** en vivo (verde,
   grande). Botón "Exacto" (F9) llena el monto del total.
3. **Tarjeta/Transferencia:** campo monto (datáfono manual — se registra el
   monto, P. hardware).
4. **Mixto:** dos campos (efectivo + resto) con barra de progreso de cobertura.
5. Confirmar → `SaleService.CrearVenta` (async, `AsyncRelayCommand` con
   `IsRunning`, botón deshabilitado + spinner mientras procesa).
6. Éxito → modal de recibo: [Imprimir] [PDF] [Nueva venta (Enter)]. Vuelto en
   pantalla hasta cerrar. **"Nueva venta" limpia el ticket y deja una línea de
   entrada nueva enfocada** (listo para la siguiente).
7. Error de negocio (`Result.Failure`): mensaje inline en el modal, sin
   excepción, sin crash. Stock insuficiente = warning, NO error (P3).

### 4.3 Descuentos

- Global: botón "% Descuento" sobre los totales → modal con input numérico;
  valida tope por rol (Cajero ≤10%, Supervisor ≤25%, Admin ∞ — configurable).
- Por línea / precio final: **pendiente de afinar** (ver abiertas).

### 4.4 Caja cerrada

- Banner rojo suave en header + COBRAR disabled + hint "Abra la caja para
  cobrar". Link directo a Apertura de caja.

---

## 5. Atajos de teclado (cajero = velocidad)

| Tecla | Acción |
|-------|--------|
| `Enter` | Confirmar match/sugerencia seleccionada; confirmar modal |
| `↑` / `↓` | Navegar dropdown de sugerencias |
| `F2` | Abrir/cerrar catálogo visual (popup) |
| `F8` | COBRAR |
| `F9` | Pago con monto exacto (en modal efectivo) |
| `Esc` | Cerrar popup/modal / descartar sugerencias |
| `+` / `-` | Subir/bajar cantidad de la línea seleccionada |
| `Supr` | Quitar línea seleccionada |
| `F1` | Ayuda de atajos |

Todo accionable por teclado (regla UX de la skill: keyboard navigation, High).

> ⚠️ El mapeo final de F4 (antes "descuento global") y demás se confirma cuando
> cerremos las decisiones abiertas de abajo.

---

## 6. Decisiones — RESUELTAS (11-ago-2026, con Bryan)

1. **Cantidad por escaneos repetidos:** el mismo producto escaneado/escrito de
   nuevo **incrementa la cantidad de la misma línea** (no duplica). Ticket más limpio.
2. **Descuento por producto:** la línea tiene descuento propio (aplica al total
   de la línea). Los permisos por rol se gestionan más adelante — ahora queda
   sin control de rol.
3. **Cliente:** a futuro ambos modos (Anónimo fijo + buscable en modal).
   **Ahora: solo Anónimo.**
4. **Atajos de teclado:** se definen en una fase dedicada más adelante. Por
   ahora solo el mínimo: Enter (confirmar), ↑/↓ (sugerencias), F2 (catálogo),
   F8 (COBRAR), Esc (cerrar). F4/F9 quedan fuera hasta esa fase.
5. **Recibo: PENDIENTE** — ver §6.1 abajo; Bryan pidió ser ilustrado antes de decidir.

---

## 6.1 Recibo — estado actual (11-ago-2026, para Bryan)

**Lo que existe hoy (Fase 0):**
- Puerto `IReceiptPrinter.PrintReceiptAsync(SaleDto)` en `POS.Application\Abstractions`.
- Implementación `ConsoleReceiptPrinter` (Fase 0): imprime un recibo ASCII de
  42 chars de ancho **en la consola** (solo devs, validación del flujo).
  Formato: encabezado UENTA — RECIBO DE VENTA, recibo #, fecha, items
  (nombre + cantidad × precio + total), subtotal, ITBIS 18%, descuento (solo
  si > 0), TOTAL, pagos por método, "¡Gracias por su compra!".
- **NO hay PDF** (el scope lo promete, pero no hay implementación ni paquete).
- **NO está conectado al flujo de cobro**: `ConfirmPaymentAsync` crea la venta
  y abre el modal de resultado, pero **nunca llama a imprimir**. La única
  referencia viva está en tests.
- **Modal de resultado** (lo que ve la cajera): "Venta completada" + venta # +
  hora, TOTAL grande, VUELTO, warnings de stock y botón "Nueva venta (Enter)".
  No hay botones [Imprimir]/[PDF] — eran un deseo de la spec vieja, nunca implementados.

**Ruta a Fase 1 (aprobada por Bryan 11-ago, ejecución por pasos):** térmica ESC/POS
80mm real vía P/Invoke a winspool.drv + exportar PDF. UI del modal de resultado
agregaría [Imprimir] [PDF] [Nueva venta].

**Plan por pasos (verificación rápida en cada uno):**

| # | Paso | Estado |
|---|------|--------|
| 1 | **Motor de contenido** `ReceiptContentBuilder` (Application, puro): SaleDto → texto 42 chars. Compartido por consola/ESC/POS/PDF | ✅ HECHO (11-ago, commit) |
| 2 | **Encoder ESC/POS** (puro): texto → bytes (init, centrado, negrita, corte, CP437/850 para ñ/acentos) | ⏳ |
| 3 | **Envío a impresora** P/Invoke winspool.drv (raw por nombre de impresora, error claro si no existe) | ⏳ |
| 4 | **PDF** (QuestPDF): mismo contenido, archivo guardable/imprimible | ⏳ |
| 5 | **Conectar al cobro**: ConfirmPaymentAsync imprime + modal [Imprimir] [PDF] [Nueva venta] | ⏳ |
| 6 | **Ajustes**: selector de impresora + auto-imprimir (con pantalla de Ajustes de Fase 1) | ⏳ |

**Paso 1 — detalles implementados (11-ago-2026):**
- `POS.Application/Receipts/ReceiptContentBuilder.cs` — función pura `Build(SaleDto)`.
  Formato conservado del console (42 chars) con mejoras: descuento por línea
  (`Desc.: -X.XX` solo si > 0), descuento global en negativo, nombres de pago en
  español (Efectivo/Tarjeta/Transferencia), cantidades fraccionarias limpias
  (2 → "2", 0.5 → "0.5"), montos alineados a columna fija (campo 10).
- `ConsoleReceiptPrinter` delega en el builder (un solo cerebro de layout).
- Tests: `ReceiptContentBuilderTests` — golden completo + descuentos + métodos de
  pago + fraccionario + ancho 42. **26/26 tests verdes.**
- Gotcha: al escribir golden tests con alineación, NO contar espacios a ojo —
  "50.00"/"36.00" tienen 5 chars (no 6); verificar con lengths o char codes.

---

## 7. Accesibilidad y calidad (checklist pre-entrega)

- [x] Contraste texto ≥4.5:1 (TextSecondary sobre blanco ✓)
- [x] Focus visible en TODO interactivo (ring 2px `PrimaryBrush`)
- [x] Orden de tabulación = orden visual (izq → der, arriba → abajo)
- [x] Sin emojis como iconos; iconos vectoriales consistentes
- [x] Cursor pointer en todo lo clickable (`Cursor="Hand"`)
- [x] Hover/press con transición 150–300ms; sin cambios de layout al presionar
- [x] `AutomationProperties.Name` en iconos y controles sin texto
- [x] Animaciones respetan `SystemParameters.ClientAreaAnimation`
- [x] Estados disabled claros (opacidad 0.4 + semántica nativa)
- [x] Montos siempre `decimal` → `N2`, tabular numbers
- [x] Async con `AsyncRelayCommand` (nunca `async void` en code-behind)
- [x] Conversión de presentación con `IValueConverter`, no propiedades
      Visibility en el ViewModel (guía stack WPF de la skill)
- [x] Perf: virtualizar listas largas (`VirtualizingStackPanel`)

---

## 8. Anti-patrones a evitar (de MASTER + específicos POS)

- ❌ Sombras decorativas en superficies de contenido (solo popups/modales)
- ❌ Hover con elevación/transform (layout shift)
- ❌ Más de 2 acentos en pantalla (Primary + Accent)
- ❌ Texto denso sin jerarquía (el TOTAL y COBRAR mandan)
- ❌ Animaciones llamativas (motion 1/10)
- ❌ Emojis como iconos de sistema
- ❌ **Líneas vacías fantasma** (regla 3.2: una sola, se descarta sola)
- ❌ **Catálogo siempre visible** (el modelo B es a demanda)
- ❌ Inputs que pierden el foco tras cada escaneo (rompe el loop del cajero)
- ❌ Ocultar métodos de pago en menús (deben estar a un click)
- ❌ Botones que cambian de tamaño al hacer hover (layout shift)

---

## 9. Estado de implementación (11-ago-2026)

Rediseño ticket-centered (modelo B) ejecutado **por pasos** (verificación rápida en cada uno):

| # | Paso | Estado |
|---|------|--------|
| 1 | **Layout ticket-centered**: ticket (izq, 60%) + cobro (der, 40%); catálogo a demanda en popup (F2) que agrega sin cerrar; input de código/nombre al pie del ticket | ✅ HECHO (commit 11-ago) |
| 2 | **Línea de entrada con reglas del modelo B**: match único por código → se rellena sola; no-código → dropdown de sugerencias por nombre (debounce 250ms, precio+stock); UNA línea pendiente a la vez; COBRAR bloqueado con línea pendiente (borde warning); escáner rellena + foco avanza a nueva línea | ✅ HECHO (commit 11-ago) |
| 3 | **Pulido**: contador "Agregados: N" en el popup, atajos finos, estados vacíos | ⏳ |

Detalles del paso 1 (11-ago):
- SaleView.xaml reestructurado: 60*/Auto/40* (ticket | separador | cobro). El panel de cobro
  (totales, métodos, COBRAR) se movió tal cual a la columna derecha; el carrito vive a la izquierda.
- Input de búsqueda al pie del ticket con placeholder "Escribe código o nombre… (Enter agrega)".
- Popup catálogo (IsCatalogOpen): reutiliza Products/CategoryFilters/SearchText, buscador propio
  con foco (evento CatalogFocusRequested), agrega **sin cerrar** (modo 3), cierra con Esc/X/Cerrar.
- AutomationProperties.Name ASCII en botones clave (AbrirCatalogo/CerrarCatalogo) — nombres UIA
  estables (a11y + verificable por script sin problemas de encoding).
- CancelOverlay: Esc cierra catálogo primero, si no el modal de cobro.
- Verificado: build 0/0, 26/26 tests, capturas DPI-aware (popup abre → agrega sin cerrar → ticket
  conserva línea al cerrar). Nota: Teams al frente estorbaba las capturas; traer ventana al frente
  con SetForegroundWindow antes de capturar.

Detalles del paso 2 (11-ago):
- `SearchText` es SOLO la línea de entrada; el buscador del popup catálogo pasó a `CatalogSearchText`
  (estados independientes; el popup ya no reacciona a lo que escribe el cajero en el ticket).
- Auto-add por código exacto (SKU/barcode, case-insensitive): al coincidir UN producto se agrega
  solo y el foco vuelve a la línea (loop de escáner). Si ya está en el ticket → incrementa cantidad
  (decisión cerrada: cantidad repetida = incrementar).
- Dropdown de sugerencias por nombre: debounce 250ms en hilo UI (async/await captura el
  SynchronizationContext — el Task.Run original rompía colecciones del UI), máx 8 items,
  `nombre - precio - stock bajo`, selección con ↑/↓ + Enter o click, Esc cierra sin seleccionar.
- Regla 3.2: `HasPendingEntry=true` con texto sin resolver → COBRAR deshabilitado
  (CanExecute real en CobrarCommand — antes CanCobrar() era código muerto sin CanExecute)
  + borde warning + hint "Línea sin completar".
- **Lección de foco (bug peludo):** el Popup de WPF es un HWND separado que roba el foco del
  teclado — el Enter jamás llegaba a la línea de entrada (el KeyBinding de Enter tampoco funciona:
  el TextBox se lo traga con AcceptsReturn=false). Fix: dropdown EN FLUJO dentro de la columna
  ticket (fila propia entre líneas e input) + `PreviewKeyDown` en code-behind para el Enter.
  El Popup de catálogo (F2) se quedó como overlay Grid dentro de la ventana (sin HWND propio).
- Verificado end-to-end con UIA + log de debug: JGO-001 → línea agregada; "ju" → dropdown;
  Enter → incrementa Jugo a 2×; "zzz" → COBRAR IsEnabled=False + borde warning visible.
  Dato de la DB demo: "Café con leche" (CAF-001) está INACTIVO (IsActive=0) — por eso no
  aparecía en las búsquedas; la DB real vive en %LOCALAPPDATA%\Uenta\pos.db.
