# A continuacion se muestra los comandos para compilar el proyecto en cada plataforma.

## Windows
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

## Linux
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true