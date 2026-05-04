# QA - Regresión completa Gestor OT

## Objetivo

Validar que los sprints no rompieron el flujo principal y que los bugs reportados quedaron cubiertos.

## Datos mínimos de prueba

Crear o identificar:

- Empresa/Tenant de prueba.
- Campaña activa: `2026/2027`.
- Campaña futura: `2028/2029`.
- Campaña bloqueada: `2025/2026`.
- Campo: `Campo Norte`.
- Lote: `Lote A`, 100 ha catastrales.
- Lote: `Lote B`, 50 ha catastrales.
- Actividades ERP: Maíz, Soja.
- Tipos de labor: Siembra, Pulverización.
- Insumos: Semilla, Herbicida.
- Contacto: Responsable propio.
- Contacto: Contratista.

---

# 1. Campañas

## Caso 1.1 - Crear campaña futura

Pasos:

1. Ir a Campañas.
2. Crear campaña `2028/2029`.
3. Guardar.
4. Abrir selector global sin refrescar navegador.

Resultado esperado:

- Aparece en selector.
- Se puede seleccionar.
- Aparece en tabla.

## Caso 1.2 - Bloquear campaña

Pasos:

1. Bloquear campaña.
2. Abrir selector.

Resultado esperado:

- Aparece como bloqueada.
- Se puede seleccionar.
- La UI indica solo lectura.

---

# 2. Lotes

## Caso 2.1 - Asignar varias campañas a un lote

Pasos:

1. Crear `Lote A`.
2. Asignar campaña `2026/2027`.
3. Asignar campaña `2028/2029`.

Resultado esperado:

- El lote muestra ambas relaciones.
- Cada relación tiene superficie productiva.

## Caso 2.2 - Selector excluye usadas

Pasos:

1. Abrir agregar campaña para `Lote A`.

Resultado esperado:

- No aparecen campañas ya asignadas.

## Caso 2.3 - Superficie mayor

Pasos:

1. Lote catastral 100.
2. Productiva 101.

Resultado esperado:

- Advierte.
- Permite guardar con confirmación.

## Caso 2.4 - GIS

Pasos:

1. Lote sin geometría: configurar.
2. Lote con geometría: editar.

Resultado esperado:

- Ambos flujos abren editor GIS.

---

# 3. Órdenes de Trabajo

## Caso 3.1 - Crear con campaña activa

Pasos:

1. Seleccionar campaña activa.
2. Click `Nueva Orden`.
3. Completar datos.
4. Guardar.

Resultado esperado:

- Modal abre.
- Guarda con `CampaignId`.
- No exige campo.

## Caso 3.2 - Sin campaña

Pasos:

1. Limpiar campaña.
2. Click `Nueva Orden`.

Resultado esperado:

- No abre modal.
- Muestra alerta.

## Caso 3.3 - Campaña bloqueada

Pasos:

1. Seleccionar campaña bloqueada.
2. Click `Nueva Orden`.

Resultado esperado:

- No permite crear.

---

# 4. Labores

## Caso 4.1 - Validación visual

Pasos:

1. Nueva labor.
2. Guardar sin datos.

Resultado esperado:

- Campos obligatorios en rojo.
- Mensajes debajo.
- No guarda.

## Caso 4.2 - Crear labor válida

Pasos:

1. Seleccionar campaña activa.
2. Seleccionar `Lote A`.
3. Completar actividad, tipo, fecha, hectáreas, responsable.
4. Agregar insumo con dosis.
5. Guardar.

Resultado esperado:

- Guarda.
- Tiene `CampaignLotId` correcto.

## Caso 4.3 - Lote multicampaña

Pasos:

1. Seleccionar campaña `2028/2029`.
2. Crear labor para `Lote A`.

Resultado esperado:

- Usa relación campaña-lote de `2028/2029`, no otra.

## Caso 4.4 - Hectáreas superiores

Pasos:

1. Crear labor con 101 ha sobre lote productivo 100.

Resultado esperado:

- Advierte.
- Permite guardar.

---

# 5. Rotaciones

## Caso 5.1 - Sin rotación

Pasos:

1. Crear labor en fecha sin rotación.

Resultado esperado:

- Warning.
- Permite guardar.

## Caso 5.2 - Rotación coincidente

Pasos:

1. Crear rotación Maíz.
2. Crear labor en esa fecha.

Resultado esperado:

- Actividad Maíz se carga.
- Selector queda bloqueado.
- Guarda.

## Caso 5.3 - Rotación conflictiva

Pasos:

1. Rotación Maíz.
2. Forzar labor Soja por API.

Resultado esperado:

- Backend rechaza.

---

# 6. Planeamiento Original

## Caso 6.1 - Nueva labor base

Pasos:

1. Seleccionar campaña activa.
2. Ir a Planeamiento Original.
3. Nueva Labor Base.
4. Completar.
5. Guardar.

Resultado esperado:

- No permite `Realizada`.
- Guarda como `Planned`.
- `IsOriginalPlan = true`.
- Aparece en tabla.

## Caso 6.2 - API protegida

Pasos:

1. Enviar `IsOriginalPlan=true` y `Status=Realized`.

Resultado esperado:

- Rechaza.

---

# 7. Estrategias

## Caso 7.1 - Crear estrategia

Pasos:

1. Crear estrategia `Maíz temprano`.
2. Actividad ERP: Maíz.
3. Agregar labores: Siembra y Pulverización.
4. Agregar insumos.

Resultado esperado:

- No hay actividad por labor.
- Lista muestra actividad Maíz.
- Muestra nombres reales de labores.
- Muestra insumos y dosis.

## Caso 7.2 - Aplicar estrategia

Pasos:

1. Aplicar a Lote A y Lote B.
2. Revisar preview.
3. Crear labores.

Resultado esperado:

- Preview claro.
- Fechas editables.
- Hectáreas con 2 decimales.
- Propio/Contratista claro.
- Crea cantidad esperada de labores.

## Caso 7.3 - Fechas con separación

Pasos:

1. Offsets 0, 5, 10.
2. Primera fecha 15/05.

Resultado esperado:

- Fechas 15/05, 20/05, 25/05.

## Caso 7.4 - Conflicto de rotación

Pasos:

1. Estrategia Soja.
2. Lote con rotación Maíz.

Resultado esperado:

- Preview bloquea.
- Backend rechaza si se fuerza.

---

# 8. Adjuntos

## Caso 8.1 - Adjuntar antes de guardar

Pasos:

1. Nueva labor.
2. Subir PDF.
3. Guardar.

Resultado esperado:

- Archivo asociado.
- Archivo en biblioteca.

## Caso 8.2 - Cancelar

Pasos:

1. Nueva labor.
2. Subir archivo.
3. Cancelar.

Resultado esperado:

- Pregunta eliminar o conservar.

## Caso 8.3 - Reutilizar

Pasos:

1. Usar archivo existente en otra labor.

Resultado esperado:

- No duplica contenido.
- Archivo asociado a ambas.

---

# 9. Campaña bloqueada

## Caso 9.1 - Solo lectura general

Pasos:

1. Seleccionar campaña bloqueada.
2. Intentar crear OT, labor, estrategia aplicada, adjunto, rotación o editar superficie.

Resultado esperado:

- Todas las mutaciones bloqueadas.
- Se puede consultar y descargar.

---

# 10. Resultado final esperado

El QA queda aprobado si:

- No hay errores de consola críticos.
- No hay errores 500 inesperados.
- Todas las validaciones críticas son visibles.
- Los endpoints rechazan payloads inválidos.
- Los bugs reportados quedan reproducidos y corregidos.
