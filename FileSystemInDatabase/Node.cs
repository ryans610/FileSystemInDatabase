namespace FileSystemInDatabase;

public abstract record Node
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public Guid ParentId { get; init; }

    public abstract Types Type { get; }

    /// <summary>
    /// 是否為根目錄。
    /// 如果此節點是根目錄，則 <see cref="ParentId"/> 的值會與 <see cref="Id"/> 相同。
    /// </summary>
    public bool IsRoot { get; init; }

    public virtual string FullName => Name;

    public enum Types : byte
    {
        Folder = 0,
        File = 1,
    }
}
