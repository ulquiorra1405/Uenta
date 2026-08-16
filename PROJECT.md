# PROJECT — POS (Punto de Venta)

> Estado: **Fase 0.5 (cimientos técnicos) cerrada — Fase 1A cerrada (15-ago: recibo real P1.1–P1.3)** — Fase 1B cerrada (15-ago: login/roles/permisos + caja P2.1/P2.2)** — Fase 1C cerrada (catálogo + inventario P3.1/P3.1b/P3.2); ver `PLAN.md` para la ejecución detallada
> Última actualización: 15-ago-2026 · build 0/0, 79/79 tests verdes

---

## 1. Visión

Sistema de punto de venta (POS) para **un negocio**, de mostrador, que corre en **Windows (desktop)** y funciona **sin internet** (offline-first). Arquitectura en capas para que, cuando Bryan quiera, el mismo negocio pueda escalar a **más sucursales, app móvil o producto comercial** **sin reescribir la lógica de negocio**.

---

## 2. Decisiones fundacionales (registradas)

| # | Decisión | Detalle |
|---|----------|---------|
| D1 | **Un negocio / una sucursal** en v1 | El modelo de datos no impide multi-sucursal después; pero v1 no la modela explícitamente |
| D2 | **Desktop WPF primero** | El cajero usa una PC de mostrador con Windows |
| D3 | **Offline-first** | La venta se graba local (SQLite) y sincroniza después; el POS nunca depende de internet para vender |
| D4 | **Lógica en librerías, no en la UI** | La regla de negocio vive en `POS.Application`/`POS.Domain`; WPF solo presenta |
| D5 | **Móvil futuro = API REST, no reescritura** | Cuando llegue el móvil se agrega `POS.Api` (ASP.NET Core) que expone la MISMA lógica |
| D6 | **Stack .NET 9** | Consistente con el resto del ecosistema de Bryan (QuickNotes, USMToolkit) |
| D7 | **Sin over-engineering en v1** | Nada de MediatR/CQRS/event sourcing; máximo 4 proyectos; el desktop consume la librería directo (no vía HTTP) |
| D8 | **e-CF (DGII) se difiere pero se deja espacio** | El modelo distingue `Sale` de `Invoice` y reserva campos NCF/RNC desde el día uno |
| D9 | **Contexto fiscal RD** | ITBIS 18%, NCF de la DGII cuando llegue la facturación formal |
| D10 | **Nombre de trabajo: "Uenta"** | Puede ser temporal; se decide el definitivo antes de publicar |

---

## 3. Alcance v1 (MVP)

### ✅ Incluido en v1

**Ventas / POS (el corazón)**
- Carrito rápido: agregar por código de barras, SKU o nombre
- Cantidades, descuento por línea y descuento global
- Subtotal, ITBIS desglosado, total
- Métodos de pago: efectivo, tarjeta, transferencia, mixto
- Cálculo de vuelto
- Venta anónima o asociada a cliente
- Recibo: impresión térmica 80mm + PDF
- Apertura/cierre de caja: efectivo inicial, retiros, conteo final, diferencia

**Productos / Catálogo**
- CRUD de productos, activo/inactivo
- Categorías
- Código de barras / SKU (único)
- Precio de venta (con ITBIS incluido) y costo
- Stock mínimo (alerta)
- Variantes simples: producto maestro (línea) + variantes hijas con SKU/precio/stock propios (v1); creación por "duplicar y editar". Atributos (ej. color) como etiquetas, no multiplican inventario (regla P9)

**Inventario**
- Stock en tiempo real por producto
- Movimientos: entrada, salida, ajuste — siempre con motivo y usuario
- Alerta de stock bajo

**Clientes (CRM básico)**
- Registro: nombre, teléfono, RNC/cedula (opcional), correo (opcional)
- Historial de compras del cliente

**Facturación (mínima)**
- Recibo de venta (térmico + PDF) con numeración local
- Factura con NCF: **NO en v1** (fase 2 con e-CF), pero el modelo ya distingue recibo/factura

**Usuarios y roles**
- Login local (usuario + contraseña)
- Roles: `Admin`, `Supervisor`, `Cajero`
- Permisos por rol (ej.: solo Admin ve costos y cierra caja)
- Auditoría básica: quién hizo qué y cuándo (ventas, ajustes de stock, cambios de precio)

