# Contexto funcional del sistema Gestor OT

## 0. Objetivo general del sistema

### ¿Cuál es el objetivo principal de Gestor OT?
Completar: El objetivo principal es ser un módulo integrado del ERP (GESTORMAX) para la gestión de labores de una empresa agropecuaria. La idea es que la app consuma los datos de cada cliente de GestionMax y les permita realizar la planificación, ejecución y seguimiento de labores. Se busca tener el registro de todas las labores ejecutadas por los productores. 

### ¿Quiénes son los usuarios principales?
Ejemplo: administrador, asesor, productor, contratista, auditor, operario, etc.

Completar: Administrador, ingeniero, contratista, encargado

### ¿Qué problema concreto busca resolver?
Completar: Permitir a los usuarios gestionar las labores de manera eficiente. Esto implica el planeamiento de qué labores se van a realizar, cuándo y dónde (lotes de campos) y tener un registro de todas las labores ejecutadas por los productores como respuesta al planeamiento. Las labores se cargan desde la app y la idea es que a futuro viaje el registro a Gestor Max. Los usuarios pueden cargar labores propias o de contratistas.  

### ¿Qué cosas NO debería hacer el sistema?
Completar: El sistema no deberia violar el tenant principal de EMPRESA y en segundo lugar el de CAMPAÑA, TODO pasa en el contexto de una empresa en una campaña determinada. 

---

# 1. Campañas

## 1.1 Definición funcional

### ¿Qué representa una campaña?
Completar: Ciclo operativo de un año agrícola. Las campañas generalmente abarcan un año, de julio de un año a junio del otro. Pero puede pasar que el cierre de la campaña se demore por alguna razón y se extienda más en el tiempo. Por eso es importante que la campaña tenga un estado de Activa o Cerrada.   

### Ejemplo real de campaña
Ejemplo: Campaña 2028/2029

Completar: Campaña 2023/2024, Campaña 2024/2025, Campaña 2025/2026, Campaña 2026/2027, Campaña 2027/2028, Campaña 2028/2029




## 1.2 Estados de campaña

### ¿Qué significa que una campaña tenga `Status = Active`?
Completar: Significa que la campaña está activa y se pueden cargar labores en ella, Ordenes de Trabajo, etc. Las labores de la campaña a futuro se pueden ir creando pero se mantienen en un estado de "Planeamiento" 

### ¿Qué significa que una campaña tenga `Status = Locked`?
Completar: Significa que la campaña está bloqueada, es decir, que se bloqueo manualmente por que finalizó, la idea de bloquearla es que no se pueda registrar nada mas, solo visualizar. Los bloqueos de campaña se pueden hacer manualmente por el administrador. El objetivo es que no se generen modificaciones de información de labores realizadas que ya se cargaron al sistema y se imputaron a la contabilidad y gestión en Gestor Max.

### ¿Qué significa `IsActive = true`?
Completar: Que la campaña está activa

### Diferencia entre `Status = Active` e `IsActive = true`
Completar: Sinceramente no lo sé, no creo que tenga sentido tener ambos. Si una campaña está activa, debería estar en el selector y viceversa. Habría que pensarlo mejor.

## 1.3 Selector global de campaña

### ¿Qué campañas deben aparecer en el selector global?
Marcar o completar:

- [x ] Todas las campañas
- [ ] Solo campañas con `IsActive = true`
- [ ] Solo campañas con `Status = Active`
- [ ] Campañas activas y futuras
- [ ] Otro criterio: Pueden aparecer todas las campañas, con la diferencia de que si están activas, puedo trabajar sobre ellas (cargar nueva información) mientras que si están inactivas, es solo lectura de la información cargada.

Completar:

### ¿Una campaña bloqueada debe aparecer en el selector?
Completar: Debe aparecer para seleccionar pero solo con permisos de visualización, es decir, no puedo registrar nada. 

### ¿Una campaña futura, por ejemplo 28/29, debe aparecer automáticamente?
Completar: Si está cargada debe poder seleccionarse para empezar a planear las labores que se deberán realizar en ella, pero no debe forzarse su selección automática. 

### Cuando creo una nueva campaña, ¿debe seleccionarse automáticamente?
Completar: No, siempre se debe respetar el selector de campañas. El usuario indica sobre qué campaña quiere trabajar.

