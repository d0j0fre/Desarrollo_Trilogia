namespace Proyecto_Final.Services;

// Server-only outcome metadata. The allocated IDs are known before COMMIT; a lost
// acknowledgement must not discard the file or replay the metadata INSERT.
public sealed class DocumentPendingCommitUnconfirmedException : Exception
{
    public int DocumentId { get; }
    public int? VersionId { get; }
    public string StorageKey { get; }
    public DocumentPendingCommitUnconfirmedException(int documentId, int? versionId, string storageKey, Exception inner)
        : base("No se confirmó el commit de la carga documental.", inner)
    {
        if (documentId <= 0 || versionId is <= 0 || !PrivateFileStorageService.IsSafeStorageKey(storageKey))
            throw new ArgumentException("Metadata de resultado documental no acreditada.");
        DocumentId = documentId; VersionId = versionId; StorageKey = storageKey;
    }
}