**Reportes / Dashboard**
- Ventas del día (total, # tickets, ticket promedio)
- Ventas por periodo (día/semana/mes), por producto, por cajero
- Productos más vendidos

**Hardware**
- Impresora térmica ESC/POS (80mm, USB/red)
- Lector de código de barras (emula teclado — sin librería)
- Cajón de dinero (pulso vía impresora)
- Datáfono (pago con tarjeta: se registra el monto manualmente)

**Operación**
- Backup/restore de la base SQLite (exportar archivo, importar archivo)

### ❌ Fuera de v1 (fases siguientes)

| Tema | Fase | Por qué se difiere |
|------|------|--------------------|
| Devoluciones / notas de crédito | **Fase 2 (temprana)** | Crítico en la práctica, pero se puede operar 1–2 meses sin él |
| Compras y proveedores (orden → recepción → costo) | Fase 2 | El inventario v1 entra por ajustes manuales |
| e-CF / facturación electrónica DGII | Fase 2 | Obligatorio progresivo; se deja espacio en el modelo |
| Fidelización / puntos | Fase 3 | No aporta al arranque |
| Multi-sucursal / multi-tenant | Fase 3 | Solo cuando haya 2+ puntos de venta |
| App móvil / POS.Api | Fase 3 | Solo cuando haya un segundo dispositivo |
| Sync offline (multi-dispositivo) | Fase 3 | Ligado a la API |
| Reportes avanzados (margen real, horas pico, tendencias) | Fase 3 | Se construyen sobre datos ya guardados |

---

## 4. Arquitectura

```
┌─────────────────────────────────────────────────────┐
│  POS.Desktop (WPF)                                  │
│  ViewModels + XAML. Code-behind DELGADO.            │
│  No conoce Domain ni Infrastructure.                │
└────────────────────────┬────────────────────────────┘
                         │ consume (directo, mismo proceso)
┌────────────────────────▼────────────────────────────┐
│  POS.Application                                    │
│  Casos de uso (CrearVenta, AbrirCaja...), DTOs,     │
│  Result<T>, puertos (ISaleRepository,               │
│  IReceiptPrinter, IBarcodeScanner, IClock)          │
└───────────────┬─────────────────────┬───────────────┘
                │                     │
┌───────────────▼──────────┐  ┌───────▼───────────────┐
│  POS.Domain              │  │  POS.Infrastructure   │
│  Entidades + reglas      │  │  EF Core + SQLite,    │
│  puras. CERO deps.       │  │  impresora térmica,   │
└──────────────────────────┘  │  scanner, PDF         │
                              └───────────────────────┘

FUTURO (sin tocar lo de arriba):
┌───────────────────────────────┐
│  POS.Api (ASP.NET Core)       │
│  Expone la MISMA lógica:      │
│  POST /api/sales, etc.        │
└───────────────────────────────┘
```

### Reglas de dependencia (unidireccionales)

```
Desktop → Application → Domain
Infrastructure → Domain  (+ Application cuando necesite sus interfaces de puerto)
Desktop NUNCA referencia Domain ni Infrastructure directamente
```

### Reglas duras de arquitectura

1. **Nada de WPF en las capas internas.** La impresora, el scanner, el reloj, etc. se declaran como interfaces en `Application` (`IReceiptPrinter`, `IBarcodeScanner`, `IClock`) y se implementan en `Infrastructure`. WPF solo inyecta.
2. **`Result<T>` para errores de negocio**, no excepciones. La UI decide cómo mostrarlo; la API futura lo serializa como HTTP 400 con código de error.
3. **DTOs en la frontera.** La UI nunca recibe entidades de dominio directo; `Application` devuelve DTOs.
4. **Repositorios agnósticos del almacenamiento.** `ISaleRepository`, `IProductRepository`... la implementación EF Core puede cambiarse sin tocar `Application`.
5. **Sin excepciones para flujo de negocio esperado** (stock insuficiente, caja cerrada, producto inactivo → `Result.Failure`).

---

## 5. Stack de desarrollo

| Capa | Tecnología |
|------|-----------|
| Runtime | **.NET 9** (SDK 9.0.316 instalado en SO03) |
| UI | **WPF** (`net9.0-windows`) |
| MVVM | **CommunityToolkit.Mvvm** (ObservableObject, RelayCommand, Source Generators) |
| ORM | **EF Core 9.0.8 + SQLite** (Microsoft.EntityFrameworkCore.Sqlite) |
| DI | **Microsoft.Extensions.DependencyInjection** (9.0.8) |
| Tests | **xUnit** (+ STA helper para pruebas WPF, patrón ya usado en USMToolkit) |
| Impresora térmica | **ESC/POS** vía P/Invoke a winspool.drv (RawPrinterHelper adaptado) — sin dependencia pesada |
| PDF | **QuestPDF** (licencia Community, simple) o exportación a archivo de texto/HTML imprimible — decidir en Fase 1 |
| Control de versiones | **Git** (repositorio local; GitHub si Bryan quiere respaldo remoto) |
| IDE | El que Bryan use: Visual Studio 2022 o VS Code + C# Dev Kit |

### Por qué estas elecciones
- **CommunityToolkit.Mvvm**: ya lo usa Bryan en QuickNotes/USMToolkit → curva cero, menos boilerplate.
- **EF Core + SQLite**: mismo stack de QuickNotes → Bryan ya lo domina; SQLite = offline-first gratis, un solo archivo para backup.
- **xUnit**: ya es su suite en USMToolkit (84 tests).
- **ESC/POS manual (P/Invoke)**: las librerías de impresión térmica NuGet son pesadas o viejas; el protocolo básico es simple (texto + cortar papel + abrir cajón). Se controla el 100%.

---

## 6. Modelo de datos v1 (alto nivel)

> Nota: el stock puede ser negativo en v1 (decisión P3); el modelo no lo impide. El manejo formal de stock/almacenes (multi-almacén, stock mínimo por ubicación) se diseña en la fase correspondiente.

```
Product ──┬── Category
          ├── Variant (producto hijo con SKU/precio/stock; opcional v1 — regla P9: solo si cambia precio o stock)
          └── StockMovement (entrada/salida/ajuste, motivo, usuario, fecha)

Customer (nombre, teléfono, RNC/cedula?, correo?)

Sale ──┬── SaleItem (producto, cantidad, precio, descuento)
       ├── Payment (efectivo/tarjeta/transferencia, monto)  ← permite pago mixto
       ├── CashSession (caja abierta a la que pertenece)
       └── User (cajero)

CashSession (apertura, efectivo inicial, retiros, cierre, conteo, diferencia)

User ── Role (Admin/Supervisor/Cajero) + permisos

AuditLog (usuario, acción, fecha, detalle)

Setting (clave/valor: nombre negocio, RNC, dirección, pie de recibo, etc.)
```

### Espacio reservado para e-CF (Fase 2, sin modelar ahora)
- `Sale` queda como la venta que emite **Recibo** (comprobante interno).
- En Fase 2, `Invoice` (factura) se modela **junto** a `Sale` o como tipo derivado: número NCF, rango de comprobante, RNC del cliente, estado de envío a la DGII.
- No bloquea nada de v1; solo hay que **no** meter campos de NCF a lo loco en `Sale` hoy.

### Reglas de datos
- **Dinero siempre `decimal`**, nunca `double`/`float`.
- Fechas/hora: `DateTimeOffset` local; `IClock` inyectado (testeable).
- IDs: `long` (SQLite autoincrement) — suficiente para un negocio; GUIDs solo si el sync lo exige en Fase 3.
- Montos: 2 decimales (RD$), ITBIS 18% incluido en el precio de venta (decisión a confirmar, sección 8).

---

## 7. Reglas de negocio clave (v1)

1. **ITBIS 18%**: el precio de venta del producto **incluye** ITBIS (práctica retail RD). El recibo muestra el desglose (subtotal sin ITBIS, ITBIS, total). *Pendiente confirmar (sección 8).*
2. **Stock**: inicialmente se **permite vender sin stock** (el stock puede ir a negativo — temporal). El carrito muestra la disponibilidad como aviso, no bloquea. El manejo de stock/almacenes se definirá más adelante.
3. **Caja**: solo se puede cobrar con una caja **abierta** para el usuario; el cierre de caja exige conteo y reporta diferencia.
4. **Descuentos**: por línea y global; tope por rol (ej.: Cajero máx. 10%, Supervisor 25%, Admin sin tope) — configuración, no hardcode.
5. **Venta anónima**: permitida por defecto (cajero puede vender sin registrar cliente).
6. **Auditoría**: toda venta, ajuste de stock y cambio de precio queda en `AuditLog` con usuario y fecha.

---

## 8. Decisiones tomadas (05-ago-2026, actualizado 06-ago-2026)

| # | Pregunta | Decisión |
|---|----------|----------|
| P1 | Nombre del proyecto | **"Uenta"** (puede ser temporal; definitivo antes de publicar) |
| P2 | ¿Precios con ITBIS incluido? | **Sí** — retail RD; el recibo muestra el desglose |
| P3 | ¿Vender sin stock (negativo)? | **Sí, inicialmente** — se permite, el carrito avisa pero no bloquea; manejo de stock/almacenes se define después |
| P4 | ¿Crédito a clientes (fiado)? | **No** — v1 solo contado (efectivo/tarjeta/transferencia) |
| P5 | ¿Variantes (talla/color)? | **Sí** — simple: variante = producto hijo con su propio stock; creación por "duplicar y editar"; ver regla completa en P9 |
| P6 | ¿Repositorio git en GitHub? | **Sí** — privado (respaldo + historial); se crea al iniciar git local |
| P7 | IDE de desarrollo | **Visual Studio 2022 Community** (recomendación de Theo, ver §8.1) |
| P8 | ¿Descuentos con tope por rol? | **Sí** — configurable por rol |
| P9 | ¿Cómo abarcar dimensiones de producto (specs, estado, color)? | **Regla: una dimensión es variante SOLO si cambia precio o necesita stock propio.** En la práctica: specs (i5/i7, RAM…) y estado (nuevo/usado/reparado) = variantes; color = atributo (etiqueta en la ficha), no multiplica inventario. Sin motor de combinaciones en v1 (genera SKUs fantasma y complica al cajero); el SKU codifica la configuración (ej. `IP3-NEG-16-512-NUE`) para que el cajero escanee y venda. Matriz de dimensiones configurables → Fase 2+, solo si un cliente real lo pide |

### 8.1 Por qué Visual Studio 2022 para este proyecto

- **Diseñador XAML visual**: para WPF es la diferencia principal. VS Code no tiene diseñador XAML nativo (solo previsualización limitada con extensiones); editar vistas WPF complejas a ciegas es lento y propenso a errores.
- **Debugging de bindings**: el output window de WPF + herramientas integradas (Live Visual Tree, Live Property Explorer) para diagnosticar bindings rotos.
- **EF Core integrado**: consola de paquetes (Add-Migration, Update-Database) y herramientas de SQLite sin salir del IDE.
- **xUnit runner integrado**: Test Explorer con filtros, debug de tests con un clic.
- **Gratis**: Community es gratis para uso individual/pequeño.

VS Code + C# Dev Kit sigue siendo buena opción para tareas ligeras (editar, revisar, git), pero el desarrollo principal de Uenta se hace en Visual Studio.

---

## 9. Roadmap

> El documento maestro de ejecución es **`PLAN.md`** (merge del roadmap, del plan del
> recibo de `venta.md` §6.1 y de la deuda técnica de la revisión de 13-ago). Esta tabla
> es el resumen de alto nivel; los pasos concretos y su verificación viven ahí.

| Fase | Contenido | Estado |
|------|-----------|--------|
| **Fase 0** | Esqueleto + ejemplo punta a punta (vender producto con stock → SQLite → Result<T>) + rediseño ticket-centered + motor de recibo | ✅ cerrada |
| **Fase 0.5** | Cimientos técnicos: pricing único, split de SaleView, async void, secuencia de numeración, estilos | ✅ cerrada |
| **Fase 1A** | Recibo real: impresora ESC/POS + conectar al cobro + Ajustes | ✅ **cerrada 15-ago** (P1.1 térmica real, P1.2 modal Imprimir/PDF, P1.3 Ajustes) |
| **Fase 1B** | Usuarios/roles + login, caja (apertura/cierre), auditoría | ✅ **cerrada 15-ago** (P2.1 login/roles/permisos/auditoría + P2.2 caja completa; 106/106 tests) |
| **Fase 1C** | Catálogo completo (variantes P9) + movimientos de inventario | ✅ **cerrada 15-ago** (P3.1 + P3.1b + P3.2) |
| **Fase 1D** | Clientes + historial, reportes/dashboard, backup/restore | ✅ **cerrada 15-ago** (P4.1 clientes + P4.2 reportes + P4.3 backup; 130/130 tests) |
| **Fase 2** | Devoluciones/notas de crédito + compras/proveedores | 🔄 en curso — **P5.1 devoluciones ✅ implementado y verificado (16-ago: 140/140 tests + smoke UIA end-to-end)**; P5.2 compras pendiente |
| **Fase 3** | e-CF (DGII) — facturación electrónica con NCF | ⏳ *deliberadamente al final (lo más delicado): depende de requisitos DGII; requiere core de venta estable y decisiones de negocio sobre ventas pre-e-CF* |
| **Fase 4** | Fidelización, reportes avanzados, multi-sucursal, POS.Api + app móvil | ⏳ |

---

## 10. Documentos relacionados

- `PLAN.md` — **plan maestro de ejecución** (merge de roadmap, recibo y deuda técnica)
- `memory/projects/pos.md` — memoria operativa del proyecto (estado, lecciones, contexto)
- `POS.sln` — solución con los 4 proyectos ya creados
