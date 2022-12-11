namespace FileSystemInDatabase;

public abstract record Node
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public Guid ParentId { get; init; }

    public abstract Types Type { get; }

    public bool IsRoot { get; init; }

    public virtual string FullName => Name;

    public enum Types : byte
    {
        Folder = 0,
        File = 1,
    }
}
