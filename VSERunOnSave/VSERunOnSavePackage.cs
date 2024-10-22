using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using Task = System.Threading.Tasks.Task;

namespace VSERunOnSave
{
    [Microsoft.VisualStudio.AsyncPackageHelpers.ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, Microsoft.VisualStudio.AsyncPackageHelpers.PackageAutoLoadFlags.BackgroundLoad)]
    [Microsoft.VisualStudio.AsyncPackageHelpers.ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, Microsoft.VisualStudio.AsyncPackageHelpers.PackageAutoLoadFlags.BackgroundLoad)]
    [Microsoft.VisualStudio.AsyncPackageHelpers.ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasSingleProject_string, Microsoft.VisualStudio.AsyncPackageHelpers.PackageAutoLoadFlags.BackgroundLoad)]
    [Microsoft.VisualStudio.AsyncPackageHelpers.ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasMultipleProjects_string, Microsoft.VisualStudio.AsyncPackageHelpers.PackageAutoLoadFlags.BackgroundLoad)]

    [Guid(PackageGuidString)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]

    public sealed class VSERunOnSavePackage : AsyncPackage
    {
        public const string PackageGuidString = "61ba0c11-7e8d-4b8e-bd33-dd086f094558";

        protected override async Task InitializeAsync(System.Threading.CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = await GetServiceAsync(typeof(DTE)) as DTE2;
            Assumes.Present(dte);

            var runningDocumentTable = new RunningDocumentTable(this);
            runningDocumentTable.Advise(new RunningDocTableEvents(dte, runningDocumentTable));
        }
    }
}
