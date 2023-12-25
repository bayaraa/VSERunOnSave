VSERunOnSave
========================
This **Visual Studio Extension** enables run **VS** command or external command on before/after save event of currently editing file.  
Supports Visual Studio 2022 x64/arm64 platforms.

## Configuration
In order to run extension you need to create `.vserunonsave` configuration file in your solition or project root directory.  
Each section may have following config lines:
- `vs_command_before` vs commands to run right before save.
- `vs_command_after` vs commands to run right after save.
- `ext_command_before` external command to call right before save.
- `ext_command_after` external command to call right after save.
- `output_string` simple message to display into output pane after all commands runs.

**You can enter comma separated multiple commands.*  
**All config are optional.* 

Built in variabled automatically replaced:
- `$(File)` Full path of currently editing file.
- `$(FileDir)` Directory of currently editing file (without trailing `\`).
- `$(FileName)` File name of currently editing file.
- `$(FileNameNoExt)` File name of currently editing file (without extension).
- `$(ProjectDir)` Project directory (without trailing `\`).
- `$(SolutionDir)` Solution directory (without trailing `\`).

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
output_string = Contents copied to clipboard!
```
Ex: Compile fragment shader file and save with different name:
```ini
[*.frag]
ext_command_after = $(SolutionDir)\tools\glslangValidator.exe -s -V $(File) -o $(FileDir)\spir-v\$(FileNameNoExt).spv
output_string = Compiled: $(FileNameNoExt).spv
```

## Credits
Inspired by [VSE-FormatDocumentOnSave](https://github.com/Elders/VSE-FormatDocumentOnSave) extension by mynkow.
