// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace FileSystemInDatabase;

internal record ChangeTableModel<TPrimaryKey>
{
    public long SYS_CHANGE_VERSION { get; init; }
    public long SYS_CHANGE_CREATION_VERSION { get; init; }
    public char SYS_CHANGE_OPERATION { get; init; }
    public TPrimaryKey Id { get; init; }
}
