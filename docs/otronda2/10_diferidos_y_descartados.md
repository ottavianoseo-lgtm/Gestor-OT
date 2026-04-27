# Items descartados / diferidos

Estos puntos del feedback original fueron **omitidos intencionalmente** de los sub-planes porque son decisiones abiertas que requieren debate antes de implementar.

---

## 🟠 Para debatir antes de implementar

### Desasignar Campaña de un Lote
> "Ver que pasa si quiero desasignar una Campaña a un Lote. Porque desencadenaría una cascada de incoherencias en relaciones."

**Estado**: Diferido. Requiere definir qué ocurre con las labores, rotaciones y planeamiento vinculados al `CampaignLot`. Se recomienda una reunión antes de codificar.

---

### Presupuesto de Campaña (cotización de labores)
> "El presupuesto total de la Campaña así como dato suelto no tiene mucha razón de ser... precio de Labores × Ha (ej. Pulverización = 5 USD × Ha)..."

**Estado**: Diferido (segunda vuelta). Eliminar el campo de asignación manual de presupuesto en Campaña está incluido como tarea simple, pero el sistema completo de cotización es un módulo nuevo que requiere especificación aparte.

**Tarea simple incluida** en `02_ot_modal_y_tabla.md`: eliminar campo de presupuesto manual de Campaña del formulario.

---

### Planeamiento masivo de Rotaciones
> "Sería muy útil pensar una herramienta de planeamiento de Rotaciones masiva para muchos Lotes en simultaneo."

**Estado**: Diferido. Concepto válido pero requiere diseño de UX antes de implementar. No hay suficiente especificación para guiar a un agente.

---

## ✅ Simplificaciones aplicadas

| Ítem original | Decisión |
|---|---|
| Eliminar campo "rendimiento objetivo" en asignación masiva de Campos/Lotes | Incluido como tarea puntual en `04_lotes_campana_area.md` — simplemente quitar el campo del form y el DTO |
| Eliminar presupuesto manual de Campaña | Tarea de 5 minutos: quitar `<FormItem>` del modal de Campaña y el campo del DTO enviado |
| Interfaz de dibujo de polígonos "poco intuitiva" | Marcado para revisión visual en reunión (no codificar a ciegas) |
