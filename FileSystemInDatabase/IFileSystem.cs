namespace FileSystemInDatabase;

public interface IFileSystem
{
    [PublicAPI]
    Node GetNodeById(Guid id);

    [PublicAPI]
    string GetFullPathOfNode(Guid nodeId);

    [PublicAPI]
    IEnumerable<Node> GetNodesUnderFolder(Guid folderId);

    [PublicAPI]
    IEnumerable<FolderNode> GetSubFoldersUnderFolder(Guid folderId);

    [PublicAPI]
    IEnumerable<FileNode> GetFilesUnderFolder(Guid folderId);

    [PublicAPI]
    IEnumerable<FileNode> SearchFilesUnderFolderAndSubFolders(Guid folderId, Func<FileNode, bool> predicate);

    [PublicAPI]
    Task<Guid> AddSubFolderToFolderAsync(string subFolderName, Guid folderId);

    [PublicAPI]
    Task<Guid> AddFileToFolderAsync(string fileName, byte[] content, Guid folderId);

    [PublicAPI]
    Task MoveNodeToFolderAsync(Guid nodeId, Guid folderId);

    [PublicAPI]
    Task DeleteFolderAsync(Guid folderId);

    [PublicAPI]
    Task DeleteFileAsync(Guid fileId);
}
