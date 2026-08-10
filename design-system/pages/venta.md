# Pantalla de Venta (POS) — Page Override

> Este archivo **sobrescribe** a `MASTER.md` para la pantalla de venta del POS.
> Regla de la skill: si `pages/venta.md` existe, sus reglas mandan sobre el Master.
> Aplicable a: `POS.Desktop` (WPF, .NET 9) — pantalla principal del cajero.

---

## 0. Qué es esta pantalla

Núcleo operativo del sistema: el cajero agrega productos (escáner o búsqueda),
ve el carrito con totales, aplica descuentos y cobra. Velocidad y cero fricción
son el objetivo: cada venta debe tomar segundos, no minutos.

**No aplican del MASTER:** el patrón de landing page, el motion GSAP (scroll
reveal) y los breakpoints responsive web. Esta es una app desktop de alta
densidad con ventana única maximizada. Se conservan: layout, flujos, atajos.
**Línea estética (10-ago-2026):** Minimalismo Funcional (Swiss) — ver
`estetica-minimalista.md` y `uenta-pos/MASTER.md`. Cambió la paleta de neutros,
se eliminaron las sombras decorativas y el sidebar ahora usa barra indicadora.

---

## 1. Layout (wireframe)

Ventana única, mínimo 1280×800, maximizada por defecto. Tres franjas
verticales: header (48px), cuerpo (flex), sin footer global.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ ● Uenta POS        Caja #12 · abierta        Cajero: Juan       12:45  ☰  │
├───────────────────────────────────────────────┬──────────────────────────────┤
│  BUSCAR O ESCANEAR  [____________________] ⌕  │  VENTA #1024    Cliente:    │
│  Enter=agregar · F2=búsqueda                   │  [Anónimo ▾]  [+ Cliente]   │
│                                               │                              │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐       │  ┌────────────────────────┐ │
│  │ iPhone15 │ │ Teclado  │ │ Mouse    │       │  │ 1× iPhone 15           │ │
│  │ RD$45,000│ │ RD$2,500 │ │ RD$1,200 │       │  │    45,000    [−][+][%]✕│ │
│  │   [+Agr] │ │   [+Agr] │ │   [+Agr] │       │  ├────────────────────────┤ │
│  └──────────┘ └──────────┘ └──────────┘       │  │ 2× Teclado GX          │ │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐       │  │    5,000     [−][+][%]✕│ │
│  │ Monitor  │ │ Cable    │ │ Audífono │       │  └────────────────────────┘ │
│  │ RD$8,500 │ │ RD$400   │ │ RD$1,800 │       │  ...                       │
│  └──────────┘ └──────────┘ └──────────┘       │                             │
│                                               │  Subtotal         50,500.00 │
│  [TODOS] [Celulares] [Cómputo] [Audio]        │  ITBIS 18%         7,703.39 │
│  [Accesorios] [Electro] [+ categorías ▸]      │  Descuento            -0.00 │
│                                               │  ────────────────────────   │
│  Resultados: 24 · [Búsqueda avanzada]         │  TOTAL         RD$ 58,203.39│
│                                               │                             │
│                                               │  [% Descuento] [👤 Cliente] │
│                                               │  ────────────────────────   │
│                                               │  [EFECTIVO] [TARJETA]      │
│                                               │  [TRANSFERENCIA] [MIXTO]   │
│                                               │  ────────────────────────   │
│                                               │  [      COBRAR  F8      ]  │
└───────────────────────────────────────────────┴──────────────────────────────┘
```

### Distribución

| Zona | Ancho | Contenido |
|------|-------|-----------|
| Header | 100% (48px) | Marca, estado de caja, usuario, reloj, menú |
| Izquierda (catálogo) | ~58% | Buscador + grid de productos (cards) + categorías |
| Derecha (venta) | ~42% | Carrito, totales, métodos de pago, COBRAR |

### Jerarquía visual

1. **TOTAL** (derecha, tamaño display, peso bold) — lo que el cliente debe pagar.
2. **COBRAR** — botón primario más grande de la pantalla, color accent, F8.
3. **Buscador** (izquierda, arriba) — siempre enfocado al entrar la pantalla.
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
desde App.xaml (tema claro/oscuro futuro sin tocar XAML de pantallas).

### Tipografía

- **Números/montos:** `Rubik` (o fallback `Segoe UI Variable Display` si Rubik
  no está instalada — NO bloquear la app por una fuente). Tabs numéricas.
- **Texto general:** `Nunito Sans` (fallback `Segoe UI Variable`).
- Escala (px): 11 UI secundaria · 13 cuerpo · 15 énfasis · 18 subtítulo ·
  24 título de sección · **32 total** · 20 botón COBRAR.
- Montos SIEMPRE `N2` con miles (RD$ 58,203.39), monospaced/tabular para que
  no bailen al cambiar.

### Espaciado (densidad 6/10 — aire sin perder densidad de cajero)

`4 / 8 / 12 / 16 / 24 / 32` px. Padding estándar de cards: 12px. Gaps de
grid de productos: 10px. Margen interno del carrito: 16px.

### Sombras (mínimas — Minimalismo Funcional)

- Superficies de contenido (cards, carrito, botones): **SIN sombra** —
  jerarquía por hairlines `BorderBrush` y tipografía.
- **Única excepción: modales** (sombra suave + scrim `#99000000`).

