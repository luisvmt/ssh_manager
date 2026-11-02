# Implementation Plan

## [Overview]
Hacer configurable el color de la línea de ayuda del selector de archivos (mensaje "ESPACIO: seleccionar…") y externalizar en `parameters.conf` todos los colores que el programa usa hoy, de modo que ninguno quede hardcodeado.

Contexto: la TUI (`Program.cs`, clase estática `Program`) ya permite configurar `highlight-foreground` y `highlight-background` para la fila resaltada y los lee en `Parameters.Load`. Sin embargo, la fila **no seleccionada** de la lista de servidores pinta en `ConsoleColor.Yellow` **hardcodeado** dentro de `BoxRow` (línea ~577). Además, la línea de teclas del selector (`PickPath`) se dibuja sin color con `BoxLine`. El objetivo es que la línea de ayuda "ESPACIO…" aparezca en otro color (rojo por defecto) y que ese color, junto con el de las filas no seleccionadas, sea configurable. La estrategia es añadir claves de color a `Parameters`, un helper de dibujo con color (`BoxLineColored`) y usar campos configurables en `BoxRow` y en `PickPath`. La coloración del texto se limita a *foreground* + *background opcional* (fondo "ninguno" = se hereda el del terminal) para no alterar la apariencia actual salvo lo pedido.

Supuestos adoptados (el usuario no fijó alternativa): (1) se externalizan **todos** los colores realmente usados, incluido el amarillo hoy hardcodeado de la lista; (2) la línea de ayuda del selector (mensaje de ESPACIO) se colorea por defecto en **rojo** (solo color de letra) y se permite además configurar un fondo opcional.

## [Types]
No se crean tipos nuevos ni se modifican `enum`. Sí se amplía el estado de configuración:

- **`Parameters`** (clase existente en `Program.cs`, ~línea 1991) — nuevos campos públicos:
  - `public ConsoleColor ListForeground = ConsoleColor.Yellow;` (filas no seleccionadas; reemplaza el `Yellow` hardcodeado).
  - `public ConsoleColor? ListBackground = null;` (fondo de filas no seleccionadas; `null` = sin fondo, hereda el del terminal).
  - `public ConsoleColor HintForeground = ConsoleColor.Red;` (color de la línea de ayuda/teclas del selector).
  - `public ConsoleColor? HintBackground = null;` (fondo de la línea de ayuda; `null` = sin fondo).

- Se mantiene el uso de `ConsoleColor` (enum del framework) y de `ConsoleColor?` para los fondos opcionales.

## [Files]
- **`/home/luisvmt/Developer/proyectos/proyecto_sshmanager/Program.cs`** (modificar):
  - Clase `Parameters`: nuevos campos (ver [Types]), nuevas ramas en `Load`, nuevo método `ParseColorOrNone`, y nuevas claves en `WriteTemplate`.
  - `BoxRow`: usar `_parameters.ListForeground` / `_parameters.ListBackground` en lugar del `ConsoleColor.Yellow` hardcodeado.
  - Nuevo helper `BoxLineColored` (ver [Functions]).
  - `PickPath`: dibujar la línea de teclas con `BoxLineColored(..., _parameters.HintForeground, _parameters.HintBackground)`.
  - `PrintHelp` (bloque "Formato…", ~líneas 324–341): documentar las nuevas claves de color y el valor especial "vacío/none" para fondos.
- **`/home/luisvmt/Developer/proyectos/proyecto_sshmanager/parameters.conf`** (modificar): añadir las nuevas claves con comentarios, junto a `highlight-*` (sección final, ~líneas 44–46).
- **`/home/luisvmt/Developer/proyectos/proyecto_sshmanager/.clinerules`** (modificar): ampliar la lista de claves soportadas en la sección "Configuración".
- **`/home/luisvmt/Developer/proyectos/proyecto_sshmanager/memory-bank/systemPatterns.md`** y **`activeContext.md`** (modificar): reflejar las nuevas claves de color y el helper `BoxLineColored`.
- **`/home/luisvmt/Developer/proyectos/proyecto_sshmanager/implementation_plan.md`** (nuevo): este documento.


## [Functions]

Nuevas:
- `static void BoxLineColored(string text, int width, ConsoleColor fg, ConsoleColor? bg)` — en `Program.cs`, junto a `BoxLine`. Escribe `│` + línea ajustada a `width` con `fg` como color de texto y, si `bg.HasValue`, `bg.Value` como fondo; termina con `Console.ResetColor()` y `│`. Propósito: permitir líneas con color reutilizando `FitLine`.
- `static ConsoleColor? ParseColorOrNone(string value)` — en `Parameters`. Devuelve `null` si `value` es `none`, `default`, `-` o vacío (fondo "sin color"/heredado); en otro caso devuelve el `ConsoleColor` válido vía `Enum.TryParse`/`Enum.IsDefined`, o `null` si no es válido. Se usa para `*-background` opcionales.

