# Propuesta: Línea estética "Minimalismo Funcional" para Uenta POS

> **Estado: APROBADA E IMPLEMENTADA (10-ago-2026).**
> Bryan aprobó el 10-ago con las recomendaciones de Theo (preguntas abiertas resueltas: barra indicadora en sidebar, botones ghost, presets de color para Ajustes).
> Implementación: App.xaml (paleta neutra, radius 6, ThemeMode Fluent, ghost), MainWindow (sidebar con barra indicadora + fix de navegación), vistas de catálogo. Verificado con build limpio (0 warn/0 err), 20/20 tests y capturas DPI-aware.
> **Decisión de Bryan:** el proyecto está en etapa temprana → es el momento de definir la línea estética; **todo lo que se agregue después debe seguir esta línea**.
> La dirección vive ahora en `MASTER.md` (fuente de verdad) + este archivo (el "por qué").

---

## 1. La dirección: Minimalismo Funcional (estilo Swiss)

Basado en los estilos **"Minimalism & Swiss Style"** y **"Swiss Modernism 2.0"** del catálogo de la skill UI/UX Pro Max (ambos califican: WCAG AAA, enterprise/dashboards/tools profesionales, complejidad baja).

**Por qué este y no "Exaggerated Minimalism":** la skill, si la dejamos elegir sola con un query "minimal", escoge el exagerado (tipografía gigante, whitespace extremo, pensado para landing pages de lujo). Un POS es una **herramienta de trabajo de alta densidad**: el cajero necesita ver producto, carrito, totales y stock de un vistazo. El minimalismo aquí no es "menos contenido", es **menos ruido visual para que el contenido mande**.

### Principios (los 5 mandamientos)

1. **Contenido primero.** Nada decorativo compite con los datos. Si un elemento no ayuda a cobrar más rápido, no está.
2. **Profundidad mínima.** Superficies planas; la jerarquía la hacen bordes finos (hairlines), contraste y tipografía, no sombras.
3. **Disciplina de color.** Los neutros dominan. El verde y el naranja son **los únicos** colores de énfasis, y cada uno tiene UN trabajo.
4. **Tipografía como jerarquía.** Pesos/tamaños/espaciado hacen el trabajo que hoy hacen los "boxes de color".
5. **Movimiento casi nulo.** Transiciones 150–200ms solo donde ayudan (hover de botón, foco). Nada de `translateY`, elevaciones ni scroll-reveal (eso es web, no WPF).

---

## 2. Paleta: neutros + 2 acentos configurables

### 2.1 Arquitectura de tokens (clave para el punto 2 de Bryan)

Los colores de énfasis viven en **tokens semánticos intercambiables en runtime**:

| Token semántico | Default (hoy) | Trabajo único |
|---|---|---|
| `PrimaryBrush` | Verde `#059669` | Acciones principales + selección + enlaces |
| `AccentBrush` (CTA) | Naranja `#EA580C` | **Solo** la acción de cobrar y acciones críticas/destructivas de confirmación |

- En WPF: `DynamicResource` en App.xaml → la pantalla de **Ajustes** (Fase 1+) cambia estos dos valores en runtime sin reiniciar.
- **Restricción de diseño:** máx. 2 acentos (decisión de Bryan). El resto de la app es neutra por definición.
- **Recomendación con opinión:** ofrecer **paleta preseleccionada** (8–12 colores con contraste verificado) en Ajustes, NO un selector libre de color. Un color arbitrario puede romper el contraste AA de texto blanco sobre botón; con presets garantizamos que cualquier elección del usuario se vea bien. (Se puede ampliar después si un cliente lo pide.)

### 2.2 Variantes de contraste (detalle importante)

Con texto blanco encima, el verde `#059669` da ~3.4:1 y el naranja `#EA580C` ~3.0:1. Suficiente para texto grande (AA large ≥3:1), insuficiente para texto normal (4.5:1). Regla:

- **Verde `#059669`**: superficies de estado (pill activo, selección) con **texto verde sobre blanco**, o fondos con texto grande.
- **Verde oscuro `#047857`** (variante de contraste): fondos sólidos con texto blanco normal (botón primario).
- **Naranja `#EA580C`**: solo el botón COBRAR (texto grande) y avisos de confirmación críticos.
- **Naranja oscuro `#C2410C`**: si alguna vez el naranja lleva texto pequeño.

Esto es exactamente lo que la skill marca como "contrast parity" — lo dejamos resuelto en el design system, no por pantalla.

### 2.3 Neutros

| Rol | Valor propuesto | Reemplaza |
|---|---|---|
| Fondo app | `#F8FAFC` (gris frío muy claro) | `#ECFDF5` (verde tinto actual — quita el tinte de color del fondo) |
| Superficie | `#FFFFFF` | — |
| Muted | `#F1F5F9` | `#F0F8F6` |
| Borde (hairline) | `#E2E8F0` | `#E1F2ED` |
| Texto primario | `#0F172A` (sin cambio) | — |
| Texto secundario | `#64748B` | — |
| Destructive | `#DC2626` (sin cambio) | — |

---

## 3. Tipografía

