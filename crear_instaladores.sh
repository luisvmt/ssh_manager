#!/usr/bin/env bash
# Genera el instalador de SSH Manager para Windows y Linux.
#
# Salida:
#   instalador/windows/                 binario win-x64 + parameters.conf + config + guia-rapida.md
#   instalador/linux/                   binario linux-x64 + parameters.conf + config + guia-rapida.md
#   instalador/sshmanager-windows.zip   contenido de instalador/windows/
#   instalador/sshmanager-linux.tar.gz  contenido de instalador/linux/
#
# Uso: ./crear_instaladores.sh

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

if [[ ! -f "$SCRIPT_DIR/guia-rapida.md" ]]; then
    echo "Error: no se encontró '$SCRIPT_DIR/guia-rapida.md'." >&2
    exit 1
fi

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

echo "==> Copiando plantillas y guía rápida..."
for platform in windows linux; do
    cp "$TEMPLATES_DIR/parameters.conf.plantilla" "$INSTALL_DIR/$platform/parameters.conf"
    cp "$TEMPLATES_DIR/config.plantilla" "$INSTALL_DIR/$platform/config"
    cp "$SCRIPT_DIR/guia-rapida.md" "$INSTALL_DIR/$platform/guia-rapida.md"
done
# El config puede contener hosts y rutas privadas.
chmod 600 "$INSTALL_DIR/linux/config" "$INSTALL_DIR/linux/parameters.conf"

echo "==> Creando archivos comprimidos..."
(cd "$INSTALL_DIR/windows" && zip -q -r "$INSTALL_DIR/sshmanager-windows.zip" .)
tar -czf "$INSTALL_DIR/sshmanager-linux.tar.gz" -C "$INSTALL_DIR/linux" .

echo
echo "Instalador generado en: $INSTALL_DIR"
ls -lh "$INSTALL_DIR/sshmanager-windows.zip" "$INSTALL_DIR/sshmanager-linux.tar.gz"
