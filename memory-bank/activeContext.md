# Active Context — SSH Manager

## Foco actual
Finalizada la implementación de **Enviar archivo (`s`)** y **Recibir archivo (`r`)** con `scp`, incluyendo selectores de ruta local/remoto.

## Cambios recientes
- Nuevo `MenuAction.SendFile` / `MenuAction.ReceiveFile`, dispatch en `Main`, entradas de menú `s`/`r` y documentación en `--help`.
- Nueva clave `scp = scp` en `Parameters`, loader, plantilla y `parameters.conf`.
- Nuevas funciones: `SendFileToHost`, `ReceiveFileFromHost`, `RunScp`, `EnsureScp`, `PickPath`, `ListLocal`, `ListRemote`, `BuildRemoteListScript`, `RemotePathExists`, `RunCapture`, `SshPrefixArgs`.
- Helpers de rutas: `ShQuote`, `RemoteCombine`, `RemoteParent`, `LocalParent`, `RemoteBaseName`.
- Clases de apoyo: `FileEntry`, `PickerItem`, `PickerKind`.
- El listado remoto se envía por **stdin** (`ssh host sh`) para evitar problemas de comillas anidadas en shells de login (p. ej. csh de Solaris).
- Build validado: 0 errores / 0 advertencias.

## Próximos pasos
1. Smoke test local: `dotnet run -- --help`, render del menú y selector local.
2. Pruebas contra un host SSH real: enviar/recibir archivo y carpeta; confirmación de sobrescritura `s/N`; selector remoto desde `$HOME`.
3. Corregir posibles problemas de comillas/UX detectados en las pruebas reales.

## Decisiones activas y consideraciones
- Remoto solo POSIX; transferencia de carpetas con `scp -r`.
- Filtro del selector persistente entre subcarpetas y sin distinguir mayúsculas.
- Selección con `ESPACIO`; `ENTER` entra/abre; `BACKSPACE` sube; `/` filtra; `ESC` cancela.
