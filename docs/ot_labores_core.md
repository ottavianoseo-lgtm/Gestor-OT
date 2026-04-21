Orden de Trabajo

Lo que tienen planteado hoy en día en el modal de Creación de Ordenes de Trabajo es bastante distinto a lo que se necesita.
Debemos tener una lista de Estaos de la OT. Naturalmente una OT se crea en un estado libre de Edición, pero hay otros estados donde una OT ya no puede Editarse (lo único que se podría Editar es el propio Estado, para permitir que vuelva a ser Editable), y si paso una OT a un estado bloqueado, por cascada se bloquean también todas las Labores ya sean planeadas o realizadas a es OT. Queda todo como de “solo consulta”. Entonces tenemos que crear una interfaz de creación de estados de OT (que traiga naturalmente tres, por ejemplo, en proceso, cerrada, cancelada) pero que el usuario pueda editarlos a voluntad.

Después, dentro de la versión de la app que hicieron, siempre estás trabajando dentro de una campaña, que la selecciono en el menú izquierdo, arriba. Con lo cuál, tener un botón de selección de campaña dentro del modal de creación de OT es una redundancia. Que tome la campaña en la cuál estoy laburando y chau.

Costo estimado como un número suelto en OT no tiene sentido. En todo caso, sería la sumatoria de los Costos de las Labores que vaya a incluir en esa OT.

Lo que sí debe tener el modal de Creación de OT:
Estado (lo que explicaba antes)
Nombre
Personas
Si será Propio o Contratista.
Y acá tengo que poder definir reglas. Como las Labores en sí también tienen personas: ¿Voy a permitir que haya labores con Múltiples Personas? O ¿Heredan todas las labores la Persona asignada en el encabezado de la OT, independientemente de que tengan o no otra persona asignada?

A su vez, cada Labor tiene su propia Fecha, entonces lo mismo: ¿Permite que una OT tenga muchas labores en distintas fechas? O ¿Fuerzo a que todas las labores que asigno tengan la misma fecha del encabezado?

Una vez creada la OT, puedo asignarle Labores Sueltas o crear Labores “Nativas” dentro de la misma OT.

Al entrar a una OT debo tener la siguiente información:
Sección Encabezado (editable. Es la info que le asigné cuando la cree)
Luego, por debajo del encabezado, la OT tiene dos caras: la mitad Planeada y La mitad Realizada. La información que se muestra en cada una deriva de las labores Planeadas y Realizadas que le fui asignando. Entonces por debajo tengo 3 tablas:

Labores Asignadas: Tendré el ID único de Cada labor, el Nombre, la Fecha de Ejecución, el Tipo de Labor, la Actividad, la cantidad de Ha, en qué Lote/Campo se hizo y la cantidad de Insumos que se utilizó. Y un botón al final de Ver Labor que me lleva a la información cargada en el croquis anterior.
Luego, en la “pestaña” de planeadas, tengo una tabla de Total de Insumos, donde me muestra la sumatoria por tipo de insumos. Es decir, si planee 3 labores distintas, que llevan las 3 mezclas de un insumo X en distintas dosis, y tengo que la Labor A usa 10.3 litros, la B usa 9 litros, y la C usa 11 Litros, en la tabla figurará que el insumo X total utilizado en esta OT es de 30,3.
Pero después hay una columna de Retiro Aprobado, donde se va a indicar qué cantidad real de insumo se le va a permitir retirar a la Persona. Entonces si el cálculo me da 30,3 pero yo sé que vienen en bidones de 5 litros, le digo que puede retirar 35 litros. Porque no puede llevarse un físico con coma. Y por último una columna de Centros donde le digo de dónde lo puede retirar.

Debajo tengo otra tabla llamada Detalle de Insumos por Labor, donde no se agrupan por tipología de producto, sino que es el detalle de todas las aplicaciones planeadas de cada insumo de cada labor. Esta tabla tiene:
Labor a la que pertenece
Actividad
Lote
Campo
Insumo
Ha
Coef/Ha
Cantidad
Unidad de medida del insumo

Y finalmente, por debajo, una lista con los archivos adjuntos de los cuales se derivan las labores que están dentro de la OT.
Toda esta interfaz hoy falta. Dejo un croquis de cómo podría ser:

PLANEADO

Esto, así tal cuál, tengo que poder compartírselo a la persona que va a llevar a cabo las Labores, el contratista. Ahí tiene toda la info de qué labores tiene que hacer en qué lotes. Cuánto insumo tiene que retirar y qué dosis de insumo aplicar por Ha.
image.png



REALIZADO

Esta es la contracara de la información, es lo que efectivamente se llevó a cabo por parte del contratista. Si son muy prolijos serán igual la mitad Planeada que la realizada, pero lo más probable es que no coincidan. De hecho podría entrar una realizada por la ventana sin que nunca haya sido planeada, y que le falta una “mitad”, esta info, que es la real, es lo que va a parar al gestor.
image.png

Tiene algunas diferencias vs la tabla de Planeadas, ya que acá se refleja la realidad. En la sección de Total Insumos, hay un campo de llamado Total (que viene del calculo de dosis por Ha de la tabla Planeadas, es decir, se hereda ese valor) pero al lado tiene un Total Utilizado. Este campo es un dato que se ingresa a mano que es efectivamente el valor que se usó.

Por otro lado, como las cantidad reales pueden variar, en la tabla de Detalles de Insumos por Labor hay dos campos nuevos, Coeficiente Calculado y Cantidad Calculada.

Este valor se calcula solo en función del valor total, de la siguiente manera: Si en el planeamiento tengo dos labores, ambas con Glifosato, con un coeficiente por Ha de 2. En una labor, la cantidad de Ha es 10, osea que Glifosato = 20 (10 Ha x Coef. 2) y el la otra labor la cantidad de Ha es 5, queda Glifosato = 10. En la tabla de Total de Insumos, le digo que retire 30 para que ya cargue la maquina para hacer ambas labores.

Pero luego viene tengo las Labores Realizadas que reflejan la realidad, y me dice que al final de la primera Labor fueron 9 Ha, en vez de las 10 planeadas, de la segunda fueron 4,5 Ha, en vez de 5. Y si bien regulan la maquina para que largue la dosis correctas, lo que ellos ven es el total gastado, entonces termió trabajando arriba de 13,5 Ha, y el total real de Glifosato gastado fue de 29.
En la columna Coef. Calculado y Total Calculado se hace una regla de tres para saber cuanto se aplico, proporcionalmente en cada Lote. Se sacan los porcentajes en función del planeamiento, y luego se aplican esos porcentajes por sobre lo realizado.
image.png
image.png

 Con esta base, luego se pueden crear mediante filtros y consultas distintos informes. Ejemplo:

Ver todo el historial de insumos en un lote específico (acá hay que ver cómo se resuelve concatenación de lotes en caso de que dividan un lote, o agrupen N lotes para formar uno nuevo).
Ver toda la planificación de Labores para un campo en particular. 
Etcétera. 

Son solo distintas formas de agrupar la información, si todo lo previo se hizo correctamente.
