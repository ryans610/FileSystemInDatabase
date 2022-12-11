namespace FileSystemInDatabase;

public interface IFileSystem
{
    Task AddSubFolderToFolderAsync(string subFolderName, Guid folderId);
    Task DeleteFileAsync(Guid fileId);
    Task DeleteFolderAsync(Guid folderId);
    IEnumerable<FileNode> GetFilesUnderFolder(Guid folderId);
    string GetFullPathOfNode(Guid nodeId);
    Node GetNodeById(Guid id);
    IEnumerable<Node> GetNodesUnderFolder(Guid folderId);
    IEnumerable<FolderNode> GetSubFoldersUnderFolder(Guid folderId);
    Task MoveNodeToFolderAsync(Guid nodeId, Guid folderId);
    IEnumerable<FileNode> SearchFilesUnderFolderAndSubFolders(Guid folderId, Func<FileNode, bool> predicate);
}
