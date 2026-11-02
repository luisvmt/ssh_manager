# Progress — SSH Manager

## Qué funciona
- TUI con lista navegable de hosts, conectar, ejecutar comandos, copiar clave, seleccionar identidad, generar claves, añadir/editar/eliminar host, filtrar y salir.
- Backups `.tar.gz` de claves y configuración.
- `--help`, `--keygen`, `--exec`, `--backup`.
- Configuración externa vía `parameters.conf` (programas, config, identity, colores, max-servers).
- **Enviar (`s`) / Recibir (`r`) archivos y carpetas** con `scp` y selectores de ruta local/remoto (listado POSIX por stdin, `-r` para carpetas, confirmación de sobrescritura `s/N`).
- Selector mejorado: selección solo con `ESPACIO`, filtro en ventana emergente (`/`), filtro que se limpia al cambiar de directorio, "Ir a carpeta" (`i`) en origen y destino, y origen seleccionado visible en la ventana de destino.
- Corregido el doble entrecomillado de rutas remotas en `scp` (OpenSSH ≥ 9 usa SFTP: la ruta no debe ir entre comillas simples).

## Qué falta / pendiente
- Pruebas reales de `s`/`r` contra un host SSH (enviar/recibir archivo y carpeta, sobrescritura, selector remoto desde `$HOME`, "Ir a carpeta" con `~`).

## Estado actual
- Build con **0 errores / 0 advertencias**.
- Listado remoto POSIX validado con nombres con espacios, ocultos y symlinks.

## Issues conocidos
- Si el destino ya existe al copiar una carpeta, `scp -r` puede anidar la carpeta origen dentro del destino (se advierte con la confirmación `s/N`).
- La TUI no funciona si `stdin` está redirigido (requiere terminal interactiva).

## Evolución de decisiones
- Se sustituyó el paso de `--config`/`--identity` por CLI por la gestión centralizada en `parameters.conf`.
- El listado remoto pasó de `sh -c '...'` (comillas anidadas) a `ssh host sh` con script por stdin, para robustez frente a shells de login no POSIX.
- Rutas remotas de `scp` pasaron de ir con `ShQuote` a ir sin comillas: OpenSSH ≥ 9 usa SFTP por defecto (la ruta no pasa por shell), y las comillas se trataban como parte del nombre (`'/tmp/x'`).
