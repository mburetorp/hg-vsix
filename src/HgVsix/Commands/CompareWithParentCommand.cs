using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using HgVsix.Core.Hg;
using HgVsix.Options;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace HgVsix.Commands
{
    // Opens the VS diff window with the parent revision on the left and the open document on the right.
    // The right side is the real file so VS shows the editor buffer including unsaved changes.
    internal sealed class CompareWithParentCommand
    {
        private readonly AsyncPackage _package;
        private readonly HgServices _services;
        private readonly DTE _dte;
        private readonly HashSet<string> _pendingLookups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CompareWithParentCommand(AsyncPackage package, HgServices services, DTE dte)
        {
            _package = package;
            _services = services;
            _dte = dte;
        }

        public static async Task InitializeAsync(AsyncPackage package, HgServices services)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            if (!(await package.GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService) ||
                !(await package.GetServiceAsync(typeof(DTE)) is DTE dte))
            {
                OutputLog.Instance.Write("Command services are not available");
                return;
            }

            var instance = new CompareWithParentCommand(package, services, dte);

            var command = new OleMenuCommand(instance.Execute, new CommandID(PackageGuids.CommandSet, PackageIds.CompareWithParentCommand));
            command.BeforeQueryStatus += instance.OnBeforeQueryStatus;
            commandService.AddCommand(command);
        }

        private string? ActiveDocumentPath
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    return _dte.ActiveDocument?.FullName;
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
        }

        // Shown only for documents inside an hg repository.
        // Opening a menu never probes the file system. An unknown location is looked up in the background
        // and the command state is refreshed afterwards.
        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var command = (OleMenuCommand)sender;
            var path = ActiveDocumentPath;
            RepositoryLocation? location = null;
            if (HgVsixSettings.Instance.Enabled && path != null && !_services.Locator.TryLocateCached(path, out location))
            {
                ScheduleLookup(path);
            }

            command.Visible = command.Enabled = location != null;
        }

        private void ScheduleLookup(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_pendingLookups.Add(path))
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                var found = _services.Locator.Locate(path) != null;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _pendingLookups.Remove(path);
                if (found && await _package.GetServiceAsync(typeof(SVsUIShell)) is IVsUIShell shell)
                {
                    shell.UpdateCommandUI(0);
                }
            }).FileAndForget("HgVsix/CompareLookup");
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var path = ActiveDocumentPath;
            if (path == null)
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await CompareAsync(path);
                }
                catch (Exception ex)
                {
                    OutputLog.Instance.Write($"Compare failed for {path}: {ex}");
                    await ShowMessageAsync($"Compare with Mercurial Parent failed: {ex.Message}");
                }
            }).FileAndForget("HgVsix/Compare");
        }

        private async Task CompareAsync(string path)
        {
            await TaskScheduler.Default;
            var baseline = await _services.Baselines.GetBaselineAsync(path, _package.DisposalToken);
            if (!baseline.IsAvailable)
            {
                await ShowMessageAsync(DescribeUnavailable(baseline));
                return;
            }

            var fileName = Path.GetFileName(path);
            var shortNode = RevisionReader.ShortNode(baseline.Node!);
            var leftFile = TempFiles.Write(fileName, shortNode, baseline.Content!);
            var source = baseline.IsNewFile ? "(new file)" : $"{baseline.SourcePath} @ {shortNode}";

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            if (!(await _package.GetServiceAsync(typeof(SVsDifferenceService)) is IVsDifferenceService diff))
            {
                throw new InvalidOperationException("The Visual Studio difference service is not available");
            }

            diff.OpenComparisonWindow2(
                leftFile,
                path,
                $"{fileName} vs. parent",
                $"{path} compared with {source}",
                $"{source} (parent)",
                $"{fileName} (working copy)",
                null,
                null,
                (uint)(__VSDIFFSERVICEOPTIONS.VSDIFFOPT_LeftFileIsTemporary | __VSDIFFSERVICEOPTIONS.VSDIFFOPT_DetectBinaryFiles));
        }

        private static string DescribeUnavailable(Baseline baseline) => baseline.State switch
        {
            BaselineState.NotInRepository => "The file is not inside a Mercurial repository.",
            BaselineState.Untracked => "The file is not tracked by Mercurial.",
            BaselineState.Ignored => "The file is ignored by Mercurial.",
            BaselineState.NotTracked => $"The file has no committed version ({baseline.Status?.State}).",
            BaselineState.Binary => "The committed version is a binary file.",
            BaselineState.TooLarge => "The file is larger than the configured maximum size.",
            _ => baseline.Message ?? "The committed version could not be read.",
        };

        private async Task ShowMessageAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            VsShellUtilities.ShowMessageBox(
                _package,
                message,
                "Hg-VSIX",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
