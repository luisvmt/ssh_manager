# SSH Manager — guía para agentes

TUI de consola en **C# / .NET 10** (`net10.0`) para gestionar conexiones SSH. Sin tests, sin CI, sin NuGet, sin solución `.sln`: un único proyecto y un único archivo de código.

## Contexto obligatorio

- Las convenciones del proyecto viven en este archivo, que es la fuente única para cualquier agente (OpenCode, Cline u otro). El estado del proyecto está en `memory-bank/*.md`: léelo si la tarea toca arquitectura, configuración o UX, y mantenlo sincronizado al cambiar el código (es costumbre del repo).
- **Todo el código, comentarios, cadenas de UI y documentación van en español.**
- `Program.cs` (~2200 líneas) es la fuente de verdad. La clase estática `Program` (namespace `SshManager`) está arriba; las clases de apoyo (`HostBlock`, `ConfigLine`, `SshConfig`, `Parameters`, `FileEntry`, `PickerKind`, `PickerItem`) al final del mismo archivo.

## Control de versiones

- Antes de empezar cualquier tarea que modifique archivos, ejecuta `git status --porcelain`. Si hay cambios sin commitear, créalos como commit de seguridad: `git add -A && git commit -m "checkpoint: antes de <tarea>"`. El mensaje del commit debe incluir la fecha y hora del commit.
- Antes de ejecutar `dotnet build` no hace falta otro commit; el checkpoint inicial cubre la tarea.

## Comandos

```bash
dotnet build                 # debe dar 0 errores y 0 advertencias
dotnet run -- --parameters ./parameters.conf          # TUI interactiva desde el repo
dotnet run -- --parameters ./parameters.conf --help   # verificación no interactiva
dotnet run -- --parameters ./parameters.conf --keygen [nombre]
dotnet run -- --parameters ./parameters.conf --exec <user@host> <comando>
dotnet run -- --parameters ./parameters.conf --backup [directorio]
```

`dotnet run` sin `--parameters` busca `parameters.conf` en `bin/Debug/net10.0/`, no en la raíz del repo.

Publicación self-contained (ver `compilar.txt`):

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r win-x64   --self-contained true -p:PublishSingleFile=true
```

Instalador completo (Windows + Linux, con plantillas y `guia-rapida.md`): `./crear_instaladores.sh`. Genera `instalador/` (ignorado por Git) con los binarios, `sshmanager-windows.zip` y `sshmanager-linux.tar.gz`; requiere `zip` y `tar`.

Tras cualquier cambio de código: `dotnet build` y confirmar **0 advertencias**. No hay suite de tests; la verificación funcional de la TUI es manual.

## Gotchas de ejecución

- **La TUI exige stdin interactivo** (`Console.ReadKey`). Con stdin redirigido (shell de agente, CI) aborta con error. Para validar sin TUI usa `--help`, `--keygen`, `--exec` o `--backup`. En VS Code hay que usar `"console": "integratedTerminal"` (ver `.vscode/launch.json`).
- `parameters.conf` se busca **junto al ejecutable** (`AppContext.BaseDirectory`). `--parameters <ruta>` permite usar otro archivo explícitamente.
- Las rutas relativas de `config` e `identity` se resuelven respecto a la carpeta del `parameters.conf` usado, no al CWD.
- Si falta `parameters.conf`, el programa ofrece crear una plantilla en la carpeta del ejecutable; sin terminal interactiva responde "no" y termina con código 1. Si la ruta pasada con `--parameters` no existe, termina con error sin preguntar.
- `--config` y `--identity` ya no existen como flags: se configuran en `parameters.conf`.
- Los valores con lado derecho vacío se ignoran en `Parameters.Load` (así los fondos `*-background =` significan "sin fondo").

## Configuración (`parameters.conf`)

- Formato `clave = valor`; claves ausentes o inválidas conservan el valor por defecto.
- Claves: `ssh`, `ssh-keygen`, `ssh-copy-id`, `scp`, `config`, `identity`, `max-servers` y los pares de color `highlight-*`, `list-*`, `hint-*`, `notice-*`.
- El archivo se elige con `--parameters`; sin esa opción se usa el que está junto al ejecutable.
- Añadir una clave exige tocar **cinco sitios**: campo en `Parameters`, rama en `Parameters.Load`, `Parameters.WriteTemplate`, `parameters.conf` y `PrintHelp` (además del memory-bank).

## Arquitectura y convenciones

- `Main` parsea argumentos y, sin ellos, entra en el bucle de la TUI: carga `SshConfig`, dibuja con `ShowMenu` y despacha `MenuAction` (valores como `Connect`, `SendFile`, `ReceiveFile`). Las acciones sobre host comprueban que haya selección.
- Convenciones C# estándar: métodos estáticos en PascalCase, variables locales en camelCase.
- `SshConfig` conserva el texto original de las líneas SSH (`ConfigLine.Raw`) y el preludio. No reformatees bloques existentes: editar/añadir/eliminar debe preservar comentarios y líneas ajenas.
- `SshConfig.Save` escribe **siempre LF** (portabilidad) y aplica permisos `600` en Unix. No cambies esto por `Environment.NewLine`.
- Los tags se guardan como comentario `# Tags: a, b` inmediatamente antes de la línea `Host`; solo se reconoce el primer patrón de `Host` como nombre del servidor.
- UI dibujada con helpers propios: `BoxTop`, `BoxLine`, `BoxLineColored`, `BoxRow`, `BoxSeparator`, `BoxBottom`, `BoxGrid`, `BoxWidth` (ancho recortado 24–100). Reutilízalos en vez de escribir `Console.WriteLine` para cajas.
- Selector de rutas (`PickPath`): `ESPACIO` selecciona, `ENTER` solo navega, `/` filtra en ventana modal, `i` abre "Ir a carpeta", `ESC` cancela. Mantén esos atajos.

## Procesos externos y remoto

- Invoca `ssh`, `scp`, `ssh-keygen` y `ssh-copy-id` con `ProcessStartInfo` + `ArgumentList`; **nunca** cadenas de comando interpoladas.
- `SshPrefixArgs` añade `-F <config>` (solo si el archivo existe) e `-i <identity>`.
- El remoto se asume **POSIX `sh`** (Linux/macOS/Solaris): usa backticks, `printf`, `test -e`/`test -L`, `pwd -P`; evita bashisms y `$(...)`.
- Listados remotos: `ssh host sh` con el script enviado por **stdin** (`RunCapture`), no `sh -c`.
- En `scp`, las rutas remotas (`host:ruta`) van **sin comillas** (OpenSSH ≥ 9 usa SFTP y las comillas pasarían a formar parte del nombre). `ShQuote` es solo para scripts `ssh host sh`.

## Seguridad

- No leas, muestres ni copies `id_ed25519` ni el contenido de `config`.
- `.clineignore` los excluye del contexto de Cline, pero **OpenCode no lo lee**: en cualquier agente, respeta esta regla aunque no haya un archivo de exclusión que lo imponga.
- Ojo: **Git los sigue rastreando** (también los `ssh-backup-*.tar.gz`). No los añadas a commits nuevos ni generes más artefactos con secretos.
- Confirma antes de sobrescribir archivos (patrón `s/N`).