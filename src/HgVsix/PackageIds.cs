using System;

namespace HgVsix
{
    internal static class PackageGuids
    {
        public const string PackageString = "b3a4e6c2-8d51-4f7e-a0c9-5e2b7d1f4a63";
        public const string CommandSetString = "e7c1f9a2-4b3d-4e8f-9a6c-1d2b3c4e5f70";
        public const string OutputPaneString = "4a9e2d71-6c3b-4f58-8e1a-7b9c0d2e3f41";

        public static readonly Guid Package = new Guid(PackageString);
        public static readonly Guid CommandSet = new Guid(CommandSetString);
        public static readonly Guid OutputPane = new Guid(OutputPaneString);
    }

    // Must match HgVsixPackage.vsct
    internal static class PackageIds
    {
        public const int CompareWithParentCommand = 0x0100;
    }
}
