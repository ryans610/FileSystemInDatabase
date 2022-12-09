namespace FileSystemInDatabase;

public record FolderNode : Node
{
    public override Types Type => Types.Folder;
}
