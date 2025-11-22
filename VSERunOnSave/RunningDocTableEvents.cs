using EditorConfig.Core;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VSERunOnSave
{
    internal class RunningDocTableEvents : IVsRunningDocTableEvents3
    {
        public class Entry
        {
            public string VsBefore { get; set; } = null;
            public string VsAfter { get; set; } = null;
            public string ExtBefore { get; set; } = null;
            public string ExtAfter { get; set; } = null;
            public int ExtTimeout { get; set; } = 30;
            public string OutStart { get; set; } = null;
            public string OutEnd { get; set; } = null;
            public bool OutClear { get; set; } = false;
        }

        public class Cache
        {
            public long Time;
            public ConcurrentDictionary<string, Entry> Entries;

            public Cache(long time)
            {
                Time = time;
                Entries = new(StringComparer.OrdinalIgnoreCase);
            }
        }

        private readonly DTE2 _dte;
        private readonly RunningDocumentTable _runningDocumentTable;
        private readonly ConcurrentDictionary<string, Cache> _dirCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _paneName = "VSERunOnSave";
        private OutputWindowPane _outputPane = null;
        private bool _outputPaneActive = false;
        private Entry _configEntry = null;

        private static readonly Lazy<string> _vsRoot = new(() =>
        {
            var config = new SetupConfiguration();
            var instance = config.GetInstanceForCurrentProcess();
            return instance.GetInstallationPath().TrimEnd('\\');
        });
        public static string VSRootDir => _vsRoot.Value;

        public RunningDocTableEvents(DTE2 _dte, RunningDocumentTable _runningDocumentTable)
        {
            this._dte = _dte;
            this._runningDocumentTable = _runningDocumentTable;
        }

        public int OnBeforeSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var document = CurrentDocument(docCookie);
            if (_dte.ActiveWindow.Kind != "Document" || document == null)
                return VSConstants.S_OK;

            document.Activate();
            _configEntry = GetConfigEntry(document);
            if (_configEntry == null)
                return VSConstants.S_OK;

            _outputPaneActive = false;
            if (_configEntry.OutClear)
                ClearOutput();

            if (!string.IsNullOrWhiteSpace(_configEntry.OutStart))
            {
                string outputString = _configEntry.OutStart;
                ReplaceDefines(document, ref outputString);
                Output(outputString);
            }

            if (!string.IsNullOrWhiteSpace(_configEntry.VsBefore))
                ExecuteCommands(document, _configEntry.VsBefore);

            if (!string.IsNullOrWhiteSpace(_configEntry.ExtBefore))
                ExecuteCommands(document, _configEntry.ExtBefore, _configEntry.ExtTimeout);

            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var document = CurrentDocument(docCookie);
            if (_dte.ActiveWindow.Kind != "Document" || document == null)
                return VSConstants.S_OK;

            if (_configEntry == null)
                return VSConstants.S_OK;

            if (!string.IsNullOrWhiteSpace(_configEntry.VsAfter))
                ExecuteCommands(document, _configEntry.VsAfter);

            if (!string.IsNullOrWhiteSpace(_configEntry.ExtAfter))
                ExecuteCommands(document, _configEntry.ExtAfter, _configEntry.ExtTimeout);

            if (!string.IsNullOrWhiteSpace(_configEntry.OutEnd))
            {
                string outputString = _configEntry.OutEnd;
                ReplaceDefines(document, ref outputString);
                Output(outputString);
            }

            return VSConstants.S_OK;
        }

        private void ExecuteCommands(Document document, string commandString, int timeout = -1)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var commands = commandString.Split(',');
            foreach (var cmd in commands)
            {
                var command = cmd.Trim();
                if (string.IsNullOrWhiteSpace(command))
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
                _dte.ExecuteCommand(command, arguments);
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"Error executing command: {ex}");
#else
                Output($"Error executing command: {ex.Message}");
#endif
            }
        }

        private void ExecuteExternalCommand(string command, int timeout)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var arguments = string.Empty;
                if (command[0] == '"')
                {
                    var segments = command.Split(new string[] { "\" " }, 2, StringSplitOptions.None);
                    command = segments[0] + '"';
                    arguments = segments.Length > 1 ? segments[1].Trim() : string.Empty;
                }
                else
                {
                    var segments = command.Split(new char[] { ' ' }, 2);
                    command = segments[0];
                    arguments = segments.Length > 1 ? segments[1].Trim() : string.Empty;
                }

                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = @"" + command;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardError = true;

                StringBuilder outputData = new StringBuilder();
                process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        outputData.AppendLine(e.Data);
                });
                process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        outputData.AppendLine(e.Data);
                });

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                bool exited = process.WaitForExit(timeout * 1000);
                process.Close();

                if (!exited)
                    Output("Command timedout(" + timeout.ToString() + "s): " + command + " " + arguments);
                else
                    Output(outputData.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"Error executing command: {ex}");
#else
                Output($"Error executing command: {ex.Message}");
