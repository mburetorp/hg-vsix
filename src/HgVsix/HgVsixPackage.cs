using System;
using System.Runtime.InteropServices;
using System.Threading;
using HgVsix.Commands;
using HgVsix.Options;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace HgVsix
{
    // Hosts the compare command and the options page. The margin works without this package.
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuids.PackageString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(OptionsPage), "Hg-VSIX", "General", 0, 0, true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class HgVsixPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            TempFiles.DeleteStale(TimeSpan.FromDays(1));

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            HgVsixSettings.Instance.EnsureLoaded();
            if (!(await GetServiceAsync(typeof(SComponentModel)) is IComponentModel componentModel))
            {
                OutputLog.Instance.Write("Component model is not available");
                return;
            }

            var services = componentModel.GetService<HgServices>();
            await CompareWithParentCommand.InitializeAsync(this, services);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TempFiles.DeleteSessionFiles();
            }

            base.Dispose(disposing);
        }
    }
}
