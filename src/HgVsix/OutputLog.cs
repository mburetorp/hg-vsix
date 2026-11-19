using System;
using System.Collections.Concurrent;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace HgVsix
{
    // Writes to the Hg-VSIX pane in the Output window from any thread
    internal sealed class OutputLog
    {
        public static OutputLog Instance { get; } = new OutputLog();

        private readonly ConcurrentQueue<string> _pending = new ConcurrentQueue<string>();
        private IVsOutputWindowPane? _pane;
        private bool _creating;

        private OutputLog()
        {
        }

        public void Write(string message)
        {
            _pending.Enqueue($"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
            var pane = _pane;
            if (pane != null)
            {
                Flush(pane);
                return;
            }

            if (_creating)
            {
                return;
            }

            _creating = true;
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsOutputWindow)) is IVsOutputWindow window)
                {
                    var guid = PackageGuids.OutputPane;
                    window.CreatePane(ref guid, "Hg-VSIX", 1, 1);
                    window.GetPane(ref guid, out var created);
                    _pane = created;
                    if (created != null)
                    {
                        Flush(created);
                    }
                }
            }).FileAndForget("HgVsix/OutputLog");
        }

        private void Flush(IVsOutputWindowPane pane)
        {
            while (_pending.TryDequeue(out var line))
            {
#pragma warning disable VSTHRD010 // OutputStringThreadSafe is free threaded
                pane.OutputStringThreadSafe(line);
#pragma warning restore VSTHRD010
            }
        }
    }
}
