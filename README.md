VSERunOnSave
========================
This **Visual Studio Extension** enables call **VS** commands or external commands on before/after save event of the file currently being edited.  
Supports Visual Studio 2022 x64/arm64 platforms.

## Configuration
In order to work this extension, you need to create `.vserunonsave` configuration file in your solution or project root directory.  
Each section may have following config lines:
- `vs_command_before` VS commands to run right before the document is saved.
- `vs_command_after` VS commands to run right after the document is saved.
- `ext_command_before` External commands to call right before the document is saved.
- `ext_command_after` External commands to call right after the document is saved.
- `ext_command_timeout` Timeout setting for each external commands in seconds.
- `output_clear` Clear the output pane before all commands run.
- `output_start` A string to display in the output pane before all commands run.
- `output_end` A string to display in the output pane after all commands run.
- ~~`output_string` simple message to display into output pane after all commands runs.~~ **Removed. Use:`output_end`*

**You can enter comma separated multiple commands.*  
**All config are optional.* 

Automatically replaced built-in variables:
- `$(File)` Full path of the file currently being edited.
- `$(FileDir)` Directory of the file currently being edited (without trailing `\`).
- `$(FileName)` File name of the file currently being edited.
- `$(FileNameNoExt)` File name of the file currently being edited (without extension).
- `$(ProjectDir)` Project directory (without trailing `\`).
- `$(SolutionDir)` Solution directory (without trailing `\`).
- `$(Configuration)` Current project's build configuration name (ex: Release/Debug etc).
- `$(Platform)` Current project's build platform name (ex: x64/Win32/arm64 etc).
- `$(time)` Current time (format: HH:mm:ss).
- `$(nl)` New line character (Useful when you output multiline string).

## Examples
Ex: Any c/c++ source/header files to automatically formatted upon save:
```ini
[*.{cpp,hpp,c,h}]
vs_command_before = Edit.FormatDocument
```
Ex: sample.cs file to automatically formatted and all it's contents copied to clipboard:
```ini
[sample.cs]
vs_command_before = Edit.FormatDocument
vs_command_after = Edit.SelectAll, Edit.Copy
output_end = Contents copied to clipboard!
```
Ex: Compile fragment shader file and save with different name:
```ini
[*.frag]
ext_command_after = "$(SolutionDir)\tools\glslangValidator.exe" -s -V "$(File)" -o "$(FileDir)\spir-v\$(FileNameNoExt).spv"
ext_command_timeout = 30
output_end = Compiled: $(FileNameNoExt).spv
output_clear = true
```

## Credits
Inspired by [VSE-FormatDocumentOnSave](https://github.com/Elders/VSE-FormatDocumentOnSave) extension by mynkow.

## Change Log
### 1.1.2
- Overall code refator.
- Added: Configs `ext_command_timeout` `output_clear` `output_start`.
- Added: Variables `$(Configuration)` `$(Platform)` `$(time)` `$(nl)`.
- Added: Slow commands can block VS until they finish. Now you can use `ext_command_timeout` to break.
- Changed: Config `output_string` replaced with `output_end`.
- Changed: now output pane not cleared after all commands run. Have to set `output_clear` to clear pane.

### 1.0.0
Initial release.