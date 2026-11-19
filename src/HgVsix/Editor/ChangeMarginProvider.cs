using System.ComponentModel.Composition;
using HgVsix.Diff;
using HgVsix.Options;
using HgVsix.Refresh;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace HgVsix.Editor
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(ChangeMargin.MarginName)]
    [Order(After = PredefinedMarginNames.LineNumber, Before = PredefinedMarginNames.Spacer)]
    [MarginContainer(PredefinedMarginNames.Left)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class ChangeMarginProvider : IWpfTextViewMarginProvider
    {
        // Roles used by the VS diff viewer
        private const string DiffRole = "DIFF";

        [Import]
        internal ITextDocumentFactoryService DocumentFactory { get; set; } = null!;

        [Import]
        internal IEditorFormatMapService FormatMapService { get; set; } = null!;

        [Import]
        internal HgServices Services { get; set; } = null!;

        [Import]
        internal VsDiffCalculator Calculator { get; set; } = null!;

        [Import]
        internal RefreshCoordinator Coordinator { get; set; } = null!;

        public IWpfTextViewMargin? CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var view = wpfTextViewHost.TextView;
            if (view.Roles.Contains(DiffRole))
            {
                return null;
            }

            var buffer = view.TextDataModel.DocumentBuffer;
            if (!DocumentFactory.TryGetTextDocument(buffer, out var document))
            {
                return null;
            }

            HgVsixSettings.Instance.EnsureLoaded();
            var tracker = DocumentChangeTracker.Acquire(buffer, document, Services, Calculator, Coordinator);
            return new ChangeMargin(view, tracker, FormatMapService.GetEditorFormatMap(view));
        }
    }
}
