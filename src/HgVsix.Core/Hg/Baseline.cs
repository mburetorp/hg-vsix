namespace HgVsix.Core.Hg
{
    public enum BaselineState
    {
        // Content holds the parent version or is empty for a new file
        Available,
        NotInRepository,
        Untracked,
        Ignored,
        NotTracked,
        Binary,
        TooLarge,
        Error,
    }

    public sealed class Baseline
    {
        private Baseline(BaselineState state, RepositoryLocation? location, FileStatus? status, string? node, byte[]? content, string? message)
        {
            State = state;
            Location = location;
            Status = status;
            Node = node;
            Content = content;
            Message = message;
        }

        public BaselineState State { get; }

        public RepositoryLocation? Location { get; }

        public FileStatus? Status { get; }

        public string? Node { get; }

        public byte[]? Content { get; }

        public string? Message { get; }

        public bool IsAvailable => State == BaselineState.Available;

        // Path the content was read from, differs from Location for renames
        public string? SourcePath => Status?.CopySource ?? Location?.RelativePath;

        // True for added files without a copy source
        public bool IsNewFile => Status?.State == HgFileState.Added && Status.CopySource == null;

        // Equal identities mean equal content so callers can skip work
        public string Identity => $"{State}|{Node}|{Status?.State}|{SourcePath}";

        public static Baseline Available(RepositoryLocation location, FileStatus status, string node, byte[] content) =>
            new Baseline(BaselineState.Available, location, status, node, content, null);

        public static Baseline Unavailable(BaselineState state, RepositoryLocation? location, FileStatus? status, string? node, string? message = null) =>
            new Baseline(state, location, status, node, null, message);
    }
}