#endif
            }
        }

        private void ReplaceDefines(Document document, ref string command)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            command = command.Replace("$(nl)", Environment.NewLine);
            command = command.Replace("$(File)", document.FullName);
            command = command.Replace("$(FileDir)", Path.GetDirectoryName(document.FullName));
            command = command.Replace("$(FileName)", Path.GetFileName(document.FullName));
            command = command.Replace("$(FileNameNoExt)", Path.GetFileNameWithoutExtension(document.FullName));
            command = command.Replace("$(time)", DateTime.Now.ToString("HH:mm:ss"));
            command = command.Replace("$(VSRootDir)", VSRootDir);

            var solution = _dte.Solution;
            command = command.Replace("$(SolutionDir)", solution != null ? Path.GetDirectoryName(solution.FullName) : "");

            var project = ActiveProject();
            command = command.Replace("$(ProjectDir)", project != null ? Path.GetDirectoryName(project.FullName) : "");
            command = command.Replace("$(Configuration)", project != null ? project.ConfigurationManager.ActiveConfiguration.ConfigurationName : "");
            command = command.Replace("$(Platform)", project != null ? project.ConfigurationManager.ActiveConfiguration.PlatformName : "");
        }

        private Entry? GetConfigEntry(Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            long time = 0;
            bool vse = false;
            const string ConfFileVse = ".vserunonsave";
            const string ConfFileEditor = ".editorconfig";

            string? confDir = null;
            string? rootDir = Path.GetDirectoryName(_dte.Solution?.FullName ?? ActiveProject()?.FullName ?? string.Empty);
            var docDir = new DirectoryInfo(Path.GetDirectoryName(document.FullName)!);
            while (docDir != null)
            {
                var file = docDir.GetFiles(ConfFileVse, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (file != null)
                {
                    vse = true;
                    time = Math.Max(time, file.LastWriteTimeUtc.ToFileTimeUtc());
                    confDir ??= docDir.FullName;
                }
                if (!vse)
                {
                    file = docDir.GetFiles(ConfFileEditor, SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (file != null)
                    {
                        time = Math.Max(time, file.LastWriteTimeUtc.ToFileTimeUtc());
                        confDir ??= docDir.FullName;
                    }
                }
                if (!string.IsNullOrEmpty(rootDir) && string.Equals(docDir.FullName, rootDir, StringComparison.OrdinalIgnoreCase))
                    break;
                docDir = docDir.Parent;
            }
            if (confDir == null)
                return null;

            var docRelName = document.FullName.Substring(confDir.Length);
            if (_dirCache.TryGetValue(confDir, out var cache) && cache.Time == time)
            {
                if (cache.Entries.TryGetValue(docRelName, out var cachedEntry))
                {
#if DEBUG
                    Output("Cache Hit! time: " + time);
#endif
                    return cachedEntry;
                }
            }
            else
            {
                _dirCache.AddOrUpdate(confDir, _ => new Cache(time), (_, old) =>
                {
                    old.Time = time;
                    old.Entries.Clear();
                    return old;
                });
            }

            var parser = new EditorConfigParser(vse ? ConfFileVse : ConfFileEditor);
            var config = parser.Parse(document.FullName, parser.GetConfigurationFilesTillRoot(document.FullName));

            var entry = new Entry();

            TrySet(config.Properties, "vs_command_before", v => entry.VsBefore = v);
            TrySet(config.Properties, "vs_command_after", v => entry.VsAfter = v);

            TrySet(config.Properties, "ext_command_before", v => entry.ExtBefore = v);
            TrySet(config.Properties, "ext_command_after", v => entry.ExtAfter = v);
            TrySet(config.Properties, "ext_command_timeout", v => entry.ExtTimeout = Math.Max(0, Math.Min(int.Parse(v), 120)));

            TrySet(config.Properties, "output_clear", v => entry.OutClear = (v.ToLower() == "true" || v == "1"));
            TrySet(config.Properties, "output_start", v => entry.OutStart = v);
            TrySet(config.Properties, "output_end", v => entry.OutEnd = v);

            _dirCache[confDir].Entries[docRelName] = entry;

            return entry;
        }

        private void TrySet(IReadOnlyDictionary<string, string> props, string key, Action<string> setter)
        {
            if (props.TryGetValue(key, out var val) && val != "unset")
                setter(val);
        }

        private Project? ActiveProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try { return _dte.ActiveWindow?.Project; }
            catch { return null; }
        }

        private Document CurrentDocument(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var documentInfo = _runningDocumentTable.GetDocumentInfo(docCookie);
                foreach (Document document in _dte.Documents)
                {
                    if (document.FullName == documentInfo.Moniker)
                        return document;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"Error active document: {ex}");
#else
                Output($"Error active document: {ex.Message}");
#endif
            }

            return _dte.ActiveDocument;
        }

        private void Output(string line, bool clear = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CreateOutputPane();
            if (clear)
                ClearOutput();

            if (!string.IsNullOrEmpty(line))
            {
                if (!_outputPaneActive)
                {
                    _outputPane.Activate();
                    _outputPaneActive = true;
                }
                _outputPane.OutputString(line + Environment.NewLine);
            }
        }

        private void ClearOutput()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CreateOutputPane();
            _outputPane.Clear();
        }

        private void CreateOutputPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_outputPane == null)
            {
                if (_dte?.ToolWindows?.OutputWindow == null)
                    return;

                try { _outputPane = _dte.ToolWindows.OutputWindow.OutputWindowPanes.Item(_paneName); }
                catch { _outputPane = _dte.ToolWindows.OutputWindow.OutputWindowPanes.Add(_paneName); }
            }
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

        public void OnAfterDocumentLockCountChanged(uint docCookie, uint dwRDTLockType, uint dwOldLockCount, uint dwNewLockCount) { }
    }
}
