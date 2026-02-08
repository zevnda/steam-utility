namespace API
{
    public enum ClientInitializeFailure : byte
    {
        None = 0,
        InstallPathNotFound,
        LibraryLoadFailed,
        ClientCreationFailed,
        PipeCreationFailed,
        UserConnectionFailed,
        ApplicationIdMismatch,
    }
}