### Cuando creo una campaña, ¿debe aparecer inmediatamente en:
- Pantalla Campañas: Sí
- Selector global: Sí
- Selector de campañas por lote: Sí, si es que está activa
- Otros selectores:

Completar: Siempre que estés dentro de la app, trabajaras sobre una empresa, y dentro de una empresa, sobre una campaña. Al crear una nueva campaña, puedo seleccionarla principalmente dentro de Selector de Campañas, y todo lo que siga trabajando dentro de la app estará vinculado a esa campaña.


---

# 2. Campos y lotes

## 2.1 Campo

### ¿Qué representa un campo?
Completar: Un campo físico de una empresa agropecuaria, el campo simplemente es el espacio que contiene lotes. 

### ¿Un campo puede pertenecer a varias campañas?
Completar: Si, generalmente los campos están en todas las campañas salvo excepciones (que se venda un campo por ejemplo) 

### ¿Un campo tiene campañas asignadas directamente o solo sus lotes?
Completar: Es al revés, sobre la campaña se asignan lotes y por ende campos. El campo es simplemente un agrupador de lotes, lo cuál le facilita el filtro, búsqueda y asignación de lotes. 

## 2.2 Lote

### ¿Qué representa un lote?
Completar: Una unidad de superficie productiva, medida en Hectáreas, donde habrá cultivos y sobre los que realizarán labores. 

### ¿Un lote puede estar asignado a muchas campañas?
Completar: Si, los lotes suelen estar en múltiples campañas salvo excepciones (ventas de campos, inundaciones, incendios, etc). 

### ¿Un lote puede tener distinta superficie productiva según la campaña?
Completar: Si, ya que la superficie productiva de un lote se puede modificar por eventos naturales (inundaciones o incendios) o por una mejora en el suelo (por ejemplo que se realicen trabajo de limpieza y se gane espacio productivo en el lote).

### ¿Puede existir el mismo lote en dos campañas distintas con distinta rotación?
Completar: Sí, totalmente. Ejemplo: En la campaña actual para el “LOTE 1” tengo la rotación activa de maíz que termina en Agosto. Pero voy  planeando la campaña siguiente, por ende la creo y le asigno a ese lote las rotaciones de la próxima campaña. Asigno dos rotaciones una de fina y una de gruesa, por ejemplo trigo de agosto a diciembre y maíz de diciembre a agosto. Las rotaciones se asignan a la relación Lote/Campaña. Cuidando de no estar planeando rotaciones superpuestas en el mismo período para el mismo lote, viendola en campañas distintas (por ejemplo, si la campaña actual se atrasa, tendré un cultivo en ese lote que se puede superponer con el planeamiento de cultivos de la campaña siguiente).

## 2.3 Superficies

### Definición de superficie catastral
Completar: superficie declarada del lote 

### Definición de superficie productiva
Completar: Superficie productiva que estará activa en la campaña para determinado lote (ya que el lote puede variar entre campañas), todo lo que se planifique y realice, será sobre la superficie productiva

### ¿Cuál debe usarse por defecto para crear labores?
Marcar:

- [ x] Superficie productiva
- [ ] Superficie catastral
- [ ] Productiva si existe, catastral si no existe
- [ ] Otra regla:

Completar:

### ¿Una labor puede superar las hectáreas del lote?
Marcar:

- [ ] No, debe bloquearse
- [x ] Sí, pero debe advertir
- [ ] Sí, sin advertencia
- [ ] Depende del tipo de labor

Completar: Ejemplo ,tengo un lote de 100htas,  y planifico una labor (siembra) en ese lote. Luego viene el contratista y me dice que no hizo 100htas, sino que sembró 101, es muy raro que pase, pero puede pasar por discrepancias de datos. Debemos registrarlo, validando el desvío en htas (labor ejecutada te permite poner cantidad real de hta y de insumos) 

### ¿Una campaña asignada a un lote puede tener más hectáreas que el lote?
Completar: La cantidad de hectáreas de la campaña será la sumatoria de las hectáreas de los lotes que se utilicen en la campaña, no es un dato aparte. Lógicamente, una campaña tiene 1 o N lotes. Por ende será la suma de todas las superficies de todos los lotes. 




## 2.4 Asignación lote-campaña

### Cuando asigno una campaña a un lote, ¿el selector debe ocultar campañas ya usadas por ese lote?
Completar: Exactamente, así como campañas que ya no estén activas, ya sea que se hayan usado en ese lote o no.

