# Product Context — SSH Manager

## Por qué existe
Centralizar la gestión de múltiples conexiones SSH en una TUI ligera y portable, sin depender de herramientas gráficas ni configuraciones dispersas.

## Problemas que resuelve
- Navegar y conectar a muchos hosts desde una sola lista navegable con teclado.
- Editar hosts, generar/copiar claves y cambiar la identidad sin salir de la terminal.
- Respaldar de forma reproducible claves y configuración.
- Transferir archivos hacia/desde un host sin recordar la sintaxis de `scp`.

## Metas de experiencia de usuario (UX)
- Toda la interacción por teclado (↑/↓, Enter, teclas de acción).
- Interfaz clara con cajas y fila resaltada configurable (colores en `parameters.conf`).
- Mensajes y ayudas en español.
- Confirmaciones de seguridad (p. ej. sobrescritura `s/N`).

## Cómo debería funcionar
Al arrancar se carga `parameters.conf` y se muestra la lista de hosts del config SSH gestionado. Cada acción (`MenuAction`) se despacha desde el bucle principal de `Main` a su flujo correspondiente, reusando los helpers de dibujo y los patrones de ejecución de procesos externos.
