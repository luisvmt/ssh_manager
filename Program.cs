using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SshManager;

class Program
{
    static string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ssh", "config");

    // Programas externos y colores de resaltado configurables en "parameters.conf".
    static Parameters _parameters = new();

    // Estado global de la TUI.
    static string _filter = "";
    static string? _selectedIdentity = null;

    static int Main(string[] args)
    {
        string? keygenPath = null;
        string? execHost = null;
        string? execCommand = null;
        string? configArg = null;
        string? backupDir = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h":
                case "--help":
                    PrintHelp();
                    return 0;
                case "--config":
                    if (i + 1 < args.Length) configArg = args[++i];
                    break;
                case "--identity":
                    if (i + 1 < args.Length) _selectedIdentity = args[++i];
                    break;
                case "--keygen":
                    keygenPath = (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        ? args[++i] : "id_ed25519";
                    break;
                case "--exec":
                    if (i + 1 < args.Length) execHost = args[++i];
                    if (i + 1 < args.Length)
                    {
                        execCommand = string.Join(" ", args.Skip(++i));
                        i = args.Length;
                    }
                    break;
                case "--backup":
                    backupDir = (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        ? args[++i] : ".";
                    break;
                default:
                    if (!args[i].StartsWith("-")) configArg ??= args[i];
                    break;
            }
        }

        // El archivo "parameters.conf" define los programas externos (ssh, ssh-keygen...)
        // y los colores de resaltado. Sin él no se continúa la ejecución.
        var parameters = LoadParameters();
        if (parameters == null) return 1;
        _parameters = parameters;

        // Modo CLI: --keygen y --exec son acciones de un solo uso.
        if (keygenPath != null) return GenerateKeyPair(keygenPath);
        if (execHost != null && execCommand != null)
            return ExecuteOnExternalHost(execHost, execCommand, _selectedIdentity);
        if (execHost != null || execCommand != null)
        {
            Console.Error.WriteLine("Error: --exec requiere <user@host> y <comando>.");
            return 1;
        }

        ConfigPath = ResolveConfigPath(configArg);

        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Modo CLI: --backup genera el archivo y termina.
        if (backupDir != null) return CreateBackup(backupDir);

        Console.CancelKeyPress += (_, e) => e.Cancel = true;
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        // La TUI necesita una consola interactiva: Console.ReadKey lanza
        // InvalidOperationException si la entrada está redirigida (por ejemplo,
        // con "console": "internalConsole" en .vscode/launch.json).
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Error: la TUI requiere una consola interactiva (entrada no redirigida).");
            Console.Error.WriteLine("En VS Code usa \"console\": \"integratedTerminal\" en .vscode/launch.json,");
            Console.Error.WriteLine("o ejecuta el programa desde una terminal: dotnet run");
            return 1;
        }