### Si un lote tiene dos campañas asignadas, ¿dónde deben verse ambas?
Completar: En la sección de Lotes, ya sea dentro del modal de edición (desde donde le asigno una nueva campaña), desde un desplegable por lote para visualizar campañas asignadas o desde CAMPAÑAS > gestión de lotes.

### ¿Qué datos debe mostrar el desplegable del lote?
Ejemplo: campaña, superficie productiva, variación, rotaciones, acceso rápido.

Completar: campaña, superficie productiva, variación de superficie campaña a campaña. acceso rápido a rotaciones.

### ¿Debe haber acceso rápido a rotaciones desde cada campaña del lote?
Completar: Actualmente el acceso está en el modal edición de lote y ahí está para asignar rotaciones por campaña. A su vez hay un acceso rápido desde el desplegable para solo lectura. Estaría bueno un botón en el módulo GIS. Al seleccionar el lote mapeado te brinde un botón de “Administrar rotaciones” 


---

# 3. Rotaciones

## 3.1 Definición

### ¿Qué representa una rotación?
Completar: Secuencia de actividades agropecuarias que tendrá el lote en una campaña determinada, entre determinadas fechas (es decir, cultivos). 

### ¿La rotación se define por lote + campaña?
Completar: Sí. 

### ¿La rotación se define con actividad/cultivo ERP?
Completar: Sí.

## 3.2 Validaciones

### Si una labor se crea en una fecha sin rotación activa, ¿qué debe pasar?
Marcar:

- [ ] Bloquear creación
- [x ] Permitir con advertencia
- [ ] Permitir sin advertencia
- [ ] Depende del tipo de labor

Completar: Debe notificar que no hay rotación activa. Puede haber labores sin rotación, por ejemplo en un lote en el cual se decide que se descansará la tierra esa campaña y que se hagan labores de limpieza de suelo. 

### Si la actividad de la labor no coincide con la rotación del lote en esa fecha, ¿qué debe pasar? 
Marcar:

- [X ] Bloquear creación
- [ ] Permitir con advertencia
- [ ] Permitir sin advertencia
- [ ] Depende del tipo de labor

Completar:En caso de que se cree una labor en un lote a una determinada fecha en la cuál ya hay planeada una rotación, el campo Actividad trae por defecto la Actividad planeada en la rotación para ese lote a esa fecha. Lo muestra bloqueado y con un cartel que avisa que la Actividad está definida a nivel Rotación. Si se quiere modificar la Actividad, debe ir a las rotaciones. Distinto es si no hay una Rotación predeterminada para ese Lote a esa Fecha, en cuyo caso, el campo de Actividades queda libre para que el usuario indique la que quiera.

### ¿La validación de rotación debe aplicarse a:
- Labores sueltas: Sí 
- Labores desde estrategia: Sí
- Labores dentro de OT: Sí
- Planeamiento original: Sí
- Ejecución de labores: Sí

Completar: TODAS 

### ¿Qué mensaje debería ver el usuario ante un conflicto de rotación?
Completar:El sistema deberá notificar en el periodo activo que relación hay cargada (en caso de que haya). Si no hay rotación , el sistema debe notificar la ausencia de rotación. 


---

# 4. Estrategias

## 4.1 Definición

### ¿Qué representa una estrategia?
Completar: Las distintas Actividades (cultivos) suelen requerir siempre de una misma secuencia de Labores para poder llevarlo a cabo, con los mismos insumos, separados entre si por el mismo tiempo. Una estrategia viene a ser una matriz que ya contempla esa secuencia de labores. La idea es poder simplificar a un usuario la carga operativa. Si voy a sembrar Soja en 10 lotes, en vez de estar cargando labores en todos los lotes, hago una estrategia planificando todas las labores e insumos a aplicar para el cultivo Soja y lo aplico a esos 10 lotes. 

### ¿Una estrategia pertenece a una única actividad/cultivo?
Completar: Sí.

### ¿Las labores dentro de una estrategia heredan siempre la actividad de la estrategia?
Completar: Sí.

### ¿Tiene sentido permitir actividad distinta por cada labor dentro de una estrategia?
Completar: No. 

## 4.2 Labores de estrategia

### ¿Qué datos debe tener cada labor dentro de una estrategia?
Marcar/completar:

