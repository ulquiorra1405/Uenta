# Uenta — Punto de Venta (POS)

Sistema de punto de venta para Windows, de mostrador, **offline-first** (funciona sin internet). Escrito en C# / .NET 9 con WPF y SQLite.

> **Estado:** Fase 0.5 (cimientos técnicos) cerrada — **Fases 1A (recibo real), 1B (usuarios + caja) y 1C (catálogo) cerradas**. Build 0 errores/0 warnings · 106/106 tests verdes.

## Características

**Ya implementado**
- Pantalla de venta moderna (modelo *ticket-centered*): carrito por código de barras, SKU o nombre
- Descuentos por línea (% o RD$) y descuento global
- Subtotal, ITBIS 18% desglosado (precio de venta con ITBIS incluido), total, vuelto
- Cobro con métodos de pago (efectivo, tarjeta, transferencia, mixto) en modal
- Catálogo de productos: CRUD, categorías, activo/inactivo, búsqueda
- Editor de producto y gestor de categorías en overlays modales
- Numeración consecutiva de ventas sin huecos (secuencia atómica)
- Motor de recibo: contenido (`ReceiptContentBuilder`), impresión ESC/POS (`EscPosEncoder`) y PDF (`ReceiptPdfGenerator`)
- Sesión de caja (apertura/cierre) con cajero

**En roadmap (ver abajo)**
- Facturación formal con NCF / e-CF (DGII) — el modelo ya reserva el espacio
- Usuarios con roles y permisos, login local
- Inventario con movimientos y alerta de stock bajo
- Clientes (CRM básico) e historial de compras
- Reportes / dashboard de ventas
- Devoluciones, compras/proveedores, multi-sucursal y API móvil (fases futuras)

## Arquitectura

```
┌─────────────────────────────────────────────┐
│  POS.Desktop (WPF)                          │
│  ViewModels + XAML · code-behind delgado    │
│  No conoce Domain ni Infrastructure         │
└────────────────────┬────────────────────────┘
                     │ consume (directo, mismo proceso)
┌────────────────────▼────────────────────────┐
│  POS.Application                            │
│  Casos de uso, DTOs, Result<T>, puertos     │
│  (ISaleRepository, IReceiptPrinter, IClock) │
└───────────┬──────────────────┬──────────────┘
            │                  │
┌───────────▼───────┐  ┌───────▼───────────────┐
│  POS.Domain       │  │  POS.Infrastructure   │
│  Entidades y      │  │  EF Core + SQLite,    │
│  reglas puras     │  │  impresora térmica,   │
│  CERO dependencias│  │  scanner, PDF         │
└───────────────────┘  └───────────────────────┘
```

**Reglas de dependencia (unidireccionales):** `Desktop → Application → Domain` · `Infrastructure → Domain` (+ Application para sus interfaces). Desktop nunca referencia Domain ni Infrastructure directamente.

## Stack

| Capa | Tecnología |
|------|-----------|
| Runtime | .NET 9 |
| UI | WPF (`net9.0-windows`) |
| MVVM | CommunityToolkit.Mvvm |
| ORM | EF Core + SQLite |
| DI | Microsoft.Extensions.DependencyInjection |
| Tests | xUnit |
| Impresión térmica | ESC/POS vía P/Invoke a winspool.drv |

## Cómo compilar y ejecutar

Requisito: **.NET 9 SDK** y Windows 10/11.

```bash
# Restaurar y compilar
dotnet build POS.sln

# Ejecutar la app
dotnet run --project POS.Desktop

# Tests
dotnet test POS.sln
```

> La base de datos SQLite se crea y migra automáticamente al primer arranque. Usuarios demo: `admin/admin123` (Administrador), `supervisor/super123`, `cajero/cajero123`. Al primer login hay que abrir la caja (header → botón de caja) para poder cobrar.

## Estructura

```
POS.sln
├── POS.Application/     # Casos de uso, DTOs, Result<T>, puertos, CartCalculator
├── POS.Domain/          # Entidades y reglas de negocio puras
├── POS.Infrastructure/  # EF Core + SQLite, repositorios, migraciones, impresora
├── POS.Desktop/         # App WPF: ViewModels, Views, Behaviors, Converters
└── POS.Tests/           # Suite xUnit (106 tests)
```

## Roadmap

| Fase | Contenido | Estado |
|------|-----------|--------|
| Fase 0 | Esqueleto 4 capas + ejemplo punta a punta + motor de recibo | Cerrada |
| Fase 0.5 | Cimientos técnicos: pricing único, secuencia atómica, estilos | Cerrada |
| Fase 1A | Recibo real: impresora ESC/POS + ajustes | ✅ Cerrada (15-ago) |
| Fase 1B | Usuarios/roles + login, caja, auditoría | ✅ Cerrada (15-ago) |
| Fase 1C | Catálogo completo + inventario | ✅ Cerrada (15-ago) |
| Fase 1D | Clientes, reportes, backup/restore | 🔄 en curso — P4.3 backup ✅ + P4.1 clientes ✅ + P4.2 reportes ✅ (15-ago) |
| Fase 2 | Devoluciones, compras/proveedores, e-CF (DGII) | Pendiente |
| Fase 3 | Fidelización, multi-sucursal, API móvil | Pendiente |

## Licencia

Sin licencia definida aún — todos los derechos reservados.
