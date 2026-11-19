using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace HgVsix.Options
{
    // Tools > Options > Hg-VSIX > General. Values are stored by HgVsixSettings.
    [Guid("9c2e5b14-7a3f-4d6e-8b1c-0f4a2e6d8c35")]
    [ComVisible(true)]
    public sealed class OptionsPage : DialogPage
    {
        [Category("Mercurial")]
        [DisplayName("hg executable")]
        [Description("Full path to hg.exe. Leave empty to use hg on PATH or the TortoiseHg default location.")]
        public string HgPath { get; set; } = string.Empty;

        [Category("General")]
        [DisplayName("Enabled")]
        [Description("Turns all Mercurial change tracking on or off.")]
        public bool Enabled { get; set; } = true;

        [Category("General")]
        [DisplayName("Show change margin")]
        [Description("Shows added, modified and deleted line markers next to the editor.")]
        public bool ShowMargin { get; set; } = true;

        [Category("General")]
        [DisplayName("Maximum file size (KB)")]
        [Description("Files larger than this are not compared.")]
        public int MaxFileSizeKb { get; set; } = HgVsixSettings.DefaultMaxFileSizeKb;

        [Category("Diagnostics")]
        [DisplayName("Verbose logging")]
        [Description("Logs every hg command to the Hg-VSIX output pane.")]
        public bool VerboseLogging { get; set; }

        public override void LoadSettingsFromStorage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var settings = HgVsixSettings.Instance;
            settings.EnsureLoaded();
            HgPath = settings.HgPath;
            Enabled = settings.Enabled;
            ShowMargin = settings.ShowMargin;
            MaxFileSizeKb = settings.MaxFileSizeKb;
            VerboseLogging = settings.VerboseLogging;
        }

        public override void SaveSettingsToStorage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var settings = HgVsixSettings.Instance;
            settings.HgPath = HgPath?.Trim() ?? string.Empty;
            settings.Enabled = Enabled;
            settings.ShowMargin = ShowMargin;
            settings.MaxFileSizeKb = MaxFileSizeKb < 1 ? HgVsixSettings.DefaultMaxFileSizeKb : MaxFileSizeKb;
            settings.VerboseLogging = VerboseLogging;
            settings.Save();
        }
    }
}