- [ x] Tipo de labor
- [ x] Días desde inicio / offset
- [x ] Insumos
- [ x] Dosis
- [ ] Responsable
- [ ] Hectáreas
- [x ] Actividad/cultivo
- [ ] Otro:

Completar: Además debe contemplar días de espera entre labor y labor (en etapa de planificación). 

### ¿Los insumos/dosis de una estrategia son obligatorios o sugeridos?
Completar: Sugeridos. 

### Cuando aplico una estrategia a lotes, ¿qué debe crear?
Marcar:

- [X] Labores sueltas
- [ ] Una OT
- [ ] Varias OT
- [ ] Planeamiento original
- [X] Depende de la pantalla desde donde se use

Completar: Las labores sueltas son la unidad de información. Las labores de Planeamiento Original son en sí Labores Sueltas, con la diferencia de que una vez creadas se guardan como solo lectura, para tener un contraste de lo planeado vs lo realizado. A su vez, una Orden de Trabajo es la agrupación de varias Labores Sueltas.

## 4.3 Fechas

### ¿Debe existir una Fecha Base?
Marcar:

- [ ] Sí
- [ ] No
- [X] Solo sugerida, editable en vista previa

Completar:

### ¿Cómo debe funcionar “mantener separación de fechas”?
Ejemplo: si una estrategia tiene labores el día 0, +5 y +10, y cambio la primera al 15/05, las demás pasan a 20/05 y 25/05.

Completar:

### Si desactivo “mantener separación de fechas”, ¿cada fecha queda independiente?
Completar: Sí. Por ejemplo, si tengo activo separación de fechas, y la estrategia tiene +5 días de separación entre labores, y en la primer labor pongo 15/05, la segunda labor se auto-completará con 20/05. Pero si quiero modificar una de las fechas por X motivo, puedo destildar la opción de “mantener separación de fechas”, me mantendrá las fechas existentes (15/05 y 20/5), pero puedo editarlas a voluntad.

## 4.4 Vista previa

### En la vista previa de labores desde estrategia, ¿qué datos deben mostrarse?
Marcar/completar:

- [X ] Nombre de estrategia
- [X] Actividad de estrategia
- [ ] Lote
- [X] Nombre real de labor
- [X] Insumos
- [X] Dosis
- [] Hectáreas con 2 decimales
- [ ] Responsable
- [ ] Propio / Contratista
- [ ] Advertencias de rotación
- [ ] Otro:

Completar:


---

# 5. Labores

## 5.1 Definición

### ¿Qué representa una labor?
Completar: Una labor es la documentación de qué se debe hacer, en qué lote, a qué fecha, sobre qué actividad, en qué cantidad de hectáreas, quién debe ejecutarla, bajo qué tipo de contratación (propio o contratista), con qué insumos y qué dosis por hectárea, en el caso del planeamiento, o lo mismo, pero realizado, en el caso de lo realizado. 

### Tipos de labor principales
Completar: Todas.

### Estados posibles de una labor
Completar: planeada o realizada.

### Diferencia entre `Status` y `Mode`, si existe funcionalmente
Completar: 

## 5.2 Creación de labor

### Campos obligatorios para crear una labor
Marcar/completar:

- [X ] Lote
- [X ] Actividad
- [X ] Tipo de labor
- [X] Hectáreas
- [X] Fecha
- [X] Responsable
- [X] Estado
- [X] Insumos
- [ ] Otro: Tipo de contratación del responsable (propio o contratista). Dosis por hectárea de los insumos

Completar: Siempre estoy trabajando dentro de una campaña, con lo cual, la labor se corresponde a una campaña, pero no es un dato que me pida el modal.

### Cuando falta un dato obligatorio, ¿cómo debe mostrarse?
Marcar:

- [X] Campo en rojo
- [ ] Mensaje general
- [X] Mensaje debajo del campo
- [ ] Toast/notificación
- [ ] Otro:

Completar:

### ¿Una labor puede crearse sin OT?
Completar: Sí, se la denomina labor suelta 

### ¿Una labor puede asignarse luego a una OT?
Completar: Sí

### ¿Una labor puede cambiar de lote después de creada?
Completar: Sería raro, pero podría editarse, sí.

### ¿Una labor puede cambiar de campaña después de creada?
Completar: No.

## 5.3 Estado Planeada / Realizada

