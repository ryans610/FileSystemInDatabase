namespace FileSystemInDatabase;

public abstract record Node
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public Guid? ParentId { get; init; }
    
    public abstract Types Type { get; }

    public enum Types
    {
        Folder = 0,
        File = 1,
    }
}
