# ENGINEERING STANDARDS & ARCHITECTURE

## 1. ESTRUCTURA DE LA SOLUCIÓN (Hosted)
- **GestorOT.Shared:** SOLO contiene DTOs, Enums y validaciones. Prohibido incluir lógica de base de datos o referencias a EF Core.
- **GestorOT.Server:** Único responsable de la persistencia (Supabase). Los controladores deben ser 'Thin' (delgados), delegando la lógica a servicios.
- **GestorOT.Client:** Interfaz de usuario pura. Toda llamada a datos se hace mediante `HttpClient` hacia el Server.

## 2. GESTIÓN DE ERRORES Y DEBUGGING
- **Global Error Handling:** Todo controlador en el Server debe usar un `try-catch` de alto nivel que devuelva `ProblemDetails` en caso de fallo.
- **Client Logs:** Prohibido `Console.WriteLine`. Usar `ILogger<T>` inyectado para seguimiento técnico y `IMessageService` de AntDesign para feedback al usuario.

## 3. POLÍTICA DE "CERO CACHÉ" (Anti-PWA)
- **NO PWA:** Prohibido registrar Service Workers.
- **Asset Fingerprinting:** En .NET 10, usar `app.MapStaticAssets()` para asegurar que cada build tenga versiones de archivos únicas, forzando al navegador a descargar siempre lo último.
