using System;
using System.ComponentModel.Composition;
using HgVsix.Core.Hg;
using HgVsix.Options;

namespace HgVsix
{
    // Shared Core objects for all MEF parts and the package
    [Export(typeof(HgServices))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class HgServices
    {
        public HgServices()
        {
            var settings = HgVsixSettings.Instance;
            Runner = new HgProcessRunner(() => HgExecutableResolver.Resolve(settings.HgPath), TimeSpan.FromSeconds(30));
            Runner.Log += message =>
            {
                if (settings.VerboseLogging)
                {
                    OutputLog.Instance.Write(message);
                }
            };

            Locator = new RepositoryLocator();
            Reader = new RevisionReader(Runner);
            Baselines = new BaselineProvider(Locator, Reader, () => settings.MaxFileSizeBytes);
        }

        public HgProcessRunner Runner { get; }

        public RepositoryLocator Locator { get; }

        public RevisionReader Reader { get; }

        public BaselineProvider Baselines { get; }
    }
}
