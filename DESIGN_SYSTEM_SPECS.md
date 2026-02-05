# 🎨 GESTOR OT - VISUAL DESIGN SYSTEM (AGRIVANT UI)

## 1. PALETA DE COLORES (EXTRACTO DE MOCKUPS)
- **Primary Brand Red:** `#E74C3C` (Acciones principales, Botones 'Submit').
- **Status Green:** `#2ECC71` (Healthy / Done).
- **Status Gold:** `#F1C40F` (Watch / In Progress).
- **Surface Dark:** `#1E1E2E` (Fondo del Dashboard de Inteligencia).
- **Surface Light:** `#FFFFFF` (Sidebar y Header).

## 2. COMPONENTES ANT DESIGN BLAZOR
- **Sidebar (Menu):** Fondo blanco. Iconos en gris suave. El elemento activo debe tener un borde lateral izquierdo rojo y un fondo rojo traslúcido (`#FCE8E6`).
- **Dashboard Widgets:** Usar `AntCard` con `Bordered="false"` y `Shadow` sutil. Títulos en negrita, fuente sans-serif.
- **Work Orders Table:** Usar `AntTable` con `Size="Small"`. Los estados deben representarse con `AntTag` usando colores semánticos (Rojo/Amarillo/Verde).
- **GIS Panel:** El panel lateral en el mapa debe ser un `AntDrawer` o un panel flotante con `Z-Index` superior, fondo blanco y transparencia del 95%.

## 3. LOGO SVG RESPONSIVE
- El logo debe usar `viewBox` y `preserveAspectRatio="xMidYMid meet"` para evitar que se corte en el Sider comprimido.
