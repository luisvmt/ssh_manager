# System Patterns — SSH Manager

## Arquitectura
- Un único `Program.cs` con la clase estática `Program` (namespace `SshManager`) y las clases de apoyo al final del archivo.
- `Main` parsea argumentos (`--help`, `--keygen`, `--exec`, `--backup`, `--parameters`) y, sin argumentos, entra en el bucle de la TUI.

## Patrón de acciones (TUI)
- `enum MenuAction { Connect, ExecCommand, CopyKey, SendFile, ReceiveFile, SelectKey, GenerateKey, Add, Edit, Delete, Filter, Backup, Quit }`.
- `ShowMenu` devuelve `(MenuAction, sel)` y `Main` lo despacha a su flujo. Las acciones `s`/`r` solo se activan con un host seleccionado.

## Dibujo de la interfaz
- Helpers estáticos: `BoxTop`, `BoxLine`, `BoxRow`, `BoxSeparator`, `BoxBottom`, `BoxGrid`, `BoxWidth` (ancho recortado a 24–100 columnas).
- Fila resaltada con colores de `parameters.conf` (`highlight-foreground`/`highlight-background`).
- Filas no resaltadas configurables (`list-foreground`/`list-background`); la línea de ayuda del selector (`ESPACIO`) usa `hint-foreground`/`hint-background` y los avisos del selector (p. ej. "Usa ESPACIO…") usan `notice-foreground`/`notice-background` (helpers `BoxLineColored`). Los `*-background` vacíos = sin fondo.

## Configuración (`parameters.conf`)
- `clave = valor`; claves ausentes usan valor por defecto. La carga está en `Parameters.Load`.
- Sin `--parameters`, el archivo se busca junto al ejecutable (`AppContext.BaseDirectory`). Las rutas relativas de `config` e `identity` se resuelven respecto a la carpeta del `parameters.conf` cargado.
- Añadir una clave requiere tocar `Parameters`, `Load`, la plantilla y el propio archivo.

## Ejecución de procesos externos
- `ProcessStartInfo` con `ArgumentList` para `ssh`, `scp`, `ssh-keygen`, `ssh-copy-id`.
- `SshPrefixArgs` añade `-F <config>` (si existe) e `-i <identity>`.
- `RunCapture` ejecuta capturando stdout/stderr y soporta enviar un `stdin` (usado para listar en remoto).

## Patrón remoto POSIX
- Listados remotos con `ssh host sh` + script por stdin. El script usa solo `sh` POSIX (backticks, `printf`, `test -e`/`-L`, `pwd -P`) y emite registros `tipo<TAB>symlink<TAB>nombre` separados por NUL.

## Selector de archivos (`PickPath`)
- `PickerItem`/`PickerKind` (Current, Parent, Directory, File); `FileEntry` para entradas.
- `ESPACIO` selecciona (exclusivo); `ENTER` solo navega; `BACKSPACE` sube; `/` abre una ventana emergente de filtro (ENTER acepta, ESC cancela); `i` abre la ventana "Ir a carpeta"; `ESC` cancela.
- El filtro (subcadena, sin distinguir mayúsculas) se borra al cambiar de directorio. `DrawPromptBox` dibuja la caja modal encima del selector.
- El parámetro opcional `info` muestra el origen ya seleccionado en la ventana de destino. `ResolveNavigate` resuelve rutas tecleadas (absolutas, relativas y `~`; los scripts remotos expanden `~`).
- Local usa `Directory.GetCurrentDirectory()`; remoto usa `.` (HOME real resuelto con `pwd`).

## Transferencia con `scp`
- `RunScp` construye `scp` con `-F`/`-i`, `-r` solo para carpetas.
- Las rutas remotas (`host:ruta`) se pasan **sin comillas**: OpenSSH ≥ 9 usa SFTP por defecto y la ruta no pasa por shell, por lo que comillas simples se interpretarían como parte del nombre. `ShQuote` queda reservado a los scripts `ssh host sh`.
- Confirmación de sobrescritura `s/N` comprobando existencia local o remota (`RemotePathExists`).
