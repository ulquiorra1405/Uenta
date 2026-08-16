# Design System Master File

> **LOGIC:** When building a specific page, first check `design-system/pages/[page-name].md`.
> If that file exists, its rules **override** this Master file.
> If not, strictly follow the rules below.

---

**Project:** Uenta POS
**Linea estetica:** Minimalismo Funcional (estilo Swiss) — aprobada por Bryan el 10-ago-2026.
**Generated:** 2026-08-07 (regenerado manualmente 10-ago-2026)
**Category:** Desktop POS / Kiosk (WPF, .NET 9)
**Design Dials:** Variance 3/10 (Centrado / Minimal) | Motion 1/10 (Subtle) | Density 6/10 (Standard)

> **Documento de intención:** `estetica-minimalista.md` (por qué esta línea, alternativas, decisiones).
> **Regla global (decisión de Bryan):** todo lo que se agregue al POS debe seguir esta línea.
> **Acentos configurables:** Primary y Accent son tokens intercambiables en runtime desde Ajustes (Fase 1+). Máximo 2 acentos.

---

## Global Rules

### Color Palette

| Role | Hex | Uso WPF |
|------|-----|---------|
| Primary | `#059669` | Acciones primarias, selección, foco, enlaces (token configurable) |
| On Primary | `#FFFFFF` | Texto sobre Primary |
| Primary Dark | `#047857` | Hover de primario / texto sobre Primary (variante de contraste) |
| Accent/CTA | `#EA580C` | **Solo** COBRAR y confirmaciones críticas (token configurable) |
| Accent Dark | `#C2410C` | Hover de COBRAR |
| Background | `#F8FAFC` | Fondo app (gris neutro — el fondo NO lleva tinte de color) |
| Surface | `#FFFFFF` | Cards, carrito, modales |
| Muted | `#F1F5F9` | Superficies secundarias, hover de filas/botones ghost |
| Border | `#E2E8F0` | Hairlines y separadores |
| Foreground | `#0F172A` | Texto principal |
| Text Secondary | `#64748B` | Texto secundario (4.76:1 sobre blanco ✓) |
| Destructive | `#DC2626` | Errores, eliminar |
| Warning | `#D97706` | Avisos no bloqueantes (stock bajo) |

**Regla dura:** nada de hex hardcodeado en las vistas — todo por `StaticResource`/`DynamicResource`
desde App.xaml. Los acentos viven en `DynamicResource` para poder cambiarlos desde Ajustes sin
reiniciar (ver `estetica-minimalista.md` §2).

### Typography

- **Heading/Números:** Rubik (fallback Segoe UI Variable Display)
- **Body:** Nunito Sans (fallback Segoe UI Variable)
- Jerarquía por peso/tamaño (400/600/700), no por color. Montos SIEMPRE `N2` tabular.
- Escala: 11 UI secundaria · 13 cuerpo · 15 énfasis · 18 subtítulo · 26 total · 32 modal cobro.

### Spacing Variables

*Density: 6/10 — Standard (aire sin perder densidad de cajero)*

| Token | Value | Usage |
|-------|-------|-------|
| `--space-xs` | `4px` | Gaps mínimos |
| `--space-sm` | `8px` | Icon gaps, inline spacing |
| `--space-md` | `12px` | Padding estándar de cards |
| `--space-lg` | `16px` | Margen de columnas/secciones |
| `--space-xl` | `24px` | Secciones grandes |
| `--space-2xl` | `32px` | Headers/espaciado hero |

### Shadows (mínimas)

- **Superficies de contenido: SIN sombra.** Jerarquía por hairlines (`BorderBrush`), contraste y tipografía.
- **Única excepción permitida: modales** (sombra suave + scrim `#99000000`).

### Radius

- Botones, inputs, cards: **6px**. Chips: 999px (píldora). Modales: 12px.

---

## Component Specs (WPF)

### Botones (estilos implícitos en App.xaml — regla de la skill: un Style por TargetType)