Modificadas:
- `static void BoxRow(string text, int width, bool selected)` — rama `selected` sin cambios (usa `HighlightForeground/Background`); rama no seleccionada: usar `_parameters.ListForeground` y aplicar `_parameters.ListBackground` solo si tiene valor (en lugar de `ConsoleColor.Yellow` fijo). Mantener `Console.ResetColor()`.
- `static Parameters Load(string path)` — añadir casos al `switch`:
  - `"list-foreground"` → `p.ListForeground = ParseColor(value, p.ListForeground);`
  - `"list-background"` → `p.ListBackground = ParseColorOrNone(value);`
  - `"hint-foreground"` → `p.HintForeground = ParseColor(value, p.HintForeground);`
  - `"hint-background"` → `p.HintBackground = ParseColorOrNone(value);`
  - (Nota: `Load` ya ignora líneas con valor vacío, por lo que `hint-background =` deja el valor por defecto `null`.)
- `static void WriteTemplate(string path)` — agregar tras `highlight-*`:
  - `# Color de las filas no seleccionadas (nombres de ConsoleColor).`
  - `list-foreground = Yellow`
  - `# Fondo de las filas no seleccionadas (vacío = sin fondo).`
  - `list-background =`
  - `# Color de la línea de ayuda/teclas del selector de archivos.`
  - `hint-foreground = Red`
  - `# Fondo de la línea de ayuda (vacío = sin fondo).`
  - `hint-background =`
- `static (string path, bool isDir)? PickPath(...)` — en el bloque de render sustituir la llamada `BoxLine(folderMode ? "ESPACIO…" : "ESPACIO…", width)` por `BoxLineColored(<misma cadena>, width, _parameters.HintForeground, _parameters.HintBackground)`.

Sin eliminaciones. `ParseColor` (`static ConsoleColor ParseColor(string, ConsoleColor)`) se conserva y se reutiliza para los *foreground*.

## [Classes]
- **`Parameters`** (existente): se amplía con 4 campos, 4 ramas en `Load`, 1 método nuevo (`ParseColorOrNone`) y 4 claves en `WriteTemplate`. Sin cambios de herencia.
- No se crean ni eliminan clases. `Program`, `HostBlock`, `FileEntry`, `PickerItem` y el enum `PickerKind` no cambian de estructura.

## [Dependencies]
Ninguna dependencia nueva. Todo usa `System.Console` y `System.Enum` ya disponibles en .NET 10. No hay cambios en `sshmanager.csproj`.

## [Testing]
- **Compilación**: `dotnet build` debe terminar con **0 errores / 0 advertencias**.
- **Ayuda**: `dotnet run -- --help` muestra las nuevas claves (`list-foreground`, `list-background`, `hint-foreground`, `hint-background`) y la nota de que fondo vacío = sin fondo.
- **Plantilla**: borrar temporalmente `parameters.conf` en un directorio de prueba y aceptar crear plantilla; verificar que las 4 claves nuevas aparecen comentadas y con valores por defecto.
- **TUI (manual)**:
  1. Arrancar la TUI; confirmar que los servidores no seleccionados salen en amarillo (default `list-foreground = Yellow`).
  2. Abrir "Enviar archivo" (`s`) y "Recibir archivo" (`r`); confirmar que la línea "ESPACIO: seleccionar…" sale en rojo (color de letra distinto al del texto normal).
  3. Cambiar `hint-foreground = Cyan` en `parameters.conf` y reabrir el selector; la línea de ayuda debe salir en cian.
  4. (Opcional) `list-foreground = Green` → filas no seleccionadas en verde; `hint-background = DarkBlue` → fondo de la línea de ayuda azul; `hint-background =` (vacío) → sin fondo.
  5. Verificar que la fila resaltada sigue usando `highlight-foreground/background` y que `ResetColor()` deja el resto del texto con los colores por defecto.
- **Regresión**: confirmar que selección con ESPACIO, filtro `/`, "Ir a" `i` y `esc` siguen funcionando (no se tocan esos flujos).
- **Edge cases**: terminal sin soporte de color (setting de colores es no-op seguro); nombres de color inválidos → se conserva el valor por defecto (`ParseColor`) o `null` (`ParseColorOrNone`).

## [Implementation Order]
1. En `Parameters`: añadir los 4 campos y el método `ParseColorOrNone`.
2. En `Parameters.Load`: añadir las 4 ramas del `switch` para las nuevas claves.
3. En `Parameters.WriteTemplate`: añadir las 4 claves con comentarios.
4. En `parameters.conf`: añadir las 4 claves con la sección de comentarios de colores válidos.
5. En `Program.cs`: añadir el helper `BoxLineColored` y modificar `BoxRow` para usar `ListForeground/ListBackground`.
6. En `PickPath`: cambiar la línea de ayuda del selector a `BoxLineColored` con `HintForeground/HintBackground`.
7. En `PrintHelp`: documentar las nuevas claves y el valor "vacío/none".
8. `dotnet build` (0 errores / 0 advertencias) y validaciones manuales de color.
9. Actualizar `.clinerules` (lista de claves) y `memory-bank/{systemPatterns,activeContext}.md`.
