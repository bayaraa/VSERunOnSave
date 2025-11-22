# VSERunOnSave

This **Visual Studio Extension** allows you to execute **Visual Studio commands** or **external tools** automatically **before** or **after** saving a file.
Supports **Visual Studio 2022/2026** on both **x64** and **ARM64** platforms.

---

## Features

* Run **VS commands** (e.g. `Edit.FormatDocument`, `Build.BuildSolution`) before or after saving.
* Run **external commands** or scripts (e.g. formatters, compilers, or custom tools).
* Automatically substitute variables like `$(File)`, `$(ProjectDir)`, `$(Configuration)`, etc.
* Display messages in a dedicated Visual Studio output pane.
* Compatible with both `.vserunonsave` and `.editorconfig` configuration files.
* Fully supports C++, C#, and other project types.
* Non-blocking — long-running external commands respect a configurable timeout.

---

## Configuration

The `.vserunonsave` configuration file follows the **[EditorConfig](https://editorconfig.org/)** file format and syntax.
You can use familiar INI-style sections (e.g. `[*.cpp]`), `key = value` pairs, and the `root = true` directive to stop searching parent directories.

If no `.vserunonsave` file is found, the extension automatically falls back to `.editorconfig`.

Create a `.vserunonsave` file in your **solution** or **project** root directory.
All keys are **optional** and support **comma-separated multiple commands**.

| Key                   | Description                                              |
| --------------------- | -------------------------------------------------------- |
| `vs_command_before`   | VS command(s) to execute **before** saving the document. |
| `vs_command_after`    | VS command(s) to execute **after** saving the document.  |
| `ext_command_before`  | External command(s) to execute **before** saving.        |
| `ext_command_after`   | External command(s) to execute **after** saving.         |
| `ext_command_timeout` | Timeout for each external command (in seconds).          |
| `output_clear`        | Clears the output pane before any commands run.          |
| `output_start`        | Message to display **before** commands run.              |
| `output_end`          | Message to display **after** all commands finish.        |
| ~~`output_string`~~   | *Removed. Use `output_end` instead.*                     |

---

## Built-in Variable Replacements

These predefined variables are automatically replaced at runtime:

| Variable           | Description                                            |
| ------------------ | ------------------------------------------------------ |
| `$(File)`          | Full path of the file currently being edited.          |
| `$(FileDir)`       | Directory of the current file (without trailing `\`).  |
| `$(FileName)`      | File name of the current file.                         |
| `$(FileNameNoExt)` | File name without extension.                           |
| `$(ProjectDir)`    | Project directory (without trailing `\`).              |
| `$(SolutionDir)`   | Solution directory (without trailing `\`).             |
| `$(Configuration)` | Current build configuration (e.g. `Debug`, `Release`). |
| `$(Platform)`      | Current build platform (e.g. `x64`, `Win32`, `arm64`). |
| `$(VSRootDir)`     | Visual Studio root directory (without trailing `\`).   |
| `$(time)`          | Current time in `HH:mm:ss` format.                     |
| `$(nl)`            | Newline character (useful for multiline output).       |

---

## Examples

**Automatically format all C/C++ files upon save**

```ini
[*.{cpp,hpp,c,h}]
vs_command_before = Edit.FormatDocument
```

**Automatically format and copy a C# file’s contents**

```ini
[sample.cs]
vs_command_before = Edit.FormatDocument
vs_command_after = Edit.SelectAll, Edit.Copy
output_end = Contents copied to clipboard!
```

**Compile a GLSL shader and save as SPIR-V**

```ini
[*.frag]
ext_command_after = "$(SolutionDir)\tools\glslangValidator.exe" -s -V "$(File)" -o "$(FileDir)\spir-v\$(FileNameNoExt).spv"
ext_command_timeout = 30
output_end = Compiled: $(FileNameNoExt).spv
output_clear = true
```

---

## Credits

Inspired by the [VSE-FormatDocumentOnSave](https://github.com/Elders/VSE-FormatDocumentOnSave) extension by **mynkow**.

---

## Change Log

### **1.1.6**
* Added new variable: `$(VSRootDir)`.
* Removed admin rights to install extension.

### **1.1.4**

* Added fallback support for `.editorconfig` when `.vserunonsave` is not present.
* Added support for the `unset` keyword to explicitly disable inherited settings.
* Improved configuration caching and file lookup performance.
* Fixed possible null-reference errors when no project or solution is loaded.
* Minor refactoring and improved logging clarity.

### **1.1.2**

* Major internal refactor for stability and maintainability.
* Added new settings: `ext_command_timeout`, `output_clear`, and `output_start`.
* Added new variables: `$(Configuration)`, `$(Platform)`, `$(time)`, and `$(nl)`.
* Long-running commands no longer block VS — use `ext_command_timeout` to limit execution time.
* Replaced `output_string` with `output_end`.
* Output pane is no longer cleared automatically; use `output_clear = true` if desired.

### **1.0.0**

* Initial release.