> **DECISIÓN ESTÉTICA (Bryan, 16-ago): en esta app NINGÚN botón usa relleno de color
> sólido.** Todos son outline / ghost / superficie con hairline. Esto fue una decisión
> consciente por el look minimalista de la app; no revivir "botón sólido naranja" aunque
> specs antiguos lo describan.

- **Primario (outline):** fondo blanco/superficie + borde `PrimaryBrush` + texto `PrimaryDarkBrush`.
  Hover = fondo `PrimaryLightBrush` + borde `PrimaryDarkBrush` (sin elevación ni translate).
  Press = opacity 0.7. Focus = borde 2px `PrimaryStrongBrush`.
- **Secundario (ghost):** sin borde, texto `PrimaryDarkBrush`. Hover = fondo `MutedBrush`. Press = `PrimaryLightBrush`. Focus = ring 2px `PrimaryBrush`. Para acciones secundarias (Buscar, Editar, Cancelar, Descuento…).
- **Pago (superficie):** blanco + hairline, hover = `MutedBrush` + borde `PrimaryBrush`.
- **Acento (COBRAR / confirmaciones críticas):** mismo patrón outline que el primario pero con
  borde `AccentBrush` + texto `AccentDarkBrush`. Hover = fondo `AccentLightBrush` + borde
  `AccentDarkBrush`. Focus = `AccentStrongBrush`. Es el ÚNICO elemento con acento naranja.
- **Icono compacto:** transparente, hover = `MutedBrush`.

### Cards de producto

- Superficie blanca + hairline `BorderBrush` (sin sombra). Hover: borde oscurece + fondo `#F8FAFC`. Seleccionado: borde/halo `PrimaryBrush`.

### Sidebar (navegación)

- Fondo `SurfaceBrush` + hairline derecho. Logo UENTA verde.
- Item activo: fondo `MutedBrush` + texto/icono `PrimaryDarkBrush` + **barra izquierda 3px `PrimaryBrush`** (indicador Swiss).
- Hover: `MutedBrush`. Press: `PrimaryLightBrush`. Focus: ring 1px.
- Colapsado (64px): solo iconos centrados, con `AutomationProperties.Name` para a11y.

### Inputs

- Fondo blanco + hairline. Focus = borde `PrimaryBrush` 2px (sin blur, sin sombra).

---

## Style Guidelines

**Style:** Minimalism & Swiss Style (WCAG AAA, enterprise/tools profesionales)

- Contenido primero: nada decorativo compite con los datos.
- Profundidad mínima: la jerarquía la hacen hairlines, contraste y tipografía.
- Disciplina de color: neutros dominan; 1–2 acentos máximo, cada uno con UN trabajo.
- Movimiento casi nulo: 150–200ms solo en hover/foco; sin elevaciones ni transforms.

---

## Anti-Patterns (Do NOT Use)

- ❌ Sombras decorativas en superficies de contenido (solo modales)
- ❌ Hover con elevación/transform (layout shift + ruido)
- ❌ Más de 2 acentos en pantalla
- ❌ Gradientes, blur de fondo, neumorphism
- ❌ Fondos con tinte de color (el fondo es neutro; el color es de los acentos)
- ❌ Emojis como iconos (usar Segoe MDL2 Assets / Path vectoriales)
- ❌ Texto con contraste < 4.5:1 (usar variantes oscuras #047857/#C2410C para texto sobre color)
- ❌ Estados sin foco visible (accesibilidad)

---

## Pre-Delivery Checklist

- [ ] Sin emojis como iconos (Segoe MDL2 / Path)
- [ ] Sin sombras decorativas fuera de modales
- [ ] `Cursor=Hand` en todo lo clickable
- [ ] Hover 150–200ms sin cambios de layout
- [ ] Contraste texto ≥4.5:1 (variantes oscuras sobre color)
- [ ] Focus visible en todo interactivo
- [ ] Montos `N2` con cifras tabulares
- [ ] Sin hex hardcodeado en vistas (tokens de App.xaml)
- [ ] `AutomationProperties.Name` en controles sin texto directo
