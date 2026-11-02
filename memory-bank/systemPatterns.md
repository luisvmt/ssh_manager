# System Patterns — SSH Manager

## Arquitectura
- Un único `Program.cs` con la clase estática `Program` (namespace `SshManager`) y las clases de apoyo al final del archivo.
- `Main` parsea argumentos (`--help`, `--keygen`, `--exec`, `--backup`) y, sin argumentos, entra en el bucle de la TUI.

## Patrón de acciones (TUI)
- `enum MenuAction { Connect, ExecCommand, CopyKey, SendFile, ReceiveFile, SelectKey, GenerateKey, Add, Edit, Delete, Filter, Backup, Quit }`.
- `ShowMenu` devuelve `(MenuAction, sel)` y `Main` lo despacha a su flujo. Las acciones `s`/`r` solo se activan con un host seleccionado.

## Dibujo de la interfaz
- Helpers estáticos: `BoxTop`, `BoxLine`, `BoxRow`, `BoxSeparator`, `BoxBottom`, `BoxGrid`, `BoxWidth` (ancho recortado a 24–100 columnas).
- Fila resaltada con colores de `parameters.conf`.

## Configuración (`parameters.conf`)
- `clave = valor`; claves ausentes usan valor por defecto. La carga está en `Parameters.Load`.
- Añadir una clave requiere tocar `Parameters`, `Load`, la plantilla y el propio archivo.

## Ejecución de procesos externos
- `ProcessStartInfo` con `ArgumentList` para `ssh`, `scp`, `ssh-keygen`, `ssh-copy-id`.
- `SshPrefixArgs` añade `-F <config>` (si existe) e `-i <identity>`.
- `RunCapture` ejecuta capturando stdout/stderr y soporta enviar un `stdin` (usado para listar en remoto).

## Patrón remoto POSIX
- Listados remotos con `ssh host sh` + script por stdin. El script usa solo `sh` POSIX (backticks, `printf`, `test -e`/`-L`, `pwd -P`) y emite registros `tipo<TAB>symlink<TAB>nombre` separados por NUL.

## Selector de archivos (`PickPath`)
- `PickerItem`/`PickerKind` (Current, Parent, Directory, File); `FileEntry` para entradas.
- Navegación, filtro persistente (subcadena, sin distinguir mayúsculas), `ESPACIO` selecciona, `ENTER` entra/abre, `ESC` cancela.
- Local usa `Directory.GetCurrentDirectory()`; remoto usa `$HOME`.

## Transferencia con `scp`
- `RunScp` construye `scp` con `-F`/`-i`, `-r` solo para carpetas, y rutas remotas con `ShQuote`.
- Confirmación de sobrescritura `s/N` comprobando existencia local o remota (`RemotePathExists`).
