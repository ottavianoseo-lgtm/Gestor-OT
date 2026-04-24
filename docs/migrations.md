# Gestión de Migraciones — GestorOT

## Comportamiento por entorno

| Entorno | Migración automática al iniciar |
|---|---|
| `Development` | Sí — `MigrateAsync()` en startup |
| `Staging` | Sí — `MigrateAsync()` en startup |
| `Production` | No — aplicar manualmente con el script SQL |

La lógica vive en `src/GestorOT.Api/Extensions/MigrationExtensions.cs` y se invoca en `Program.cs` antes del pipeline HTTP.

Si la migración falla en Development/Staging, la aplicación no arranca y el error queda en los logs con nivel `Critical`.

---

## Crear una nueva migración

```bash
dotnet ef migrations add <NombreMigracion> \
  --project src/GestorOT.Infrastructure \
  --startup-project src/GestorOT.Api \
  --context ApplicationDbContext
```

Reemplazar `<NombreMigracion>` con un nombre descriptivo en PascalCase, por ejemplo `AddWorkOrderName`.

---

## Aplicar migraciones manualmente (cualquier entorno)

```bash
dotnet ef database update \
  --project src/GestorOT.Infrastructure \
  --startup-project src/GestorOT.Api \
  --context ApplicationDbContext
```

---

## Generar script SQL para producción

Genera el script incremental desde la última migración aplicada en prod hasta `HEAD`:

```bash
dotnet ef migrations script \
  --idempotent \
  --project src/GestorOT.Infrastructure \
  --startup-project src/GestorOT.Api \
  --context ApplicationDbContext \
  --output migrations_prod.sql
```

La flag `--idempotent` genera sentencias con verificación `IF NOT EXISTS` para que el script sea seguro de correr múltiples veces.

Para generar solo el delta desde una migración específica:

```bash
dotnet ef migrations script <MigracionBase> <MigracionDestino> \
  --project src/GestorOT.Infrastructure \
  --startup-project src/GestorOT.Api \
  --context ApplicationDbContext \
  --output migrations_prod.sql
```

Ejemplo:

```bash
dotnet ef migrations script 20260423115255_ManualLaborColumnsFix HEAD \
  --project src/GestorOT.Infrastructure \
  --startup-project src/GestorOT.Api \
  --context ApplicationDbContext \
  --output migrations_prod.sql
```

---

## Aplicar el script en producción

```bash
psql -h <host> -U <user> -d gestorot -f migrations_prod.sql
```

---

## Revertir la última migración (solo en Development)

```bash
dotnet ef migrations remove \
  --project src/GestorOT.Infrastructure \
  --startup-project src/GestorOT.Api \
  --context ApplicationDbContext
```

Solo elimina la migración si aún no fue aplicada a la base. Si ya fue aplicada, primero hacer `database update` a la migración anterior.
