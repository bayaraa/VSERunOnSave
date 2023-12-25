using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Threading.Tasks;
using System.IO;
using EditorConfig.Core;

namespace VSERunOnSave
{
    internal class RunningDocTableEvents : IVsRunningDocTableEvents3
    {
        private readonly DTE2 dte;
        private readonly RunningDocumentTable runningDocumentTable;
        private readonly string configFileName = ".vserunonsave";
        private readonly string paneName = "VSERunOnSave";
        private OutputWindowPane outputPane = null;
        private FileConfiguration fileConfig;

        public RunningDocTableEvents(DTE2 dte, RunningDocumentTable runningDocumentTable)
        {
            this.dte = dte;
            this.runningDocumentTable = runningDocumentTable;
        }

        public int OnBeforeSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var document = CurrentDocument(docCookie);
            if (dte.ActiveWindow.Kind != "Document" || document == null)
                return VSConstants.S_OK;

            document.Activate();
            run(document, true);

            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var document = CurrentDocument(docCookie);
            if (dte.ActiveWindow.Kind != "Document" || document == null)
                return VSConstants.S_OK;

            run(document, false);
            document.Activate();

            return VSConstants.S_OK;
        }

        private void run(Document document, bool before)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (before)
            {
                var document_dir = Path.GetDirectoryName(document.FullName);

                FileInfo configFile = null;
                var dir = new DirectoryInfo(document_dir);
                while (dir != null)
                {
                    var files = dir.GetFiles(configFileName);
                    if (files.Length > 0)
                    {
                        configFile = files[0];
                        break;
                    }
                    dir = dir.Parent;
                }

                if (configFile == null)
                    return;

                var parser = new EditorConfigParser(configFile.FullName);
                fileConfig = parser.Parse(document.FullName);

                OutputString("", true);
                fileConfig.Properties.TryGetValue("vs_command_before", out string vsCommandString);
                fileConfig.Properties.TryGetValue("ext_command_before", out string extCommandString);
                executeCommands(document, vsCommandString, true);
                executeCommands(document, extCommandString, false);
            }
            else
            {
                fileConfig.Properties.TryGetValue("vs_command_after", out string vsCommandString);
                fileConfig.Properties.TryGetValue("ext_command_after", out string extCommandString);
                executeCommands(document, vsCommandString, true);
                executeCommands(document, extCommandString, false);

                fileConfig.Properties.TryGetValue("output_string", out string output_string);
                if (!string.IsNullOrWhiteSpace(output_string))
                    OutputString(output_string);
            }
        }

        private void executeCommands(Document document, string commandString, bool isVSCommand)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (string.IsNullOrWhiteSpace(commandString))
                return;

            var filePath = document.FullName;
            var projectDir = Path.GetDirectoryName(dte.ActiveWindow.Project.FullName);
            var solutionDir = Path.GetDirectoryName(dte.Solution.FullName);

            var commands = commandString.Split(',');
            foreach (var cmd in commands)
            {
                var command = cmd.Trim();
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                command = command.Replace("$(File)", filePath);
                command = command.Replace("$(ProjectDir)", projectDir);
                command = command.Replace("$(SolutionDir)", solutionDir);

                if (isVSCommand)
                    executeVSCommand(command);
                else
                    executeExternalCommand(command);
            }
        }

        private void executeVSCommand(string command)
        {
            try
            {
                var segments = command.Split(new char[] { ' ' }, 2);
                var vsCommand = segments[0];
                var arguments = segments.Length > 1 ? segments[1].Trim() : string.Empty;
                dte.ExecuteCommand(vsCommand, arguments);
            }
            catch (Exception) { }
        }

        private void executeExternalCommand(string command)
        {
            try
            {
                var extCommand = command;
                var arguments = string.Empty;
                if (command[0] == '"')
                {
                    var segments = command.Split(new string[] { "\" " }, 2, StringSplitOptions.None);
                    extCommand = segments[0] + '"';
                    arguments = segments.Length > 1 ? segments[1].Trim() : string.Empty;
                }
                else
                {
                    var segments = command.Split(new char[] { ' ' }, 2);
                    extCommand = segments[0];
                    arguments = segments.Length > 1 ? segments[1].Trim() : string.Empty;
                }

                System.Diagnostics.Process p = new System.Diagnostics.Process();
                p.StartInfo.FileName = @"" + extCommand;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardError = true;
                p.OutputDataReceived += (sender, args) => OutputStringAsync(args.Data).ConfigureAwait(true);
                p.ErrorDataReceived += (sender, args) => OutputStringAsync(args.Data).ConfigureAwait(true);
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                p.Close();
            }
            catch (Exception) { }
        }

        private Document CurrentDocument(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var documentInfo = runningDocumentTable.GetDocumentInfo(docCookie);
                foreach(Document document in dte.Documents)
                {
                    if (document.FullName == documentInfo.Moniker)
                        return document;
                }
            }
            catch (Exception) { }

            return dte.ActiveDocument;
        }

        private void OutputString(string line, bool clear = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (outputPane == null)
                outputPane = dte.ToolWindows.OutputWindow.OutputWindowPanes.Add(paneName);

            if (clear)
            {
                outputPane.Clear();
                return;
            }
            outputPane.Activate();
            outputPane.OutputString(line + System.Environment.NewLine);
        }

        private async Task OutputStringAsync(string line)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            outputPane.Activate();
            outputPane.OutputString(line + System.Environment.NewLine);
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.S_OK;
        }

        public void OnAfterDocumentLockCountChanged(uint docCookie, uint dwRDTLockType, uint dwOldLockCount, uint dwNewLockCount) {}
    }
}
