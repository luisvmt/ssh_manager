# Para linux

1. Crear una carpeta donde descomprimir el instalador
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

config = ./config       <-- dejar como esta
identity = ./id_ed25519     <-- la ruta es relativa al directorio donde esta instalado el aplicativo

7. Inscribir el directorio del aplicativo en el PATH del sistema operativo

8. Ejecutar el aplicativo
sshmanager

7. Ejecutar el instalador
8. Ejecutar el instalador
9. Ejecutar el instalador
10. Ejecutar el instalador
11. Ejecutar el instalador
Descomprimir el i