using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace HgVsix.Editor
{
    // Marker colors, editable in Tools > Options > Fonts and Colors
    internal static class ChangeFormatNames
    {
        public const string Added = "HgVsix.AddedLine";
        public const string Modified = "HgVsix.ModifiedLine";
        public const string Deleted = "HgVsix.DeletedLine";
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(ChangeFormatNames.Added)]
    [UserVisible(true)]
    internal sealed class AddedLineFormat : EditorFormatDefinition
    {
        public AddedLineFormat()
        {
            DisplayName = "Hg-VSIX Added Line";
            BackgroundColor = Color.FromRgb(0x2E, 0xA0, 0x43);
            ForegroundCustomizable = false;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(ChangeFormatNames.Modified)]
    [UserVisible(true)]
    internal sealed class ModifiedLineFormat : EditorFormatDefinition
    {
        public ModifiedLineFormat()
        {
            DisplayName = "Hg-VSIX Modified Line";
            BackgroundColor = Color.FromRgb(0x2F, 0x81, 0xF7);
            ForegroundCustomizable = false;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(ChangeFormatNames.Deleted)]
    [UserVisible(true)]
    internal sealed class DeletedLineFormat : EditorFormatDefinition
    {
        public DeletedLineFormat()
        {
            DisplayName = "Hg-VSIX Deleted Line";
            BackgroundColor = Color.FromRgb(0xF8, 0x51, 0x49);
            ForegroundCustomizable = false;
        }
    }
}
