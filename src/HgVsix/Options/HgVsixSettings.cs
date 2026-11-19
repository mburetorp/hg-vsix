using System;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

namespace HgVsix.Options
{
    // Settings live in the VS user settings store so MEF parts can read them without loading the package
    internal sealed class HgVsixSettings
    {
        public const string CollectionPath = "HgVsix";
        public const int DefaultMaxFileSizeKb = 5 * 1024;

        public static HgVsixSettings Instance { get; } = new HgVsixSettings();

        private bool _loaded;

        private HgVsixSettings()
        {
        }

        public event EventHandler? Changed;

        public string HgPath { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        public bool ShowMargin { get; set; } = true;

        public int MaxFileSizeKb { get; set; } = DefaultMaxFileSizeKb;

        public bool VerboseLogging { get; set; }

        public long MaxFileSizeBytes => Math.Max(1, MaxFileSizeKb) * 1024L;

        public void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            ThreadHelper.ThrowIfNotOnUIThread();
            _loaded = true;
            try
            {
                var store = GetStore();
                if (!store.CollectionExists(CollectionPath))
                {
                    return;
                }

                HgPath = store.GetString(CollectionPath, nameof(HgPath), HgPath);
                Enabled = store.GetBoolean(CollectionPath, nameof(Enabled), Enabled);
                ShowMargin = store.GetBoolean(CollectionPath, nameof(ShowMargin), ShowMargin);
                MaxFileSizeKb = store.GetInt32(CollectionPath, nameof(MaxFileSizeKb), MaxFileSizeKb);
                VerboseLogging = store.GetBoolean(CollectionPath, nameof(VerboseLogging), VerboseLogging);
            }
            catch (Exception ex)
            {
                OutputLog.Instance.Write($"Could not load settings: {ex.Message}");
            }
        }

        public void Save()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var store = GetStore();
                if (!store.CollectionExists(CollectionPath))
                {
                    store.CreateCollection(CollectionPath);
                }

                store.SetString(CollectionPath, nameof(HgPath), HgPath ?? string.Empty);
                store.SetBoolean(CollectionPath, nameof(Enabled), Enabled);
                store.SetBoolean(CollectionPath, nameof(ShowMargin), ShowMargin);
                store.SetInt32(CollectionPath, nameof(MaxFileSizeKb), MaxFileSizeKb);
                store.SetBoolean(CollectionPath, nameof(VerboseLogging), VerboseLogging);
            }
            catch (Exception ex)
            {
                OutputLog.Instance.Write($"Could not save settings: {ex.Message}");
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static WritableSettingsStore GetStore()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var manager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            return manager.GetWritableSettingsStore(SettingsScope.UserSettings);
        }
    }
}
