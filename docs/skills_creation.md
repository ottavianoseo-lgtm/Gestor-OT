Para crear una skill (habilidad) en OpenCode, necesitas definir un conjunto de instrucciones y metadatos dentro de una estructura de carpetas específica. OpenCode utiliza estas skills bajo demanda para darle a tu agente flujos de trabajo reutilizables, contexto o acceso a scripts sin saturar la ventana de contexto principal.

Aquí tienes el paso a paso para crear una:

1. Elige la ubicación de tu Skill
Dependiendo de si quieres que la skill esté disponible solo para el proyecto actual o para cualquier proyecto en tu computadora, debes crear la carpeta en la ruta correspondiente:

Para un proyecto específico: Crea la ruta .opencode/skills/<nombre-de-tu-skill>/ en la raíz de tu proyecto.

Global (para todos tus proyectos): Crea la ruta ~/.config/opencode/skills/<nombre-de-tu-skill>/ en tu directorio de usuario.

Regla para el nombre: El <nombre-de-tu-skill> (y el nombre de la carpeta) debe tener entre 1 y 64 caracteres, usar solo letras minúsculas y números, y puede contener guiones medios (ej. mi-skill-personal, generador-api).

2. Crea el archivo SKILL.md
Dentro de la carpeta que acabas de crear, debes generar un archivo llamado exactamente SKILL.md. Este será el núcleo de tu skill.

3. Configura el Frontmatter (YAML)
El archivo SKILL.md debe comenzar con un bloque YAML en la parte superior. Esto es crucial porque le dice a OpenCode de qué trata la skill y en qué momento el agente debe activarla de forma automática.

Abre tu SKILL.md y agrega lo siguiente:

Markdown
---
name: nombre-de-tu-skill
description: |-
  Describe aquí de forma clara y específica qué hace esta skill y cuándo debe usarse.
  Puedes incluir ejemplos:
  - Usuario dice "crear endpoint" -> Usa esta skill para generar el código.
---

# Instrucciones de la Skill
A partir de aquí, escribes en Markdown todas las instrucciones, el contexto, las reglas de código o los pasos que el agente de OpenCode debe seguir cuando active esta skill.
name (Obligatorio): Debe coincidir exactamente con el nombre de la carpeta.

description (Obligatorio): Puede tener entre 1 y 1024 caracteres. Sé muy específico, ya que OpenCode lee esto para decidir si la necesita cargar o no durante una conversación.

4. Agrega recursos adicionales (Opcional)
Si tu flujo de trabajo es complejo, OpenCode te permite organizar recursos extra dentro de la carpeta de tu skill. El agente podrá inspeccionarlos cuando la skill se active:

scripts/: Carpeta para guardar archivos ejecutables (ej. un script de Python o Bash que el agente puede correr).

references/: Carpeta para guardar esquemas de bases de datos, documentación técnica o referencias de APIs.

assets/: Carpeta para guardar plantillas de código o archivos base.

5. Cómo usarla en OpenCode
Una vez guardada, la skill ya es detectable.
Al abrir el chat de OpenCode en tu terminal, el agente la descubrirá automáticamente leyendo los directorios. Puedes dejar que el agente la cargue por su cuenta basándose en el contexto de tu prompt, o puedes forzarla diciéndole explícitamente:

"Usa la skill nombre-de-tu-skill para..."

paths para context7 Repo principal para Context7: microsoft/agent-framework
Repo secundario de apoyo: microsoft/semantic-kernel