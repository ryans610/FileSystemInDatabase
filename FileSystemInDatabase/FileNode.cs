namespace FileSystemInDatabase;

public record FileNode : Node
{
    public override Types Type => Types.File;

    public string Extension { get; init; }

    public byte[] Content { get; init; }
}