### ¿Cuándo una labor debe nacer como Planeada?
Completar: A criterio del usuario, se utiliza justamente para planear a futuro, pero es un campo seleccionable por el usuario.

### ¿Cuándo una labor puede nacer como Realizada?
Completar: A criterio del usuario, se utiliza justamente para registrar lo realizado, pero es un campo seleccionable por el usuario.

### ¿Una labor del Planeamiento Original puede nacer como Realizada?
Completar: No.

### ¿Qué datos adicionales exige una labor Realizada?
Completar: Se ingresa la cantidad real de hectáreas laboreadas y la cantidad real de insumos utilizados total. Los coeficientes reales se obtienen a partir de la relación de estas dos variables. 


---

# 6. Órdenes de Trabajo

## 6.1 Definición

### ¿Qué representa una OT?
Completar: Es un documento que agrupa Labores para enviar a ejecutar, y vuelve (por lo general impreso en papel) con el detalle de las labores realizadas.

### ¿Una OT siempre pertenece a una campaña?
Completar: Sí. 

### ¿Una OT puede agrupar labores de distintos lotes?
Completar: Sí.

### ¿Una OT puede agrupar labores de distintas fechas?
Completar: Depende. La OT posee un encabezado desde donde se setea cómo debe comportarse. Una opción de dicho encabezado es justamente si permite o no incorporar labores con distintas fechas. De permitirlo, sí, se puede. De no permitirlo, se designa una fecha por defecto a nivel de encabezado de OT y se fuerza a que las labores que se agreguen a la OT hereden la fecha en cuestión. 

### ¿Una OT puede agrupar labores de distintos responsables?
Completar: Depende. La OT posee un encabezado desde donde se setea cómo debe comportarse. Una opción de dicho encabezado es justamente si permite o no incorporar labores con distintos responsables. De permitirlo, sí, se puede. De no permitirlo, se designa un responsable y tipo de contratación (propio o contratista) por defecto a nivel de encabezado de OT y se fuerza a que las labores que se agreguen a la OT hereden el responsable en cuestión. 


## 6.2 Creación de OT

### Si no hay campaña seleccionada, ¿qué debe pasar al hacer clic en “Nueva Orden”?
Marcar:

- [ ] No abrir modal y mostrar advertencia
- [ ] Abrir modal con selector de campaña obligatorio
- [ ] Seleccionar automáticamente la campaña más reciente
- [ ] Otro:

Completar: No deberías poder trabajar sin una campaña seleccionada.

### Estado inicial de una OT nueva
Completar: Existe un menú de creación de Estados de OT, donde el usuario puede configurar sus propios estados. De ellos, eligirá uno que sea un estado inicial por defecto, y a su vez indicará si los estados creados permiten o no continuar modificando la OT.

### Campos obligatorios para crear OT
Completar: Nombre (ya sea que es indique uno o que se genere por defecto), Estado (trae por defecto el que esté indicado por defecto, pero puedo modificarlo), checkbox para definir si permite o no múltiples personas, en caso de que no permita, campo de persona y tipo de contratación (propio o contratista), checkbox para definir si permite o no múltiples fechas, en caso de que no permita, campo se fecha.

### ¿Debe existir selector de campo en el modal de OT?
Completar: No.

## 6.3 OT y labores

### ¿Se pueden crear labores desde una OT?
Completar: Sí.

### Si existen labores sin OT, al crear una labor desde OT, ¿debe sugerir asignarlas?
Completar: Sí. Se sugiere revisar si la labor que estás queriendo crear no se encuentra ya creada, pero suelta, para evitar generar información duplicada.

### ¿Se puede crear una OT a partir de labores seleccionadas?
Completar: Sí.

### ¿Qué pasa si una labor ya tiene OT?
Completar: No la puedo asignar a otra OT, debo primero desvincularla de la OT que tiene asignada.


---

# 7. Planeamiento Original

## 7.1 Definición

### ¿Qué representa el Planeamiento Original?
Completar: La proyección de todas las labores planeadas, en sus repsectivos lotes, con sus respectivas rotaciones, a sus respectivas fechas, con sus respectivos insumos y dosis. Sirve como hoja de ruta para contrastar la realidad de lo que se realiza a lo largo de la campaña vs. el plan original.

### ¿Es una línea de base inmutable?
Completar: Sí, debería serlo. Pero algún usuario con permisos de administrador debería tener acceso para modificar alguna omisión.

