using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using Task = System.Threading.Tasks.Task;

namespace VSERunOnSave
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>

    [Microsoft.VisualStudio.AsyncPackageHelpers.ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, Microsoft.VisualStudio.AsyncPackageHelpers.PackageAutoLoadFlags.BackgroundLoad)]
    [Microsoft.VisualStudio.AsyncPackageHelpers.ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, Microsoft.VisualStudio.AsyncPackageHelpers.PackageAutoLoadFlags.BackgroundLoad)]
    [Microsoft.VisualStudio.AsyncPackageHelpers.ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasSingleProject_string, Microsoft.VisualStudio.AsyncPackageHelpers.PackageAutoLoadFlags.BackgroundLoad)]
    [Microsoft.VisualStudio.AsyncPackageHelpers.ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasMultipleProjects_string, Microsoft.VisualStudio.AsyncPackageHelpers.PackageAutoLoadFlags.BackgroundLoad)]

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(VSERunOnSavePackage.PackageGuidString)]

    public sealed class VSERunOnSavePackage : AsyncPackage, IAsyncLoadablePackageInitialize
    {
        /// <summary>
        /// VSERunOnSavePackage GUID string.
        /// </summary>
        public const string PackageGuidString = "61ba0c11-7e8d-4b8e-bd33-dd086f094558";

        protected override async Task InitializeAsync(System.Threading.CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = await GetServiceAsync(typeof(DTE)).ConfigureAwait(false) as DTE2;
            var runningDocumentTable = new RunningDocumentTable(this);
            runningDocumentTable.Advise(new RunningDocTableEvents(dte, runningDocumentTable));
        }
    }
}
