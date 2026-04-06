# 🎨 GUIÓN DE ESTILO: PREMIUM COMMAND CENTER
Interfaz de alta fidelidad basada en tendencias SaaS 2026.

## 1. ESTÉTICA GLASSMORPHISM
- **Sidebar Flotante:** Diseño con `backdrop-filter: blur(10px)` y fondo `rgba(30, 30, 46, 0.7)`.
- **Dashboard Widgets:** Tarjetas con bordes sutiles de 1px (blanco transparente) y sombras suaves.
- **Color Brand:** Rojo Principal `#E74C3C` para acentos y estados críticos.

## 2. COMPONENTES CLAVE (ANT DESIGN)
- **Ultra-Modern OT Manager:** Vista de lista compacta usando `AntTable` con `StickyHeader`.
- **Modern GIS Explorer:** Mapa a sangre (full-bleed) con panel lateral deslizante ("Sliding Side Rail") y "Bottom Sheet" de analíticas para visualización máxima.
- **Status Indicators:** Usar `AntTag` con colores semánticos (Healthy/Watch/Issue) y bordes redondeados.
  
## 3. LOGO SVG RESPONSIVE
- El logo debe usar `viewBox` y `preserveAspectRatio="xMidYMid meet"` para evitar que se corte en el Sider comprimido.
