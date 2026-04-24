Tarea:

Configuración de Program.cs: Implementá un método de extensión o un bloque lógico al inicio del pipeline que resuelva el DbContext mediante un IServiceScope.
Ejecución Asíncrona: Utilizá context.Database.MigrateAsync() para aplicar las migraciones pendientes.
Control de Entorno: La migración automática solo debe ejecutarse si el entorno es Development o Staging. Para Production, la aplicación debe ignorar este paso y esperar que las migraciones se apliquen vía script externo.
Logging y Robustez: Agregá un bloque try-catch que capture fallos en la migración, los registre en los logs y detenga el inicio de la aplicación si la base de datos no está sincronizada.
Instrucciones CLI: Proveé los comandos específicos de dotnet ef para generar la migración inicial y para generar el script SQL de producción.
Restricciones Técnicas:

Usar C# 14 / .NET 10.
Evitar EnsureCreated(), ya que rompe el flujo de migraciones.
Priorizar código limpio, asíncrono y sin bloqueos de hilo principal.
No usar emojis ni explicaciones genéricas; limitate a la implementación técnica y los comandos.