# Active Context — SSH Manager

## Foco actual
Finalizada la implementación de **Enviar archivo (`s`)** y **Recibir archivo (`r`)** con `scp`, incluyendo selectores de ruta local/remoto.

## Cambios recientes
- **Instalador reproducible**: `crear_instaladores.sh` publica `win-x64` y `linux-x64` en `instalador/{windows,linux}`, copia las plantillas `plantillas/parameters.conf.plantilla` y `plantillas/config.plantilla` (renombradas a `parameters.conf` y `config`) y `guia-rapida.md`, y genera `instalador/sshmanager-windows.zip` e `instalador/sshmanager-linux.tar.gz`. `instalador/` está en `.gitignore`.
- **Ruta de `parameters.conf` configurable**: `--parameters <ruta>` carga un archivo explícito; sin él se busca junto al ejecutable (`AppContext.BaseDirectory`). Las rutas relativas de `config` e `identity` se resuelven respecto a la carpeta de ese archivo.
- **Editar host (`e`) permite cambiar el alias**: nuevo `PromptAlias` (sin espacios ni duplicados), `HostBlock.Rename` reescribe solo el primer patrón de la línea `Host` conservando espaciado y patrones adicionales, y `ConfirmEdit` muestra el alias nuevo con su valor anterior. `EditHost` recibe ahora el `SshConfig` para validar duplicados.
- Colores configurables ampliados: `list-foreground`/`list-background` (filas no resaltadas), `hint-foreground`/`hint-background` (línea de ayuda del selector, mensaje `ESPACIO`) y `notice-foreground`/`notice-background` (avisos del selector, p. ej. "Usa ESPACIO para seleccionar"). `hint-foreground` y `notice-foreground` por defecto `Red`; `*-background` vacío = sin fondo. Nuevo helper `BoxLineColored` y método `Parameters.ParseColorOrNone`.
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
- El filtro del selector se **borra al cambiar de directorio** (subir/bajar). Se aplica con `/` en una ventana emergente: `ENTER` acepta, `ESC` cancela sin cambios.
- Selección **exclusiva con `ESPACIO`**; `ENTER` solo navega (entra en carpetas/subir), nunca selecciona (avisa si se pulsa sobre un archivo).
- `i` abre la ventana **"Ir a carpeta"** (disponible en origen y destino) para teclear una ruta absoluta/relativa/`~` y navegar.
- En la ventana de destino se muestra el **origen ya seleccionado** (`info`) en la primera línea del recuadro.
- Rutas remotas para `scp` van **sin comillas** (OpenSSH ≥ 9 usa SFTP por defecto: la ruta no pasa por shell). `ShQuote` solo para scripts `ssh host sh`.
