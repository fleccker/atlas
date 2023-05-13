namespace Atlas.EntryPoint.Interface
{
    public enum AtlasResult
    {
        FailedToLocateEntryType,
        FailedToInstantiateEntryType,
        FailedToLocateEntryMethod,
        FailedToLocateFile,
        FailedToLocateDirectory,
        FailedToLoadAssembly,
        FailedToInvokeEntryMethod,
        FailedVersionMismatch,
        FailedAlreadyLoaded,

        Success
    }
}