### Radio de esquinas

- Botones, inputs y cards: **6px**. Modales: 12px. Chips de categoría: 999px (píldora).

---

## 3. Componentes

### 3.1 Buscador (izquierda, arriba)
- Input grande (48px), placeholder "Buscar o escanear…", icono lupa a la izquierda.
- **Enter agrega el producto al carrito** (código de barras = emulación de
  teclado; el foco NUNCA se pierde del input).
- Coincidencia por: código de barras exacto → SKU → nombre (LIKE). Si el
  escaneado no existe: aviso inline "Producto no encontrado" + sugerencias.
- Debounce 250ms para búsqueda por nombre; resultados en el grid.

### 3.2 Grid de productos
- Cards compactas (icono/color de categoría, nombre 2 líneas máx., precio,
  botón [+ AGREGAR]).
- Click en card o Enter = agregar 1 unidad. Click en [+ AGREGAR] también.
- **Stock bajo (< mínimo):** badge ámbar "Quedan N". **Stock 0 o negativo:**
  badge ámbar "Sin stock — avisar" (P3: se vende igual, avisa, no bloquea).
- Producto inactivo: nunca aparece en grid ni en búsqueda.

### 3.3 Categorías (chips)
- Fila de píldoras bajo el buscador. "TODOS" por defecto. Scroll horizontal si
  sobran. Selección con foco visible (ring 2px primary).

### 3.4 Carrito (derecha)
- Lista de líneas: `cant × nombre — precio línea — [−][+][%] ✕`.
- Descuento por línea: botón `%` abre mini-input inline (valida tope por rol).
- Fila de línea: 48px mín. de alto (target táctil).
- Scroll interno; el footer de totales SIEMPRE visible (fijo).

### 3.5 Totales
- Subtotal, ITBIS 18% desglosado, Descuento, separador, **TOTAL** (32px, bold,
  color TextPrimary sobre fondo Surface).
- Aviso ámbar si algún ítem tiene stock insuficiente: "⚠ 2 productos sin stock
  suficiente — se venderán igual" (P3).

### 3.6 Botones de pago (4, siempre visibles)
- EFECTIVO · TARJETA · TRANSFERENCIA · MIXTO — botones de superficie (blanco +
  hairline), 44px+ alto. Hover: `MutedBrush` + borde `PrimaryBrush`.

### 3.7 COBRAR (botón primario global)
- 100% del ancho del panel derecho, 52px alto, fondo **AccentBrush** (`#EA580C`),
  texto blanco, bold, F8. Hover: `AccentDarkBrush`. Sin sombra, sin cambios de layout.

### 3.8 Header
- Izquierda: marca "Uenta POS" (logo + nombre).
- Centro: estado de caja — badge verde "Caja #12 · abierta" o gris/rojo
  "Caja cerrada" (si cerrada, COBRAR deshabilitado y banner de aviso).
- Derecha: usuario + rol, reloj (HH:mm), botón menú (☰ → navegación:
  Catálogo, Inventario, Clientes, Reportes, Caja, Config).

### 3.9 Iconos
- **Prohibido emojis como iconos** (regla de la skill). WPF: `Path`/`Geometry`
  vectoriales propios o `Segoe Fluent Icons` (viene con Windows 11). Un solo
  set, un solo peso de trazo. Iconos de acción (lupa, ✕, −, +, %) con target
  ≥32px visual y área de click ≥44px.

---

## 4. Flujos de interacción