### ¿Se puede editar?
Completar: No debería, salvo usuarios con el permiso correcto. 

### ¿Quién puede modificarlo?
Completar: Un administrador que carga con la responsabilidad del análisis de gestión de la empresa.

## 7.2 Creación

### ¿Cómo se carga el Planeamiento Original?
Marcar:

- [X ] Labor manual
- [X ] Estrategia
- [X] Importación
- [ ] Otro:

Completar:

### ¿Las labores del Planeamiento Original deben ser siempre Planeadas?
Completar: Sí.

### ¿Puede haber labores Realizadas dentro del Planeamiento Original?
Completar: No.

## 7.3 Desanclar

### ¿Qué significa “desanclar” una labor del Planeamiento Original?
Marcar/completar:

- [X ] Quitar marca `IsOriginalPlan`
- [ ] Crear copia editable
- [ ] Mover a labores normales
- [ ] Otro:

Completar:

### ¿Quién puede desanclar?
Completar: Administrador.

### ¿Debe quedar auditoría?
Completar: 

## 7.4 Comparación plan vs real

### ¿Cómo se vincula una labor planificada con una labor realizada?
Completar: La OT permite agrupar muchas labores independientemente del estado. Puedo tener labores pertenecientes a Planeamiento Original dentro de una OT y luego incorporar las labores Realizadas que fueron respuesta de las planeadas.

### ¿Qué métricas de desvío querés ver?
Marcar/completar:

- [X] Hectáreas plan vs real
- [X] Insumos plan vs real
- [X] Dosis plan vs real
- [X] Costo plan vs real
- [ X Fecha plan vs fecha real
- [ ] Otro:

Completar:


---

# 8. Adjuntos y archivos

## 8.1 Situación esperada

### ¿Qué tipos de archivo se adjuntan?
Ejemplo: PDF, imagen, Excel, receta, mapa de prescripción, contrato, remito. Idealmente también audios.

Completar: 

### ¿Los archivos se adjuntan a:
- Labor: Sí
- OT: Sí
- Lote: No
- Campaña: No
- Estrategia: No
- Otro:

Completar:

## 8.2 Adjuntar antes de guardar

### ¿Querés poder adjuntar archivos mientras se crea una labor, antes de guardar?
Completar: Sí.

### Si la labor se cancela, ¿qué pasa con los archivos subidos?
Marcar: 

- [ ] Se eliminan automáticamente
- [ ] Quedan en biblioteca general
- [X ] Se pregunta al usuario
- [ ] Otro:

Completar:Si el archivo se está subiendo por primera vez a la labor, y no es que la labor se está referenciando a un archivo ya existente, el archivo se elimina, previa pregunta al usuario.

## 8.3 Archivo reutilizable

### ¿Un mismo archivo puede asociarse a muchas labores?
Completar: Sí.

### ¿Debe existir una biblioteca general de archivos?
Completar: Sí.

### ¿Desde dónde se debería poder seleccionar un archivo ya cargado?
Completar: Desde labores u órdenes de trabajos. Debería poder cargar un nuevo archivo o seleccionar uno ya cargado.

### ¿Debe haber categorías o etiquetas para archivos?
Completar: Sí, sería útil.

## 8.4 Seguridad

### ¿Todos los usuarios pueden ver todos los archivos?
Completar: No.

### ¿Hay archivos privados o por rol?
Completar: Sí.

### ¿Hay límite de tamaño?
Completar: Sí.


---

# 9. Usuarios, permisos y roles

## 9.1 Roles existentes

### Listar roles del sistema
Completar: Mandante (la persona que determina qué se va a hacer, por ejemplo, el ingeniero agrónomo), Administrador (la persona que transforma las ordenes del mandante en labores planeadas y recibe la información de las labores realizadas para cargarlas al sistema), Mandado (el operario o contratista que recibe la orden para ir y ejecutarla en los lotes, por ejemplo, el contratista).

## 9.2 Permisos por rol

Completar la matriz:

| Acción | Admin | Asesor | Productor | Contratista | Auditor | Otro |
|---|---|---|---|---|---|---|
| Crear campaña | Admin | Mandante |
| Bloquear campaña | Admin | Mandante |
| Crear lote | Admin | Mandante |
| Editar lote |  Admin | Mandante |
| Editar geometría | Admin | Mandante |
| Crear rotación | Admin | Mandante |
| Crear estrategia | Admin | Mandante |
| Crear labor | Admin | Mandante | 
| Crear OT | Admin |
| Ejecutar labor | Admin | Mandante | Mandado |
| Validar labor | Admin |
| Desanclar planeamiento | Admin |
| Adjuntar archivos | Admin |
| Eliminar registros | Admin |


---

# 10. Estados y reglas de edición

## 10.1 Estados de OT

### Estados posibles de OT
Completar: Son a criterio del usuario

### ¿Qué acciones permite cada estado?

| Estado OT | Editar OT | Agregar labores | Editar labores | Ejecutar labores | Eliminar OT | Bloquea insumos |
|---|---|---|---|---|---|---|
| Draft |  |  |  |  |  |  |
| Pending |  |  |  |  |  |  |
| InProgress |  |  |  |  |  |  |
| Completed |  |  |  |  |  |  |
| Approved |  |  |  |  |  |  |
| Cancelled |  |  |  |  |  |  |

## 10.2 Estados de labor

### Estados posibles de labor
Completar: Planeada o Realizada

### ¿Qué acciones permite cada estado?

| Estado Labor | Editar | Asignar a OT | Ejecutar | Validar | Eliminar | Adjuntar |
|---|---|---|---|---|---|---|
| Planned |  |  |  |  |  |  |
| AwaitingValidation |  |  |  |  |  |  |
| Validated |  |  |  |  |  |  |
| Realized |  |  |  |  |  |  |


---

# 11. UX esperada

## 11.1 Principios generales

### ¿Qué prioriza la interfaz?
Marcar/completar:

- [X] Velocidad de carga
- [X] Simplicidad
- [ ] Control estricto
- [X] Advertencias antes que bloqueos
- [ ] Bloqueos fuertes para evitar errores
- [X] Vista tipo tablero
- [X] Vista tipo planilla
- [ ] Otro:

Completar:

## 11.2 Validaciones visuales

### Cuando falta un campo obligatorio, ¿qué debe pasar?
Completar: Advertir al usuario visualmente pintándose de rojo.

### Cuando hay advertencia agronómica, ¿qué debe pasar?
Completar:

### Cuando hay error bloqueante, ¿qué debe pasar?
Completar:

## 11.3 Modales

### ¿Preferís modales grandes tipo wizard o pantallas separadas?
Completar: Depende la cantidad de información que deban mostrar.

### ¿Rotaciones desde lote debe abrir drawer/modal o navegar a otra pantalla?
Completar:

### ¿GIS/polígono debe abrir modal/drawer o navegar al mapa?
Completar:


---

# 12. Integración ERP / Gestor Max

## 12.1 Actividades ERP

### ¿Qué representa una Actividad ERP?
Completar: Una actividad es un cultivo en el plan de cuentas del cliente de GestorMax

### ¿La actividad ERP es obligatoria en labores?
Completar: Sí, y debe validar por la rotación activa en ese periodo para ese lote. 

### ¿La actividad ERP debe venir siempre desde rotación?
Completar: Sí, siempre que esté planeada.

## 12.2 Personas / contratistas

### ¿Los responsables vienen del ERP?
Completar: SI 

### Diferencia funcional entre Propio y Contratista
Completar: Propio es un responsable de la labor que pertenece a la organización (empleado); Contratista es una persona externa que se la contrató para ese servicio y luego el emite la factura del servicio. 

### ¿Propio/Contratista afecta facturación o solo clasificación?
Completar: ambas, aunque en esta versión no haremos nada referido a facturación. Cuando envíe a Gestor Max las labores realizadas por un Contratista, el cuál nos factura, se traducirá en una deuda de facturas a pagar.


## 12.3 Insumos

### ¿Los insumos vienen del ERP/inventario?
Completar: Sí.

### ¿Las dosis son planeadas, reales o ambas?
Completar: ambas, las reales serán al completar la labor. 

### ¿Se debe controlar stock?
Completar: Actualmente no tenemos stock en el sistema, queda pendiente a resolver en próxima versión. 


---

# 13. Bugs reportados: prioridad real

Completar prioridad para cada uno:

| Bug / Mejora | Prioridad | Bloqueante | Comentario funcional |
|---|---:|---|---|
| Adjuntar archivo al crear labor | Alta / Media / Baja | Sí / No |  |
| Campos obligatorios en rojo | Alta / Media / Baja | Sí / No |  |
| Campaña 28/29 no aparece en selector | Alta / Media / Baja | Sí / No |  |
| Selector campaña por lote debe excluir usadas | Alta / Media / Baja | Sí / No |  |
| Nueva campaña no refresca lista | Alta / Media / Baja | Sí / No |  |
| Acceso rápido a rotaciones por campaña | Alta / Media / Baja | Sí / No |  |
| No se puede editar polígono con lote creado | Alta / Media / Baja | Sí / No |  |
| Bloqueo por hectáreas superiores al lote | Alta / Media / Baja | Sí / No |  |
| Crear OT no abre modal | Alta / Media / Baja | Sí / No |  |
| Lote con dos campañas muestra una sola | Alta / Media / Baja | Sí / No |  |
| Campo Cultivo/Actividad dentro de labor de estrategia no tiene sentido | Alta / Media / Baja | Sí / No |  |
| Superposición botón eliminar/quitar labor | Alta / Media / Baja | Sí / No |  |
| Unificar modales de labores desde estrategia | Alta / Media / Baja | Sí / No |  |
| Estrategia muestra labores como “Labor” | Alta / Media / Baja | Sí / No |  |
| Quitar Fecha Base precargada | Alta / Media / Baja | Sí / No |  |
| Decimales Ha por lote solo 2 | Alta / Media / Baja | Sí / No |  |
| Propio/Contratista más obvio | Alta / Media / Baja | Sí / No |  |
| Crear Labores no funciona | Alta / Media / Baja | Sí / No |  |
| Vista previa de estrategias no muestra actividad/labores | Alta / Media / Baja | Sí / No |  |
| Encabezado sin nombre de estrategia | Alta / Media / Baja | Sí / No |  |
| Chequeo actividad estrategia vs rotaciones | Alta / Media / Baja | Sí / No |  |
| Planeamiento Original no debe crear Realizada | Alta / Media / Baja | Sí / No |  |
| Guardar labor en Planeamiento Original no crea | Alta / Media / Baja | Sí / No |  |


---

# 14. Criterio de aceptación general

## 14.1 Para considerar corregido el módulo Campañas
Completar: 

## 14.2 Para considerar corregido el módulo Lotes
Completar:

## 14.3 Para considerar corregido el módulo Rotaciones
Completar:

## 14.4 Para considerar corregido el módulo Estrategias
Completar:

## 14.5 Para considerar corregido el módulo Labores
Completar:

## 14.6 Para considerar corregido el módulo OT
Completar:

## 14.7 Para considerar corregido el módulo Planeamiento Original
Completar:

## 14.8 Para considerar corregido el módulo Adjuntos
Completar:


---

# 15. Decisiones técnicas permitidas

## 15.1 Cambios de base de datos

### ¿Se pueden agregar tablas nuevas?
Completar: Si es necesario agregar, sí 

### ¿Se pueden agregar columnas?
Completar: Si es necesario agregar, sí 


### ¿Se pueden crear migraciones EF Core?
Completar:Si es necesario agregar si 


### ¿Hay datos productivos que preservar?
Completar: no 

## 15.2 Refactor

### ¿Se permite refactorizar componentes grandes?
Completar: Si es necesario agregar si 


### ¿Se permite mover lógica de `.razor` a servicios?
Completar:Si es necesario agregar si 


### ¿Se permite modificar DTOs compartidos?
Completar:Si es necesario agregar si 


### ¿Se permite modificar endpoints existentes?
Completar: Si es necesario agregar si 


### ¿Se permite agregar endpoints nuevos?
Completar:Si es necesario agregar si 


## 15.3 Estilo de trabajo

### ¿Preferís:
- [ ] Un PR grande con todos los fixes
- [ x] Varios PR chicos por módulo
- [ ] Primero informe, luego PRs
- [ ] Solo plan para que lo ejecute otro agente

Completar:


---

# 16. Información técnica adicional

## 16.1 Entorno

### ¿Dónde corre hoy?
Completar: Servidor Local 	

### ¿Base de datos?
Completar: PostSQL

### ¿Tiene datos reales o es entorno de prueba?
Completar: Datos reales- consultas api a clientes de GestorMax

### ¿Hay deploy automático?
Completar: no