        // Modo TUI interactivo.
        while (true)
        {
            var config = SshConfig.Load(ConfigPath);

            if (config.Hosts.Count == 0)
            {
                Console.Clear();
                int w = BoxWidth();
                BoxTop("SSH Manager", w);
                BoxLine(HeaderField("Config", ConfigPath), w);
                BoxLine(HeaderField("Clave privada", _selectedIdentity ?? "(por defecto de ssh)"), w);
                BoxSshWarning(w);
                BoxLine("", w);
                BoxLine("No hay hosts configurados.", w);
                BoxBottom(w);
                BoxTop("Opciones", w);
                BoxGrid(new[] { "a Añadir", "k Clave privada", "g Generar clave", "b Backup", "q Salir" }, w, 4);
                BoxBottom(w);

                var k = Console.ReadKey(true);
                if (k.Key == ConsoleKey.A) { if (AddHost(config)) config.Save(ConfigPath); }
                else if (char.ToLower(k.KeyChar) == 'k') _selectedIdentity = PromptIdentityFile(null);
                else if (char.ToLower(k.KeyChar) == 'g') GenerateKeyPairInteractive();
                else if (char.ToLower(k.KeyChar) == 'b') CreateBackup(".", pause: true);
                else if (char.ToLower(k.KeyChar) == 'q') return 0;
                continue;
            }

            // Filtro por nombre, HostName (IP/dominio) o tags; orden alfabético.
            var visible = config.Hosts
                .Where(h => MatchesFilter(h, _filter))
                .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var (action, index) = ShowMenu(visible);
            var sel = (index >= 0 && index < visible.Count) ? visible[index] : null;

            switch (action)
            {
                case MenuAction.Connect:
                    if (sel != null) Connect(sel, _selectedIdentity);
                    break;
                case MenuAction.ExecCommand:
                    if (sel != null) ExecCommandOnHost(sel, _selectedIdentity);
                    break;
                case MenuAction.CopyKey:
                    if (sel != null) CopyPublicKeyToHost(sel, _selectedIdentity);
                    break;
                case MenuAction.SelectKey:
                    _selectedIdentity = PromptIdentityFile(sel);
                    break;
                case MenuAction.GenerateKey:
                    GenerateKeyPairInteractive();
                    break;
                case MenuAction.Add:
                    if (AddHost(config)) { config.Save(ConfigPath); _filter = ""; }
                    break;
                case MenuAction.Edit:
                    if (sel != null && EditHost(sel)) config.Save(ConfigPath);
                    break;
                case MenuAction.Delete:
                    if (sel != null && ConfirmDelete(sel))
                    {
                        config.Hosts.Remove(sel);
                        config.Save(ConfigPath);
                    }
                    break;
                case MenuAction.Filter:
                    // Sin Console.Clear(): la lista actual permanece visible mientras
                    // se escribe el filtro; solo al pulsar Enter se redibuja filtrada.
                    Console.WriteLine();
                    Console.Write("Filtro (alias, IP o tag; vacío para limpiar): ");
                    _filter = (Console.ReadLine() ?? "").Trim();
                    break;
                case MenuAction.Backup:
                    CreateBackup(".", pause: true);
                    break;
                case MenuAction.Quit:
                    return 0;
            }
        }
    }

    // ---------- CLI ----------

    // Prioridad del archivo de configuración:
    //   1) el indicado por parámetro (--config o argumento posicional),
    //   2) el archivo "config" del directorio actual,
    //   3) ~/.ssh/config.
    static string ResolveConfigPath(string? fromArg)
    {
        if (!string.IsNullOrWhiteSpace(fromArg)) return Path.GetFullPath(fromArg);

        var localConfig = Path.Combine(Directory.GetCurrentDirectory(), "config");
        if (File.Exists(localConfig)) return localConfig;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ssh", "config");
    }

    // Etiqueta + valor con la columna de valores alineada en la cabecera.
    static string HeaderField(string label, string value) =>
        (label + ":").PadRight(14) + " " + value;

    // Carga "parameters.conf" del directorio actual. Si no existe, avisa y ofrece
    // crear una plantilla (No por defecto). Devuelve null si no se puede continuar.
    static Parameters? LoadParameters()
    {
        var path = Parameters.DefaultPath;

        if (!File.Exists(path))
        {
            Console.WriteLine();
            Console.WriteLine($"Aviso: no se encontró el archivo de parámetros: {path}");
            Console.WriteLine("Ese archivo define los programas externos que usa SSH Manager");
            Console.WriteLine("(ssh, ssh-keygen y ssh-copy-id) y los colores de resaltado.");

            if (!ConfirmCreateParameters())
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Error: sin el archivo 'parameters.conf' no se puede continuar.");
                return null;
            }

            try
            {
                Parameters.WriteTemplate(path);
                Console.WriteLine($"✓ Archivo creado: {path}");
                Console.WriteLine("  Edítalo si necesitas usar otra ruta o nombre de ejecutable.");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error al crear '{path}': {ex.Message}");
                return null;
            }
        }

        try
        {
            return Parameters.Load(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al leer '{path}': {ex.Message}");
            return null;
        }
    }

    // Confirmación explícita: vacío, Enter, cualquier tecla distinta de 's' = No.
    static bool ConfirmCreateParameters()
    {
        // Sin consola interactiva no se puede preguntar: se asume "no".
        if (Console.IsInputRedirected) return false;
        Console.Write("¿Crear un archivo 'parameters.conf' de modelo? (s/N): ");
        var k = Console.ReadKey(true);
        Console.WriteLine();
        return char.ToLower(k.KeyChar) == 's';
    }

    static void PrintHelp()
    {
        Console.WriteLine("SSH Manager - TUI para gestionar conexiones SSH");
        Console.WriteLine();
        Console.WriteLine("Uso:");
        Console.WriteLine("  ssh-manager [opciones] [archivo-config]");
        Console.WriteLine();
        Console.WriteLine("Opciones:");
        Console.WriteLine("  -h, --help                  Muestra esta ayuda");
        Console.WriteLine("  --config <ruta>             Archivo de configuración (por defecto: ./config o ~/.ssh/config)");
        Console.WriteLine("  --identity <ruta>           Clave privada a usar (afecta a --exec y a la TUI)");
        Console.WriteLine("  --keygen [nombre]           Crea un par de claves ed25519 en el directorio actual");
        Console.WriteLine("  --exec <user@host> <cmd>    Ejecuta un comando en un servidor externo");
        Console.WriteLine("  --backup [directorio]       Crea un backup (.tar.gz) de claves y config; por defecto en '.'");
        Console.WriteLine();
        Console.WriteLine("Modo TUI:");
        Console.WriteLine("  ↑/↓          Navegar (la lista se desplaza si hay más servidores)");
        Console.WriteLine("  Enter        Conectar");
        Console.WriteLine("  x            Ejecutar comando en el servidor seleccionado");
        Console.WriteLine("  c            Copiar clave pública al servidor seleccionado");
        Console.WriteLine("  k            Seleccionar clave privada para las conexiones");
        Console.WriteLine("  g            Generar par de claves (pública/privada) en el directorio actual");
        Console.WriteLine("  /            Filtrar servidores por texto");
        Console.WriteLine("  a / e / d    Añadir / editar / eliminar host");
        Console.WriteLine("  b            Crear backup de claves y configuración");
        Console.WriteLine("  q            Salir");
        Console.WriteLine();
        Console.WriteLine("Archivo de parámetros:");
        Console.WriteLine("  'parameters.conf' (en el directorio actual) define los programas");
        Console.WriteLine("  externos, el máximo de servidores en pantalla y los colores de la lista.");
        Console.WriteLine("  Si no existe, al arrancar se avisa y se ofrece crear una plantilla");
        Console.WriteLine("  (respuesta por defecto: no); sin ese archivo el programa no continúa.");
        Console.WriteLine();
        Console.WriteLine("  Formato (clave = valor; las claves ausentes usan el valor por defecto):");
        Console.WriteLine("    ssh                   = ssh");
        Console.WriteLine("    ssh-keygen            = ssh-keygen");
        Console.WriteLine("    ssh-copy-id           = ssh-copy-id");
        Console.WriteLine("    max-servers           = 15       (servidores visibles en pantalla)");
        Console.WriteLine("    highlight-foreground  = Yellow   (texto de la fila resaltada)");
        Console.WriteLine("    highlight-background  = Blue     (fondo de la fila resaltada)");
        Console.WriteLine();
        Console.WriteLine("  Colores válidos (ConsoleColor): Black, DarkBlue, DarkGreen, DarkCyan,");
        Console.WriteLine("    DarkRed, DarkMagenta, DarkYellow, Gray, DarkGray, Blue, Green, Cyan,");
        Console.WriteLine("    Red, Magenta, Yellow, White.");
        Console.WriteLine();
        Console.WriteLine("Requisitos en Windows:");
        Console.WriteLine("  - Cliente OpenSSH (ssh.exe y ssh-keygen.exe) en el PATH:");
        Console.WriteLine("    Configuración > Aplicaciones > Características opcionales > OpenSSH Client");
        Console.WriteLine("  - Windows Terminal o una fuente Unicode (Consolas/Cascadia) para dibujar las cajas");
        Console.WriteLine("  - 'c' (copiar clave) usa un método alternativo que requiere un servidor remoto Unix/macOS");
        Console.WriteLine();
        Console.WriteLine("Publicación para Windows:");
        Console.WriteLine("  dotnet publish sshmanager.csproj -c Release -r win-x64 --self-contained true \\");
        Console.WriteLine("    -p:PublishSingleFile=true -o publish-win");
    }

    // Opción de la TUI: crea el par de claves en el directorio actual.
    static void GenerateKeyPairInteractive()
    {
        Console.Clear();
        Console.WriteLine("=== Generar par de claves (pública/privada) ===");
        Console.WriteLine($"Directorio actual: {Directory.GetCurrentDirectory()}");
        Console.WriteLine();
        Console.Write("Nombre del archivo [id_ed25519]: ");
        var name = (Console.ReadLine() ?? "").Trim();
        if (string.IsNullOrEmpty(name)) name = "id_ed25519";

        Console.WriteLine();
        GenerateKeyPair(name);

        Console.WriteLine();
        Console.WriteLine("Presiona una tecla para volver...");
        Console.ReadKey(true);
    }

    static int GenerateKeyPair(string keyPath)
    {
        var fullPath = Path.GetFullPath(keyPath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        Console.WriteLine($"Generando par de claves ed25519 en: {fullPath}");
        Console.WriteLine("(ssh-keygen pedirá passphrase; pulsa Enter para dejar vacía)");
        Console.WriteLine();

        try
        {
            var psi = new ProcessStartInfo { FileName = _parameters.SshKeygen, UseShellExecute = false };
            psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add("ed25519");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(fullPath);

            using var proc = Process.Start(psi);
            proc?.WaitForExit();

            if (proc?.ExitCode == 0)
            {
                Console.WriteLine();
                Console.WriteLine($"✓ Clave privada: {fullPath}");
                Console.WriteLine($"✓ Clave pública: {fullPath}.pub");
                return 0;
            }
            return proc?.ExitCode ?? 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine("Asegúrate de tener ssh-keygen instalado (OpenSSH).");
            return 1;
        }
    }

    static int ExecuteOnExternalHost(string host, string command, string? identity)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = _parameters.Ssh, UseShellExecute = false };
            if (identity != null) { psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(identity); }
            psi.ArgumentList.Add(host);
            psi.ArgumentList.Add(command);
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
            return proc?.ExitCode ?? 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al ejecutar ssh: {ex.Message}");
            return 1;
        }
    }

    // ---------- BACKUP ----------

    static int CreateBackup(string destinationDir, bool pause = false)
    {
        if (pause) Console.Clear();
        Console.WriteLine("=== Backup de claves y configuración ===");
        Console.WriteLine();

        var sshDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

        var entries = new List<(string name, string path)>();

        // Claves privadas de ~/.ssh (y su .pub correspondiente).
        foreach (var key in FindPrivateKeys())
        {
            entries.Add((Path.GetFileName(key), key));
            var pub = key + ".pub";
            if (File.Exists(pub)) entries.Add((Path.GetFileName(pub), pub));
        }

        // Claves públicas sueltas que no tengan privada asociada.
        if (Directory.Exists(sshDir))
        {
            foreach (var pub in Directory.GetFiles(sshDir, "id_*.pub"))
            {
                var pubName = Path.GetFileName(pub);
                if (!entries.Any(e => e.name.Equals(pubName, StringComparison.OrdinalIgnoreCase)))
                    entries.Add((pubName, pub));
            }
        }

        // Configuración activa (puede estar fuera de ~/.ssh).
        if (File.Exists(ConfigPath))
        {
            var cfgName = "config";
            if (entries.Any(e => e.name.Equals(cfgName, StringComparison.OrdinalIgnoreCase)))
                cfgName = Path.GetFileName(ConfigPath);
            entries.Add((cfgName, ConfigPath));
        }

        if (entries.Count == 0)
        {
            Console.WriteLine("No se encontraron claves ni configuración para respaldar.");
            if (pause) { Console.WriteLine("\nPresiona una tecla..."); Console.ReadKey(true); }
            return 1;
        }

        try
        {
            Directory.CreateDirectory(destinationDir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var baseName = $"ssh-backup-{stamp}";
            var filePath = Path.GetFullPath(Path.Combine(destinationDir, baseName + ".tar.gz"));
            for (int n = 2; File.Exists(filePath); n++)
                filePath = Path.GetFullPath(Path.Combine(destinationDir, $"{baseName}-{n}.tar.gz"));

            using (var fs = File.Create(filePath))
            using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
            using (var tar = new TarWriter(gz, TarEntryFormat.Pax))
            {
                foreach (var (entryName, path) in entries)
                {
                    using var input = File.OpenRead(path);
                    var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
                    {
                        DataStream = input,
                        ModificationTime = File.GetLastWriteTimeUtc(path)
                    };
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        try { entry.Mode = File.GetUnixFileMode(path); } catch { }
                    }
                    tar.WriteEntry(entry);
                }
            }

            Console.WriteLine($"✓ Backup creado: {filePath}");
            Console.WriteLine($"  Archivos incluidos: {entries.Count}");
            foreach (var (entryName, _) in entries) Console.WriteLine($"    - {entryName}");
            if (pause) { Console.WriteLine("\nPresiona una tecla para volver..."); Console.ReadKey(true); }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al crear el backup: {ex.Message}");
            if (pause) { Console.WriteLine("\nPresiona una tecla para volver..."); Console.ReadKey(true); }
            return 1;
        }
    }

    // ---------- TUI ----------

    enum MenuAction { Connect, ExecCommand, CopyKey, SelectKey, GenerateKey, Add, Edit, Delete, Filter, Backup, Quit }

    // ---------- Dibujo de cajas ----------

    static int BoxWidth()
    {
        int w;
        try { w = Console.WindowWidth - 1; } catch { w = 79; }
        if (w < 24) w = 24;
        if (w > 100) w = 100;
        return w;
    }

    static void BoxTop(string title, int width)
    {
        var head = "─ " + title + " ";
        var fill = new string('─', Math.Max(0, width - 2 - head.Length));
        Console.WriteLine("┌" + head + fill + "┐");
    }

    static void BoxSeparator(int width) =>
        Console.WriteLine("├" + new string('─', width - 2) + "┤");

    static void BoxBottom(int width) =>
        Console.WriteLine("└" + new string('─', width - 2) + "┘");

    static string FitLine(string text, int width)
    {
        var inner = width - 2;
        var s = " " + text;
        if (s.Length > inner - 1) s = s[..(inner - 1)];
        return s.PadRight(inner);
    }

    static void BoxLine(string text, int width) =>
        Console.WriteLine("│" + FitLine(text, width) + "│");

    static void BoxRow(string text, int width, bool selected)
    {
        Console.Write("│");
        var s = FitLine(text, width);
        if (selected)
        {
            // Colores de la fila resaltada, configurables en "parameters.conf".
            Console.BackgroundColor = _parameters.HighlightBackground;
            Console.ForegroundColor = _parameters.HighlightForeground;
        }
        else
        {
            // Los servidores no seleccionados se muestran en amarillo.
            Console.ForegroundColor = ConsoleColor.Yellow;
        }
        Console.Write(s);
        Console.ResetColor();
        Console.WriteLine("│");
    }

    // Reparte las opciones en una rejilla de columnas alineadas.
    static void BoxGrid(string[] items, int width, int columns)
    {
        int colWidth = Math.Max(2, (width - 2) / columns);
        for (int i = 0; i < items.Length; i += columns)
        {
            var line = new StringBuilder();
            for (int c = 0; c < columns && i + c < items.Length; c++)
            {
                var cell = items[i + c];
                if (cell.Length >= colWidth) cell = cell[..(colWidth - 1)];
                line.Append(cell.PadRight(colWidth));
            }
            BoxLine(line.ToString().TrimEnd(), width);
        }
    }

    // Lee el número de servidor pulsado (admite varios dígitos, con una
    // pequeña espera solo cuando el número aún puede crecer).
    static int ReadServerNumber(char first, int max, ref ConsoleKeyInfo? pending)
    {
        var digits = first.ToString();
        while (int.TryParse(digits, out int current) && current * 10 <= max)
        {
            bool extended = false;
            long deadline = Environment.TickCount64 + 400;
            while (Environment.TickCount64 < deadline)
            {
                if (Console.KeyAvailable)
                {
                    var next = Console.ReadKey(true);
                    if (char.IsDigit(next.KeyChar)
                        && int.TryParse(digits + next.KeyChar, out int candidate)
                        && candidate <= max)
                    {
                        digits += next.KeyChar;
                        extended = true;
                    }
                    else
                    {
                        pending = next;
                    }
                    break;
                }
                Thread.Sleep(15);
            }
            if (!extended) break;
        }
        return int.TryParse(digits, out int value) ? value : 0;
    }

    static (MenuAction, int) ShowMenu(List<HostBlock> hosts)
    {
        int sel = 0;
        // Primer servidor visible de la ventana de scrolling.
        int offset = 0;
        ConsoleKeyInfo? pending = null;

        while (true)
        {
            int width = BoxWidth();
            Console.Clear();

            // Marco superior: cabecera + lista numerada de servidores.
            BoxTop("SSH Manager", width);
            BoxLine(HeaderField("Config", ConfigPath), width);
            if (!string.IsNullOrEmpty(_filter))
                BoxLine(HeaderField("Filtro", $"\"{_filter}\" ({hosts.Count} resultado(s))"), width);
            BoxLine(HeaderField("Clave privada", _selectedIdentity ?? "(por defecto de ssh)"), width);
            BoxSshWarning(width);
            BoxSeparator(width);

            if (hosts.Count == 0)
            {
                BoxLine("(sin resultados con el filtro actual)", width);
            }
            else
            {
                if (sel >= hosts.Count) sel = hosts.Count - 1;
                if (sel < 0) sel = 0;

                // Ventana visible: máximo de servidores en pantalla definido en
                // "parameters.conf" (max-servers). Si la selección sale de la
                // ventana, se desplaza una fila ocultando la del extremo opuesto.
                int window = MaxVisibleRows(hosts.Count);
                if (sel < offset) offset = sel;
                if (sel >= offset + window) offset = sel - window + 1;
                if (offset > hosts.Count - window) offset = hosts.Count - window;
                if (offset < 0) offset = 0;
                int last = Math.Min(hosts.Count, offset + window);

                int numWidth = hosts.Count.ToString().Length;

                // Tabla: Servidor | IP/Dominio | Usuario | Tags | Puerto | ID.
                var header = new[] { "Servidor", "IP/Dominio", "Usuario", "Tags", "Puerto", "ID" };
                var rows = new List<string[]>(hosts.Count);
                for (int i = 0; i < hosts.Count; i++)
                    rows.Add(HostCells(hosts[i], i + 1, numWidth));

                const int gap = 2;
                // Los anchos se calculan con todas las filas para que las columnas
                // no cambien de tamaño al desplazarse.
                var widths = ColumnWidths(rows, header, width - 3, gap);

                BoxLine(JoinColumns(header, widths, gap), width);
                BoxSeparator(width);
                for (int i = offset; i < last; i++)
                    BoxRow(JoinColumns(rows[i], widths, gap), width, i == sel);
                if (hosts.Count > window)
                {
                    BoxSeparator(width);
                    BoxLine($"[{offset + 1}-{last} de {hosts.Count}] ↑/↓ desplazar", width);
                }
            }
            BoxBottom(width);

            // Marco inferior: opciones disponibles en columnas (4 por fila).
            BoxTop("Opciones", width);
            bool compact = (width - 2) / 4 < 15;
            BoxGrid(compact
                ? new[]
                {
                    "↑/↓ Nav",  "Enter",    "1-N Sel",  "x Cmd",
                    "c Copiar", "k Clave",  "g Generar", "b Backup",
                    "a Añadir", "e Editar", "d Borrar", "/ Filtro",
                    "q Salir"
                }
                : new[]
                {
                    "↑/↓ Navegar",     "Enter Conectar",  "1-N Seleccionar",  "x Ejecutar cmd",
                    "c Copiar clave",  "k Clave privada", "g Generar clave", "b Backup",
                    "a Añadir",        "e Editar",        "d Eliminar",       "/ Filtrar",
                    "q Salir"
                }, width, 4);
            BoxBottom(width);

            var k = pending ?? Console.ReadKey(true);
            pending = null;
            switch (k.Key)
            {
                case ConsoleKey.UpArrow:
                    if (hosts.Count > 0) sel = (sel - 1 + hosts.Count) % hosts.Count;
                    break;
                case ConsoleKey.DownArrow:
                    if (hosts.Count > 0) sel = (sel + 1) % hosts.Count;
                    break;
                case ConsoleKey.Enter:
                    if (hosts.Count > 0) return (MenuAction.Connect, sel);
                    break;
                default:
                    if (char.IsDigit(k.KeyChar) && hosts.Count > 0)
                    {
                        var num = ReadServerNumber(k.KeyChar, hosts.Count, ref pending);
                        if (num >= 1 && num <= hosts.Count) sel = num - 1;
                        break;
                    }
                    switch (char.ToLower(k.KeyChar))
                    {
                        case 'x': if (hosts.Count > 0) return (MenuAction.ExecCommand, sel); break;
                        case 'c': if (hosts.Count > 0) return (MenuAction.CopyKey, sel); break;
                        case 'k': return (MenuAction.SelectKey, hosts.Count > 0 ? sel : -1);
                        case 'g': return (MenuAction.GenerateKey, -1);
                        case 'a': return (MenuAction.Add, -1);
                        case 'e': if (hosts.Count > 0) return (MenuAction.Edit, sel); break;
                        case 'd': if (hosts.Count > 0) return (MenuAction.Delete, sel); break;
                        case 'b': return (MenuAction.Backup, -1);
                        case '/': return (MenuAction.Filter, -1);
                        case 'q': return (MenuAction.Quit, -1);
                    }
                    break;
            }
        }
    }

    // Número máximo de filas de servidores visibles a la vez: se toma de
    // "max-servers" (parameters.conf) y se recorta si la ventana es más baja.
    static int MaxVisibleRows(int hostCount)
    {
        int rows = Math.Min(Math.Max(1, _parameters.MaxServers), hostCount);
        try
        {
            // Alto que ocupan los marcos (cabecera, tabla, separador, indicador y opciones).
            int available = Console.WindowHeight - 18;
            if (available > 0 && rows > available) rows = available;
        }
        catch { }
        return Math.Max(1, rows);
    }

    // Celdas de una fila: alias numerado, IP, usuario, tags, puerto e identidad.
    static string[] HostCells(HostBlock h, int number, int numberWidth)
    {
        var id = h.GetOption("IdentityFile");
        return new[]
        {
            number.ToString().PadLeft(numberWidth) + ") " + h.Name,
            h.GetOption("HostName") ?? "-",
            h.GetOption("User") ?? "-",
            string.IsNullOrWhiteSpace(h.Tags) ? "-" : h.Tags,
            h.GetOption("Port") ?? "-",
            id != null ? Path.GetFileName(id) : "-"
        };
    }

    // Anchos de columna ajustados al contenido y al espacio disponible.
    static int[] ColumnWidths(List<string[]> rows, string[] header, int available, int gap)
    {
        int cols = header.Length;
        var widths = new int[cols];
        for (int c = 0; c < cols; c++) widths[c] = header[c].Length;
        foreach (var row in rows)
            for (int c = 0; c < cols; c++)
                if (row[c].Length > widths[c]) widths[c] = row[c].Length;

        int TotalWidth() => widths.Sum() + gap * (cols - 1);
        while (TotalWidth() > available)
        {
            // Reduce siempre la columna más ancha para repartir el recorte.
            int widest = 0;
            for (int c = 1; c < cols; c++)
                if (widths[c] > widths[widest]) widest = c;
            if (widths[widest] <= 3) break;
            widths[widest]--;
        }
        return widths;
    }

    static string JoinColumns(string[] cells, int[] widths, int gap)
    {
        var sb = new StringBuilder();
        for (int c = 0; c < cells.Length; c++)
        {
            if (c > 0) sb.Append(' ', gap);
            var cell = cells[c];
            if (cell.Length > widths[c]) cell = cell[..widths[c]];
            sb.Append(cell.PadRight(widths[c]));
        }
        return sb.ToString().TrimEnd();
    }

    // Coincidencia del filtro con el alias, el HostName (IP/dominio) o los tags.
    static bool MatchesFilter(HostBlock h, string filter)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        return h.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (h.GetOption("HostName")?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
            || h.Tags.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    static void Connect(HostBlock host, string? identity)
    {
        Console.Clear();
        Console.WriteLine($"Conectando a {host.Name}...");
        Console.WriteLine();

        try
        {
            var psi = new ProcessStartInfo { FileName = _parameters.Ssh, UseShellExecute = false };
            if (identity != null) { psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(identity); }
            psi.ArgumentList.Add(host.Name);
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al iniciar ssh: {ex.Message}");
            Console.WriteLine("Asegúrate de tener OpenSSH instalado y en el PATH.");
        }

        Console.WriteLine();
        Console.WriteLine("Sesión finalizada. Presiona una tecla para volver al menú...");
        Console.ReadKey(true);
    }

    static void ExecCommandOnHost(HostBlock host, string? identity)
    {
        Console.Clear();
        Console.WriteLine($"=== Ejecutar comando en {host.Name} ===");
        Console.Write("Comando: ");
        var cmd = (Console.ReadLine() ?? "").Trim();
        if (string.IsNullOrEmpty(cmd)) return;

        Console.WriteLine();
        try
        {
            var psi = new ProcessStartInfo { FileName = _parameters.Ssh, UseShellExecute = false };
            if (identity != null) { psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(identity); }
            psi.ArgumentList.Add(host.Name);
            psi.ArgumentList.Add(cmd);
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
        }
        catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }

        Console.WriteLine();
        Console.WriteLine("Presiona una tecla para volver...");
        Console.ReadKey(true);
    }

    static void CopyPublicKeyToHost(HostBlock host, string? identity)
    {
        var pubKey = FindPublicKey(identity);
        if (pubKey == null)
        {
            Console.Clear();
            Console.WriteLine("No se encontró ninguna clave pública (.pub) en ~/.ssh/.");
            Console.WriteLine("Genera una con la opción --keygen o con ssh-keygen.");
            Console.WriteLine("\nPresiona una tecla...");
            Console.ReadKey(true);
            return;
        }

        Console.Clear();
        Console.WriteLine($"=== Copiar clave pública a {host.Name} ===");
        Console.WriteLine($"Clave: {pubKey}");
        Console.Write("\n¿Continuar? (s/N): ");
        var k = Console.ReadKey(true); Console.WriteLine();
        if (k.Key != ConsoleKey.S) return;

        try
        {
            // ssh-copy-id es un script de shell: en Windows (o con Git Bash en el
            // PATH) no se puede ejecutar con UseShellExecute=false, así que se usa
            // siempre el método alternativo.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && CommandExists(_parameters.SshCopyId))
            {
                var psi = new ProcessStartInfo { FileName = _parameters.SshCopyId, UseShellExecute = false };
                if (identity != null) { psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(identity); }
                else { psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(pubKey); }
                psi.ArgumentList.Add(host.Name);
                using var proc = Process.Start(psi);
                proc?.WaitForExit();
            }
            else
            {
                // Fallback portable: envía la clave embebida en base64 y la añade al authorized_keys.
                // Requiere un servidor remoto tipo Unix/macOS (usa base64 -d y chmod).
                Console.WriteLine("Usando método alternativo (requiere servidor remoto Unix/macOS)...");
                var pubContent = File.ReadAllText(pubKey).Trim();
                var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(pubContent));
                var remoteCmd =
                    "mkdir -p ~/.ssh && chmod 700 ~/.ssh && " +
                    $"(echo '{b64}' | base64 -d; echo) >> ~/.ssh/authorized_keys && " +
                    "chmod 600 ~/.ssh/authorized_keys";

                var psi = new ProcessStartInfo { FileName = _parameters.Ssh, UseShellExecute = false };
                if (identity != null) { psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(identity); }
                psi.ArgumentList.Add(host.Name);
                psi.ArgumentList.Add(remoteCmd);
                using var proc = Process.Start(psi);
                proc?.WaitForExit();
            }
        }
        catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }

        Console.WriteLine("\nPresiona una tecla para volver...");
        Console.ReadKey(true);
    }

    static string? PromptIdentityFile(HostBlock? host)
    {
        var existing = host?.GetOption("IdentityFile");
        var keys = FindPrivateKeys();

        Console.Clear();
        Console.WriteLine("=== Seleccionar clave privada ===");
        Console.WriteLine();

        var options = new List<(string label, string? path)>();
        if (!string.IsNullOrEmpty(existing))
            options.Add(($"(config del host) {existing}", existing));
        foreach (var k in keys)
            options.Add((k, k));
        options.Add(("(ninguna / usar por defecto de ssh)", null));
        options.Add(("(ruta personalizada...)", "__custom__"));

        for (int i = 0; i < options.Count; i++)
            Console.WriteLine($"  {i + 1}) {options[i].label}");

        Console.Write("\nSelecciona una opción (número): ");
        if (!int.TryParse(Console.ReadLine(), out int n) || n < 1 || n > options.Count)
            return _selectedIdentity;

        var path = options[n - 1].path;
        if (path == "__custom__")
        {
            Console.Write("Ruta a la clave privada: ");
            var custom = (Console.ReadLine() ?? "").Trim();
            return string.IsNullOrEmpty(custom) ? _selectedIdentity : custom;
        }
        return path;
    }

    static List<string> FindPrivateKeys()
    {
        var sshDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        if (!Directory.Exists(sshDir)) return new List<string>();

        var candidates = Directory.GetFiles(sshDir)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n)
                        && n!.StartsWith("id_")
                        && !n.EndsWith(".pub", StringComparison.OrdinalIgnoreCase)
                        && !n.EndsWith(".old", StringComparison.OrdinalIgnoreCase))
            .Select(n => n!)
            .ToList();

        // Orden de preferencia: ed25519 → ecdsa → rsa → resto.
        static int Rank(string n)
        {
            if (n.Contains("ed25519", StringComparison.OrdinalIgnoreCase)) return 0;
            if (n.Contains("ecdsa",   StringComparison.OrdinalIgnoreCase)) return 1;
            if (n.Contains("rsa",     StringComparison.OrdinalIgnoreCase)) return 2;
            return 3;
        }

        return candidates
            .OrderBy(Rank)
            .ThenBy(n => n)
            .Select(n => Path.Combine(sshDir, n))
            .ToList();
    }

    static string? FindPublicKey(string? privateKeyPath)
    {
        if (!string.IsNullOrEmpty(privateKeyPath))
        {
            var pub = privateKeyPath.EndsWith(".pub", StringComparison.OrdinalIgnoreCase)
                ? privateKeyPath
                : privateKeyPath + ".pub";
            if (File.Exists(pub)) return pub;
        }

        var sshDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        foreach (var c in new[] { "id_ed25519.pub", "id_ecdsa.pub", "id_rsa.pub", "id_dsa.pub" })
        {
            var p = Path.Combine(sshDir, c);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    static bool CommandExists(string cmd)
    {
        try
        {
            var isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var psi = new ProcessStartInfo
            {
                FileName = isWin ? "where.exe" : "which",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add(cmd);
            using var p = Process.Start(psi);
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    // Evita repetir la búsqueda de ssh en cada redibujado de la TUI.
    static bool? _sshAvailable;

    static bool SshAvailable()
    {
        if (_sshAvailable == null) _sshAvailable = CommandExists(_parameters.Ssh);
        return _sshAvailable.Value;
    }

    // Aviso visible en la cabecera si no hay cliente OpenSSH en el PATH.
    static void BoxSshWarning(int width)
    {
        if (SshAvailable()) return;
        var msg = width >= 58
            ? "Aviso: falta 'ssh' en el PATH (instala OpenSSH Client)."
            : "Aviso: falta 'ssh' en el PATH.";
        BoxLine(msg, width);
    }

    static bool AddHost(SshConfig config)
    {
        Console.Clear();
        Console.WriteLine("=== Añadir host ===");
        Console.Write("Alias (obligatorio): ");
        var name = (Console.ReadLine() ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return false;
        if (config.Hosts.Any(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"Ya existe '{name}'. Presiona una tecla...");
            Console.ReadKey(true); return false;
        }
        Console.Write("HostName (IP/dominio, opcional): "); var hn = (Console.ReadLine() ?? "").Trim();
        Console.Write("User (opcional): ");               var u  = (Console.ReadLine() ?? "").Trim();
        var p = PromptPort("Port (opcional)");
        Console.Write("Tags (opcional, ej: production, web): "); var tags = NormalizeTags(Console.ReadLine());
        Console.Write($"IdentityFile (opcional, sugerida: {_selectedIdentity ?? "ninguna"}): ");
        var id = (Console.ReadLine() ?? "").Trim();
        if (string.IsNullOrEmpty(id) && _selectedIdentity != null) id = _selectedIdentity;

        var host = new HostBlock { Name = name, RawHostLine = "Host " + name, Tags = tags };
        if (!string.IsNullOrEmpty(hn)) host.SetOption("HostName", hn);
        if (!string.IsNullOrEmpty(u))  host.SetOption("User", u);
        if (!string.IsNullOrEmpty(p) && p != "22") host.SetOption("Port", p);
        if (!string.IsNullOrEmpty(id)) host.SetOption("IdentityFile", id);

        if (!ConfirmCreate(host))
        {
            Console.WriteLine("\nCreación cancelada. Presiona una tecla...");
            Console.ReadKey(true);
            return false;
        }

        config.AddNewHost(host);
        Console.WriteLine("\nHost añadido. Presiona una tecla..."); Console.ReadKey(true);
        return true;
    }

    // Fila del resumen con etiqueta alineada.
    static void SummaryField(string label, string? value, string emptyText = "(no definido)") =>
        Console.WriteLine($"  {(label + ":").PadRight(14)} {(string.IsNullOrWhiteSpace(value) ? emptyText : value)}");

    // Fila del resumen que añade "(antes: ...)" cuando el valor cambia.
    static void SummaryChange(string label, string? value, string? previous, string emptyText = "(no definido)")
    {
        var text = $"  {(label + ":").PadRight(14)} {(string.IsNullOrWhiteSpace(value) ? emptyText : value)}";
        if (!string.Equals(previous, value, StringComparison.Ordinal))
            text += $"   (antes: {(string.IsNullOrWhiteSpace(previous) ? emptyText : previous)})";
        Console.WriteLine(text);
    }

    static bool ConfirmCreate(HostBlock host)
    {
        Console.Clear();
        Console.WriteLine("=== Resumen del nuevo servidor ===");
        Console.WriteLine();
        SummaryField("Alias", host.Name);
        SummaryField("HostName", host.GetOption("HostName"));
        SummaryField("User", host.GetOption("User"));
        SummaryField("Port", host.GetOption("Port") ?? "22 (por defecto)");
        SummaryField("Tags", host.Tags, "(ninguno)");
        SummaryField("IdentityFile", host.GetOption("IdentityFile"), "(por defecto de ssh)");
        Console.WriteLine();
        Console.Write("¿Crear el servidor con estas opciones? (S/n): ");
        var k = Console.ReadKey(true);
        Console.WriteLine();
        return k.Key == ConsoleKey.Enter || char.ToLower(k.KeyChar) == 's';
    }

    // Devuelve true solo si el usuario confirma; hasta entonces el host no se modifica.
    static bool EditHost(HostBlock host)
    {
        Console.Clear();
        Console.WriteLine($"=== Editar: {host.Name} ===");
        Console.WriteLine("Deja en blanco para mantener el valor actual.\n");

        var newHostName = Prompt("HostName", host.GetOption("HostName"));
        var newUser     = Prompt("User", host.GetOption("User"));
        var newPort     = PromptPort("Port", host.GetOption("Port"));
        var newTags     = Prompt("Tags", string.IsNullOrWhiteSpace(host.Tags) ? null : host.Tags);
        var newId       = Prompt("IdentityFile", host.GetOption("IdentityFile"));

        if (!ConfirmEdit(host, newHostName, newUser, newPort, newTags, newId))
        {
            Console.WriteLine("\nEdición cancelada. Presiona una tecla...");
            Console.ReadKey(true);
            return false;
        }

        if (newHostName != null) host.SetOption("HostName", newHostName);
        if (newUser != null)     host.SetOption("User", newUser);
        if (newPort != null)     host.SetOption("Port", newPort);
        if (newTags != null)     host.Tags = NormalizeTags(newTags);
        if (newId != null)       host.SetOption("IdentityFile", newId);

        Console.WriteLine("\nHost actualizado. Presiona una tecla..."); Console.ReadKey(true);
        return true;
    }

    static bool ConfirmEdit(HostBlock host, string? hostName, string? user, string? port,
                            string? tags, string? identity)
    {
        var oldPort = host.GetOption("Port") ?? "22 (por defecto)";
        Console.Clear();
        Console.WriteLine($"=== Resumen de los cambios: {host.Name} ===");
        Console.WriteLine();
        SummaryField("Alias", host.Name);
        SummaryChange("HostName", hostName ?? host.GetOption("HostName"), host.GetOption("HostName"));
        SummaryChange("User", user ?? host.GetOption("User"), host.GetOption("User"));
        SummaryChange("Port", port ?? oldPort, oldPort);
        SummaryChange("Tags", tags != null ? NormalizeTags(tags) : host.Tags, host.Tags, "(ninguno)");
        SummaryChange("IdentityFile", identity ?? host.GetOption("IdentityFile"),
                      host.GetOption("IdentityFile"), "(por defecto de ssh)");
        Console.WriteLine();
        Console.Write($"¿Seguro que deseas guardar los cambios en '{host.Name}'? (S/n): ");
        var k = Console.ReadKey(true);
        Console.WriteLine();
        return k.Key == ConsoleKey.Enter || char.ToLower(k.KeyChar) == 's';
    }

    static string? Prompt(string label, string? current)
    {
        Console.Write($"{label} [{current ?? ""}]: ");
        var s = (Console.ReadLine() ?? "").Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    // Puerto: entero mayor que cero o vacío (omitir / mantener el actual).
    static string? PromptPort(string label, string? current = null)
    {
        while (true)
        {
            Console.Write(current == null ? $"{label}: " : $"{label} [{current}]: ");
            var s = (Console.ReadLine() ?? "").Trim();
            if (s.Length == 0) return null;
            if (int.TryParse(s, out int value) && value > 0) return s;
            Console.WriteLine("  Puerto inválido: escribe un número entero mayor que cero o déjalo vacío.");
        }
    }

    // Normaliza la lista de etiquetas: separa por comas, quita '#' y duplicados.
    static string NormalizeTags(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var parts = input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.TrimStart('#').Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", parts);
    }

    static bool ConfirmDelete(HostBlock host)
    {
        Console.Clear();
        Console.Write($"¿Eliminar '{host.Name}'? (s/N): ");
        var k = Console.ReadKey(true); Console.WriteLine();
        return k.Key == ConsoleKey.S;
    }
}

class HostBlock
{
    public string RawHostLine = "";
    public string Name = "";
    public string Tags = "";   // Etiquetas libres; se guardan como "# Tags: ..." antes del Host.
    public List<ConfigLine> Body = new();

    public void AddBodyLine(string line) => Body.Add(ConfigLine.Parse(line));

    public string? GetOption(string key)
        => Body.FirstOrDefault(l => l.Key != null && l.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;

    public void SetOption(string key, string value)
    {
        foreach (var line in Body)
        {
            if (line.Key != null && line.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                var leading = line.Raw.Substring(0, line.Raw.Length - line.Raw.TrimStart().Length);
                if (leading.Length == 0) leading = "    ";
                line.Raw = leading + key + " " + value;
                line.Value = value;
                return;
            }
        }
        Body.Add(new ConfigLine { Raw = "    " + key + " " + value, Key = key, Value = value });
    }
}

class ConfigLine
{
    public string Raw = "";
    public string? Key;
    public string? Value;

    public static ConfigLine Parse(string raw)
    {
        var line = new ConfigLine { Raw = raw };
        var t = raw.TrimStart();
        if (t.Length == 0 || t.StartsWith("#")) return line;
        int sep = -1;
        for (int i = 0; i < t.Length; i++)
            if (t[i] == ' ' || t[i] == '\t' || t[i] == '=') { sep = i; break; }
        if (sep <= 0) { line.Key = t; line.Value = ""; return line; }
        line.Key = t[..sep];
        line.Value = t[(sep + 1)..].Trim();
        return line;
    }
}

class SshConfig
{
    public string Prelude = "";
    public List<HostBlock> Hosts = new();

    public static SshConfig Load(string path)
    {
        var cfg = new SshConfig();
        if (!File.Exists(path)) return cfg;
        var prelude = new StringBuilder();
        HostBlock? current = null;
        var pendingTagLines = new List<string>();
        string pendingTags = "";

        // Un comentario "# Tags:" situado antes de un Host pertenece a ese Host.
        // Si no le sigue un Host, se conserva como comentario normal.
        void FlushPendingTags()
        {
            foreach (var raw in pendingTagLines)
            {
                if (current == null) prelude.Append(raw).Append('\n');
                else current.AddBodyLine(raw);
            }
            pendingTagLines.Clear();
            pendingTags = "";
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (IsHostLine(line))
            {
                var after = line.TrimStart()[4..].Trim();
                var patterns = after.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                current = new HostBlock
                {
                    Name = patterns.Length > 0 ? patterns[0] : "unnamed",
                    RawHostLine = line,
                    Tags = pendingTags
                };
                pendingTagLines.Clear();
                pendingTags = "";
                cfg.Hosts.Add(current);
            }
            else if (TryParseTagsLine(line, out var tags))
            {
                FlushPendingTags();
                pendingTagLines.Add(line);
                pendingTags = tags;
            }
            else
            {
                FlushPendingTags();
                if (current == null) prelude.Append(line).Append('\n');
                else current.AddBodyLine(line);
            }
        }
        FlushPendingTags();
        cfg.Prelude = prelude.ToString();
        return cfg;
    }

    static bool IsHostLine(string line)
    {
        var t = line.TrimStart();
        if (t.Length < 4 || !t.StartsWith("Host", StringComparison.OrdinalIgnoreCase)) return false;
        return t.Length == 4 || t[4] == ' ' || t[4] == '\t';
    }

    // Reconoce "# Tags: production, web" (solo con valor no vacío).
    static bool TryParseTagsLine(string line, out string tags)
    {
        var t = line.Trim();
        tags = "";
        if (!t.StartsWith("# Tags:", StringComparison.OrdinalIgnoreCase)) return false;
        tags = t[7..].Trim();
        return tags.Length > 0;
    }

    public void AddNewHost(HostBlock host)
    {
        if (Hosts.Count > 0)
        {
            var last = Hosts[^1];
            if (last.Body.Count == 0 || !string.IsNullOrWhiteSpace(last.Body[^1].Raw))
                last.Body.Add(new ConfigLine { Raw = "" });
        }
        Hosts.Add(host);
    }

    public void Save(string path)
    {
        var sb = new StringBuilder();
        // Saltos de línea LF fijos: el config es portable entre Windows, Linux y macOS.
        sb.Append(Prelude.Replace("\r\n", "\n").Replace('\r', '\n'));
        foreach (var h in Hosts)
        {
            if (!string.IsNullOrWhiteSpace(h.Tags))
                sb.Append("# Tags: ").Append(h.Tags.Trim()).Append('\n');
            sb.Append(h.RawHostLine).Append('\n');
            foreach (var l in h.Body) sb.Append(l.Raw).Append('\n');
        }
        File.WriteAllText(path, sb.ToString());

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch { }
        }
    }
}

// Programas externos y colores que usa SSH Manager. Se leen del archivo
// "parameters.conf" del directorio actual, con formato "clave = valor" (una por línea).
class Parameters
{
    public const string FileName = "parameters.conf";

    public string Ssh = "ssh";
    public string SshKeygen = "ssh-keygen";
    public string SshCopyId = "ssh-copy-id";

    // Colores de la fila resaltada en la lista de servidores.
    public ConsoleColor HighlightForeground = ConsoleColor.Yellow;
    public ConsoleColor HighlightBackground = ConsoleColor.Blue;

    // Número máximo de servidores visibles a la vez en la pantalla principal.
    public int MaxServers = 15;

    public static string DefaultPath =>
        Path.Combine(Directory.GetCurrentDirectory(), FileName);

    // Las claves ausentes conservan el valor por defecto.
    public static Parameters Load(string path)
    {
        var p = new Parameters();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            int sep = line.IndexOf('=');
            if (sep <= 0) continue;

            var key = line[..sep].Trim().ToLowerInvariant();
            var value = line[(sep + 1)..].Trim();
            if (value.Length == 0) continue;

            switch (key)
            {
                case "ssh":                  p.Ssh = value; break;
                case "ssh-keygen":           p.SshKeygen = value; break;
                case "ssh-copy-id":          p.SshCopyId = value; break;
                case "highlight-foreground": p.HighlightForeground = ParseColor(value, p.HighlightForeground); break;
                case "highlight-background": p.HighlightBackground = ParseColor(value, p.HighlightBackground); break;
                case "max-servers":
                    // Solo valores positivos; si no es válido se conserva el valor por defecto.
                    if (int.TryParse(value, out int maxServers) && maxServers > 0) p.MaxServers = maxServers;
                    break;
            }
        }
        return p;
    }

    // Convierte un nombre de ConsoleColor (sin distinguir mayúsculas). Si el valor
    // no es un color válido se conserva el valor por defecto.
    static ConsoleColor ParseColor(string value, ConsoleColor fallback) =>
        Enum.TryParse<ConsoleColor>(value, ignoreCase: true, out var color)
        && Enum.IsDefined(color)
            ? color
            : fallback;

    // Plantilla de modelo que se ofrece crear cuando el archivo no existe.
    public static void WriteTemplate(string path)
    {
        var sb = new StringBuilder();
        sb.Append("# SSH Manager - parámetros\n");
        sb.Append("# Indica el nombre en el PATH o la ruta completa al ejecutable.\n");
        sb.Append("# Las claves ausentes usan el valor por defecto.\n");
        sb.Append('\n');
        sb.Append("ssh = ssh\n");
        sb.Append("ssh-keygen = ssh-keygen\n");
        sb.Append("ssh-copy-id = ssh-copy-id\n");
        sb.Append('\n');
        sb.Append("# Número máximo de servidores visibles en la pantalla principal.\n");
        sb.Append("# Si hay más, la lista se desplaza con ↑/↓.\n");
        sb.Append("max-servers = 15\n");
        sb.Append('\n');
        sb.Append("# Colores de la fila resaltada (nombres de ConsoleColor).\n");
        sb.Append("highlight-foreground = Yellow\n");
        sb.Append("highlight-background = Blue\n");
        File.WriteAllText(path, sb.ToString());
    }
}
