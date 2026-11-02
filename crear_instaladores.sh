#!/usr/bin/env bash
# Genera el instalador de SSH Manager para Windows y Linux.
#
# Salida:
#   instalador/windows/                 binario win-x64 + parameters.conf + config
#   instalador/linux/                   binario linux-x64 + parameters.conf + config
#   instalador/sshmanager-windows.zip   contenido de instalador/windows/
#   instalador/sshmanager-linux.tar.gz  contenido de instalador/linux/
#   instalador/instalar.md              guía de instalación
#
# Uso: ./crear_instalador_zip.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_DIR="$SCRIPT_DIR/instalador"
TEMPLATES_DIR="$SCRIPT_DIR/plantillas"
PROJECT="$SCRIPT_DIR/sshmanager.csproj"

require() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "Error: falta '$1' en el PATH." >&2
        exit 1
    fi
}

require dotnet
require zip
require tar

if [[ -z "$INSTALL_DIR" || "$INSTALL_DIR" == "/" ]]; then
    echo "Error: ruta de instalador no válida." >&2
    exit 1
fi

for template in parameters.conf.plantilla config.plantilla; do
    if [[ ! -f "$TEMPLATES_DIR/$template" ]]; then
        echo "Error: no se encontró la plantilla '$TEMPLATES_DIR/$template'." >&2
        exit 1
    fi
done

# Se regenera por completo para no mezclar binarios de compilaciones antiguas.
rm -rf "$INSTALL_DIR"
mkdir -p "$INSTALL_DIR/windows" "$INSTALL_DIR/linux"

echo "==> Publicando Windows (win-x64)..."
dotnet publish "$PROJECT" \
    -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -o "$INSTALL_DIR/windows"

echo "==> Publicando Linux (linux-x64)..."
dotnet publish "$PROJECT" \
    -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -o "$INSTALL_DIR/linux"

echo "==> Copiando plantillas de configuración..."
for platform in windows linux; do
    cp "$TEMPLATES_DIR/parameters.conf.plantilla" "$INSTALL_DIR/$platform/parameters.conf"
    cp "$TEMPLATES_DIR/config.plantilla" "$INSTALL_DIR/$platform/config"
done
# El config puede contener hosts y rutas privadas.
chmod 600 "$INSTALL_DIR/linux/config" "$INSTALL_DIR/linux/parameters.conf"

echo "==> Generando guía de instalación..."
cat > "$INSTALL_DIR/instalar.md" <<'MD'
# Instalación de SSH Manager

## Contenido

- `sshmanager-windows.zip`: ejecutable para Windows x64.
- `sshmanager-linux.tar.gz`: ejecutable para Linux x64.
- Cada paquete incluye `parameters.conf` y `config` de ejemplo.

## Requisitos

- Cliente OpenSSH en el `PATH`: `ssh`, `scp` y `ssh-keygen`.
- En Linux/macOS también puede usarse `ssh-copy-id` (opcional).
- No hace falta instalar .NET: el ejecutable es self-contained.

## Windows

1. Instala el cliente OpenSSH si no lo tienes:
   *Configuración > Aplicaciones > Características opcionales > Cliente OpenSSH*.
2. Crea una carpeta, por ejemplo `C:\SshManager`.
3. Descomprime `sshmanager-windows.zip` dentro de esa carpeta.
4. Edita `parameters.conf` y `config` según tus servidores.
5. Abre PowerShell o CMD **en esa carpeta** y ejecuta:

   ```powershell
   .\sshmanager.exe
   ```

## Linux

1. Comprueba que OpenSSH está instalado: `ssh -V`.
2. Crea una carpeta, por ejemplo `~/sshmanager`.
3. Descomprime el paquete dentro de esa carpeta:

   ```bash
   mkdir -p ~/sshmanager
   tar -xzf sshmanager-linux.tar.gz -C ~/sshmanager
   cd ~/sshmanager
   chmod +x sshmanager
   ```

4. Edita `parameters.conf` y `config` según tus servidores.
5. Ejecuta desde esa carpeta:

   ```bash
   ./sshmanager
   ```

## Configuración

`parameters.conf` se busca en el **directorio de trabajo actual**, no junto al
ejecutable. Ejecuta siempre SSH Manager desde la carpeta donde está ese archivo.

Claves principales:

- `config =`: vacío usa `~/.ssh/config`; con una ruta, SSH Manager gestiona ese
  archivo y lo pasa a `ssh`/`scp` con `-F`.
- `identity =`: vacío usa las claves por defecto de `ssh`; con una ruta, se pasa
  a `ssh`/`scp` con `-i`.
- `max-servers`: número máximo de servidores visibles en pantalla.
- `highlight-*`, `list-*`, `hint-*`, `notice-*`: colores de la interfaz.

`config` es la configuración SSH que gestiona la aplicación. La plantilla incluye
un host de ejemplo; sustitúyelo por tus servidores o déjala vacía para usar
`~/.ssh/config`.

## Uso rápido

```bash
./sshmanager --help
./sshmanager --keygen
./sshmanager --exec usuario@host "uname -a"
./sshmanager --backup
```

Sin argumentos se abre la interfaz interactiva. La TUI necesita una terminal
interactiva: no funciona si la entrada está redirigida.

## Seguridad

- `config` e `identity` pueden contener rutas y datos sensibles. No compartas
  esos archivos ni los subas a un repositorio.
- Las copias generadas con `--backup` contienen claves privadas: guárdalas en
  un lugar seguro.
MD

echo "==> Creando archivos comprimidos..."
(cd "$INSTALL_DIR/windows" && zip -q -r "$INSTALL_DIR/sshmanager-windows.zip" .)
tar -czf "$INSTALL_DIR/sshmanager-linux.tar.gz" -C "$INSTALL_DIR/linux" .

echo
echo "Instalador generado en: $INSTALL_DIR"
ls -lh "$INSTALL_DIR/sshmanager-windows.zip" "$INSTALL_DIR/sshmanager-linux.tar.gz" "$INSTALL_DIR/instalar.md"