### 4.1 Agregar producto (3 vías)
1. **Escáner:** foco en buscador → escanea → Enter automático → línea al carrito.
2. **Búsqueda:** escribe → Enter en resultado (o click) → al carrito.
3. **Grid:** click en card o [+ AGREGAR].

Todas las vías dejan el foco de vuelta en el buscador (loop de escaneo continuo).

### 4.2 Cobrar (F8)
1. Modal de pago (centrado, 480px): método preseleccionado según último usado.
2. **Efectivo:** campo "Monto recibido" autofocus → muestra **VUELTO** en vivo
   (verde, grande). Botón "Exacto" (F9) llena el monto del total.
3. **Tarjeta/Transferencia:** campo monto (datáfono manual — se registra el
   monto, P. hardware).
4. **Mixto:** dos campos (efectivo + resto) con barra de progreso de cobertura.
5. Confirmar → `SaleService.CrearVenta` (async, `AsyncRelayCommand` con
   `IsRunning` — botón deshabilitado mientras procesa, spinner).
6. Éxito → modal de recibo: [Imprimir] [PDF] [Nueva venta (Enter)]. Vuelto en
   pantalla hasta cerrar el modal.
7. Error de negocio (`Result.Failure`): mensaje inline en el modal, sin
   excepción, sin crash. Stock insuficiente = warning, NO error (P3).

### 4.3 Descuentos
- Por línea: `%` en la fila. Global: botón "% Descuento" sobre los totales.
- Modal con input numérico; valida tope por rol (Cajero ≤10%, Supervisor ≤25%,
  Admin ∞ — configurable, no hardcode). Si excede: error inline + tope visible.

### 4.4 Caja cerrada
- Banner rojo suave en header + COBRAR disabled + hint "Abra la caja para
  cobrar". Link directo a Apertura de caja.

---

## 5. Atajos de teclado (cajero = velocidad)

| Tecla | Acción |
|-------|--------|
| `Enter` | Agregar producto escaneado/buscado; confirmar modal |
| `F2` | Foco al buscador (siempre) |
| `F4` | Descuento global |
| `F8` | COBRAR |
| `F9` | Pago con monto exacto |
| `+` / `-` | Subir/bajar cantidad de la línea seleccionada |
| `Supr` | Quitar línea seleccionada |
| `Esc` | Cerrar modal / limpiar búsqueda |
| `F1` | Ayuda de atajos |

Todo accionable por teclado (regla UX de la skill: keyboard navigation, High).

---

## 6. Accesibilidad y calidad (checklist pre-entrega)

- [x] Contraste texto ≥4.5:1 (TextSecondary `#475569` sobre blanco = 7.6:1 ✓)
- [x] Focus visible en TODO interactivo (ring 2px `PrimaryBrush`, no `outline-none`)
- [x] Orden de tabulación = orden visual (izq → der, arriba → abajo)
- [x] Sin emojis como iconos; iconos vectoriales consistentes
- [x] Cursor pointer en todo lo clickable (`Cursor="Hand"`)
- [x] Hover/press con transición 150–300ms; sin cambios de layout al presionar
- [x] `AutomationProperties.Name` en iconos y controles sin texto
- [x] Animaciones respetan `SystemParameters.ClientAreaAnimation` (reduced motion)
- [x] Estados disabled claros (opacidad 0.4 + sin acción, semántica nativa)
- [x] Montos siempre `decimal` → `N2` (nunca double), tabular numbers
- [x] Async con `AsyncRelayCommand` (nunca `async void` en code-behind)
- [x] Conversión de presentación con `IValueConverter` (bool→visibility, etc.),
      no propiedades de Visibility en el ViewModel (guía stack WPF de la skill)
- [x] Perf: virtualizar el grid de productos (`VirtualizingStackPanel`) y
      medir con el Performance Profiler antes de optimizar (guía stack WPF)

## 7. Anti-patrones a evitar (de MASTER + específicos POS)

- ❌ Sombras decorativas en superficies de contenido (solo modales)
- ❌ Hover con elevación/transform (layout shift)
- ❌ Más de 2 acentos en pantalla (Primary + Accent)
- ❌ Texto denso sin jerarquía (el TOTAL y COBRAR mandan)
- ❌ Animaciones llamativas (motion 1/10: solo micro-transiciones de hover/foco)
- ❌ Emojis como iconos de sistema
- ❌ Inputs que pierden el foco tras cada escaneo (rompe el flujo del cajero)
- ❌ Ocultar métodos de pago en menús (deben estar a un click)
- ❌ Botones que cambian de tamaño al hacer hover (layout shift)