- **Se mantienen:** Rubik (títulos/números) + Nunito Sans (cuerpo). Son buenas y ya están en el design system.
- **Jerarquía más dura:** los totales y montos son los elementos más importantes de la pantalla → tamaño grande, peso 600–700, **cifras tabulares** (no se mueven al cambiar de 9 a 10), color `#0F172A` máximo contraste.
- **Menos pesos en uso:** limitar a 400/600/700 (hoy se usan más); el peso hace la jerarquía, no el color.
- **Menos "chips" y etiquetas de color:** el estado se comunica con texto + 1 indicador puntual, no con cajas de color.

---

## 4. Componentes (tratamiento concreto)

| Componente | Hoy (Soft UI) | Propuesto (Minimal Funcional) |
|---|---|---|
| Botones | Radio 8px, sombra suave, hover `translateY(-1px)` | Radio 6px, **sin sombra**, hover = oscurecer fondo 5% (150ms), foco visible con ring |
| Botón COBRAR | Naranja con sombra | Naranja sólido, **el único** elemento de color fuerte de la pantalla |
| Cards de producto | Sombra md + hover eleva | Superficie blanca + hairline `#E2E8F0`; hover = borde se oscurece + fondo `#F8FAFC`; seleccionado = borde/halo verde |
| Inputs | Borde `#E2E8F0`, foco con sombra 3px | Igual, foco = borde verde + ring fino `2px` (sin blur) |
| Sidebar | Pill verde de activo | Activo = fondo `#F1F5F9` + texto/icono verde + **indicador de barra izquierda 3px verde** (más Swiss, menos "pill blando") |
| Tablas (futuro catálogo) | — | Hairlines verticales solo donde aportan; filas con hover sutil; números alineados a la derecha |
| Modales | Sombra xl + blur | Sombra solo aquí (es la excepción permitida), sin blur del fondo |
| Scrollbars | — | Estilo delgado consistente con la línea |

**Anti-patrones prohibidos (actualizan el MASTER):**
- ❌ Emojis como iconos (ya prohibido; se mantiene con Segoe MDL2)
- ❌ Sombras decorativas en superficies de contenido
- ❌ Hover con elevación/transform (causa jitter y ruido)
- ❌ Más de 2 acentos en pantalla
- ❌ Gradientes, blur de fondo, neumorphism
- ❌ Fondos con tinte de color (el fondo es neutro; el color es de los acentos)

---

## 5. Densidad — punto de partida y plan de pruebas (punto 3 de Bryan)

"Probar hasta dar con la correcta" → propongo un **punto de partida y un loop de verificación rápida**:

- **Punto de partida: densidad 6/10** (hoy 8/10). Más aire entre secciones (espaciado base 8px → 8/12/16/24), pero sin sacrificar "todo visible de un vistazo".
- **Qué se prueba concretamente (en orden):**
  1. Paddings y gaps del grid de productos (¿cuántos productos por fila se ven bien con más aire?)
  2. Altura de header y del panel de venta (carrito)
  3. Tamaño de los totales (jerarquía tipográfica)
  4. Grosor de hairlines y radios de borde
- **Loop de verificación:** ajustar tokens → build → captura DPI-aware (lección del 07-ago: nunca medir UI WPF desde caller DPI-unaware) → comparar → repetir. 2–3 iteraciones por elemento, no más.
- **Regla de corte:** si en la iteración 3 un cambio no se ve mejor, se queda como estaba. El minimalismo no es excusa para iterar infinito.

---

## 6. Lo que NO cambia (reglas duras del proyecto)

- Flujo de la pantalla de Ventas: header 48px + catálogo 58% / venta 42%, buscador siempre enfocado (flujo escáner), COBRAR con F8.
- Atajos F2/F4/F8/F9.
- Comportamiento: stock bajo = aviso ámbar no bloqueante, carrito, pagos mixtos, etc.
- Arquitectura: Desktop → Application → Domain; WPF solo presenta.
- ThemeMode Fluent (guía WPF de la skill para .NET 9) — se mantiene y verifica.
- Iconos Segoe MDL2 Assets (ya sin emojis).

---

## 7. Plan de ejecución — COMPLETADO (10-ago-2026)

1. ✅ **Design system actualizado** (MASTER.md reescrito con la línea Minimalismo Funcional; la skill se usó para fundamentar, el estilo se fijó manualmente para evitar Exaggerated Minimalism).
2. ✅ **Tokens en App.xaml** — neutros nuevos, variantes de contraste, ThemeMode="Light" (Fluent .NET 9), radius 6, ghost secondary.
3. ✅ **Componentes retocados** — sidebar (barra indicadora 3px + fix del bug de navegación: `CurrentChanged` ahora notifica los flags activos), botones, cards, inputs.
4. ✅ **Densidad 6/10** aplicada en tokens de espaciado del design system; loop de densidad pendiente de afinar con Bryan en uso real.
5. ✅ **Infra de Ajustes** — los acentos ya son `DynamicResource` en App.xaml (intercambiables en runtime); la pantalla de Ajustes llega en Fase 1. Pendiente: definir presets de color con contraste verificado.

## 8. Preguntas abiertas — RESUELTAS (10-ago)

1. **Indicador de sidebar:** ✅ barra izquierda 3px verde (Swiss puro), como recomendó Theo.
2. **Botones secundarios:** ✅ ghost sin borde (hover = fondo suave), como recomendó Theo.
3. **Ajustes de color:** ✅ presets preseleccionados de 8–12 colores con contraste verificado (pendiente de implementar con la pantalla de Ajustes en Fase 1).
