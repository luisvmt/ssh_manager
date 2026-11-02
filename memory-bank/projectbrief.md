# Project Brief — SSH Manager

## Objetivo
Aplicación **TUI (interfaz de texto)** para gestionar conexiones SSH desde la terminal: listar, conectar, editar, añadir y eliminar hosts; gestionar claves; ejecutar comandos remotos; hacer copias de seguridad; y transferir archivos por `scp`.

## Requisitos núcleo
- Gestionar un archivo de configuración SSH (por defecto `~/.ssh/config`, configurable vía `parameters.conf` con `-F`).
- Gestionar claves (generar `ed25519`, copiar clave pública con `ssh-copy-id`, seleccionar clave privada para la sesión con `-i`).
- Ejecutar comandos en un host seleccionado.
- Crear backups `.tar.gz` de claves y configuración.
- **Enviar/recibir archivos o carpetas** con `scp` mediante un selector de rutas local y remoto (POSIX).

## Alcance
- Único ejecutable C#/.NET (single-file publish para Windows y Linux).
- Sin dependencias externas más allá de las herramientas OpenSSH (`ssh`, `scp`, `ssh-keygen`, `ssh-copy-id`).
- Remoto asumido **POSIX** (Linux/macOS/Solaris), no Windows.

## Fuente de verdad del alcance
Este documento define el objetivo general; los detalles técnicos viven en `systemPatterns.md` y `techContext.md`, y el estado actual en `progress.md`.
