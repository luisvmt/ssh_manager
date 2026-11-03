
# Para linux

1. Crear una carpeta donde descomprimir el instalador
mkdir sshmanager
cd sshmanager

2. Descomprimir el instalador
tar -xvzf sshmanager-linux.tar.gz

3. Ingresar a la carpeta descomprimida
cd sshmanager

4. Ver las opciones del programa
./sshmanager --help

5. (Recomendado) Crear par de claves publica/privada
./sshmanager --keygen

6. Configurar rutas.
Tener en cuenta que el aplicativo utiliza el archivo parameters para saber que archivo config y que claves publica/privada utilizar
Si creaste las claves publicas/privadas del punto anterior, configurar estas claves en el archivo parameters

    Configuracion recomendada:
    --------------------------
    config = ./config       <-- dejar como esta
    identity = ./id_ed25519     <-- Si creaste segun punto 5. La ruta es relativa al directorio donde esta el archivo parameters.conf
                                    Dejar en blanco si no creaste claves publica/privada y quieres usar del sistema

7. Inscribir el directorio del aplicativo en el PATH del sistema operativo

8. Ejecutar el aplicativo
./sshmanager

# Opcionalmente
Puedes crear scripts en bash para iniciar el aplicativo con varios archivos parametros

    script 01
    ---------
    #!/usr/bin/bash
    <ruta>/sshmanager --parameters <ruta>/parameters01.conf

    script 02
    ---------
    #!/usr/bin/bash
    <ruta>/sshmanager --parameters <ruta>/parameters02.conf
