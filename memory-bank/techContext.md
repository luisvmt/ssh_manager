# Tech Context — SSH Manager

## Stack
- **Lenguaje**: C# / .NET 10 (`net10.0`), `Nullable` e `ImplicitUsings` habilitados.
- **Librerías del framework**: `System.Formats.Tar`, `System.IO.Compression` (backups), `System.Diagnostics` (procesos), `System.Runtime.InteropServices`.

## Herramientas externas (requeridas en el PATH)
- `ssh`, `scp`, `ssh-keygen`, `ssh-copy-id` (configurables en `parameters.conf`).

## Setup de desarrollo
- Compilar: `dotnet build`.
- Publicar (single-file, self-contained):
  - Windows: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
  - Linux:   `dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true`
- VS Code: `launch.json` y `tasks.json` configurados para depurar y compilar.

## Restricciones técnicas
- Remoto asumido **POSIX** (Linux/macOS/Solaris); los comandos remotos deben ser `sh` compatible.
- La TUI depende de `Console.ReadKey(true)` y de `Console.WindowWidth` (no interactiva si stdin está redirigido).

## Dependencias
- Ninguna externa de NuGet; solo el SDK de .NET y las herramientas OpenSSH.

## Convenciones de tooling
- `parameters.conf` define rutas y valores; `config` y `identity` se pasan a `ssh`/`scp` con `-F` e `-i`. Se busca junto al ejecutable salvo que se indique con `--parameters`; sus rutas relativas se resuelven respecto a su propia carpeta.
- Backups se nombran `ssh-backup-<AAAAmmdd-HHMMSS>.tar.gz`.
