
# A continuacion se muestra modelos de prompt para todos los proyectos

## Inicializar archivos en cline
Crea un archivo .clinerules en base al actual proyecto
Crea tambien un archivo .clineignore basado en el actual proyecto
Configura auto-compact
Asimismo, inicializa una carpeta MemoryBank
Que otros archivos o configuraciones recomiendas para optimizar el uso de cline?

## Inicializar archivos en opencode
Para usarlo con opencode, crea un archivo AGENTS.md en base al actual proyecto
Tambien crea un archivo opencode.json con la configuracion para usar un modelo para Plan y un modelo para Build, similar al siguiente:
{
  "$schema": "https://opencode.ai/config.json",
  "model": "deepseek/deepseek-v4-flash",
  "small_model": "deepseek/deepseek-v4-flash",
  "agent": {
    "plan":  { "model": "deepseek/deepseek-v4-pro" },
    "build": { "model": "deepseek/deepseek-v4-flash" }
  }
}
Que otros archivos o configuraciones recomiendas para optimizar el uso de opencode?