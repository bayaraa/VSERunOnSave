using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using EditorConfig.Core;
using System.Text;
using System.Diagnostics;

namespace VSERunOnSave
{
    internal class RunningDocTableEvents : IVsRunningDocTableEvents3
    {
        private readonly DTE2 dte;
        private readonly RunningDocumentTable runningDocumentTable;
        private readonly string configFileName = ".vserunonsave";
        private readonly string paneName = "VSERunOnSave";
        private readonly int defaultTimeout = 30;

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

            var documentDir = Path.GetDirectoryName(document.FullName);

            FileInfo configFile = null;
            var dir = new DirectoryInfo(documentDir);
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
                return VSConstants.S_OK;

            var parser = new EditorConfigParser(configFile.FullName);
            fileConfig = parser.Parse(document.FullName);

            if (fileConfig.Properties.TryGetValue("output_clear", out var clear) && (clear.ToLower() == "true" || clear == "1"))
                ClearOutput();

            if (fileConfig.Properties.TryGetValue("output_start", out string outputString) && !String.IsNullOrWhiteSpace(outputString))
            {
                ReplaceDefines(document, ref outputString);
                Output(outputString);
            }

            if (fileConfig.Properties.TryGetValue("vs_command_before", out var vsCommandString))
                ExecuteCommands(document, vsCommandString);

            if (fileConfig.Properties.TryGetValue("ext_command_before", out var extCommandString))
                ExecuteCommands(document, extCommandString, GetTimeoutValue());

            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var document = CurrentDocument(docCookie);
            if (dte.ActiveWindow.Kind != "Document" || document == null)
                return VSConstants.S_OK;

            if (fileConfig.Properties.TryGetValue("vs_command_after", out var vsCommandString))
                ExecuteCommands(document, vsCommandString);

            if (fileConfig.Properties.TryGetValue("ext_command_after", out var extCommandString))
                ExecuteCommands(document, extCommandString, GetTimeoutValue());

            if (fileConfig.Properties.TryGetValue("output_end", out string outputString) && !String.IsNullOrWhiteSpace(outputString))
            {
                ReplaceDefines(document, ref outputString);
                Output(outputString);
            }

            return VSConstants.S_OK;
        }

        private int GetTimeoutValue()
        {
            if (fileConfig.Properties.TryGetValue("ext_command_timeout", out var timeoutString))
                return Math.Max(0, Math.Min(Int32.Parse(timeoutString), 120));
            return defaultTimeout;
        }

        private void ExecuteCommands(Document document, string commandString, int timeout = -1)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (String.IsNullOrWhiteSpace(commandString))
                return;

            var commands = commandString.Split(',');
            foreach (var cmd in commands)
            {
                var command = cmd.Trim();
                if (String.IsNullOrWhiteSpace(command))
                    continue;

                ReplaceDefines(document, ref command);

                if (timeout == -1)
                    ExecuteVSCommand(command);
                else
                    ExecuteExternalCommand(command, timeout);
            }
        }

        private void ExecuteVSCommand(string command)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var segments = command.Split(new char[] { ' ' }, 2);
                command = segments[0];
                var arguments = segments.Length > 1 ? segments[1].Trim() : string.Empty;
                dte.ExecuteCommand(command, arguments);
            }
            catch (Exception) { }
        }

        private void ExecuteExternalCommand(string command, int timeout)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var arguments = String.Empty;
                if (command[0] == '"')
                {
                    var segments = command.Split(new string[] { "\" " }, 2, StringSplitOptions.None);
                    command = segments[0] + '"';
                    arguments = segments.Length > 1 ? segments[1].Trim() : String.Empty;
                }
                else
                {
                    var segments = command.Split(new char[] { ' ' }, 2);
                    command = segments[0];
                    arguments = segments.Length > 1 ? segments[1].Trim() : String.Empty;
                }

                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = @"" + command;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardError = true;

                StringBuilder outputData = new StringBuilder();
                process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => {
                    if (!String.IsNullOrEmpty(e.Data))
                        outputData.AppendLine(e.Data);
                });
                process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => {
                    if (!String.IsNullOrEmpty(e.Data))
                        outputData.AppendLine(e.Data);
                });

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                bool exited = process.WaitForExit(timeout * 1000);
                process.Close();

                Output(outputData.ToString().TrimEnd());
                if (!exited)
                    Output("Command timedout(" + timeout.ToString() + "s): " + command + " " + arguments);
            }
            catch (Exception) { }
        }

        private void ReplaceDefines(Document document, ref string command)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            command = command.Replace("$(nl)", Environment.NewLine);
            command = command.Replace("$(File)", document.FullName);
            command = command.Replace("$(FileDir)", Path.GetDirectoryName(document.FullName));
            command = command.Replace("$(FileName)", Path.GetFileName(document.FullName));
            command = command.Replace("$(FileNameNoExt)", Path.GetFileNameWithoutExtension(document.FullName));
            command = command.Replace("$(ProjectDir)", Path.GetDirectoryName(dte.ActiveWindow.Project.FullName));
            command = command.Replace("$(SolutionDir)", Path.GetDirectoryName(dte.Solution.FullName));
            command = command.Replace("$(Configuration)", dte.ActiveWindow.Project.ConfigurationManager.ActiveConfiguration.ConfigurationName);
            command = command.Replace("$(Platform)", dte.ActiveWindow.Project.ConfigurationManager.ActiveConfiguration.PlatformName);
            command = command.Replace("$(time)", DateTime.Now.ToString("HH:mm:ss"));
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

        private void Output(string line, bool clear = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CreateOutputPane();
            if (clear)
                ClearOutput();

            if (line != String.Empty)
            {
                outputPane.Activate();
                outputPane.OutputString(line + Environment.NewLine);
            }
        }

        private void ClearOutput()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CreateOutputPane();
            outputPane.Clear();
        }

        private void CreateOutputPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (outputPane == null)
                outputPane = dte.ToolWindows.OutputWindow.OutputWindowPanes.Add(paneName);
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
