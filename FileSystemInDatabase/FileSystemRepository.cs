using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using Dapper;

namespace FileSystemInDatabase;

internal class FileSystemRepository
{
    private const int DeleteChunkSize = 500;

    private const string SqlSelectNode = $@"
SELECT
    [{nameof(Node)}].[{nameof(Node.Type)}], -- Type need to be first for node type check
    [{nameof(Node)}].[{nameof(Node.Id)}],
    [{nameof(Node)}].[{nameof(Node.Name)}],
    [{nameof(Node)}].[{nameof(Node.ParentId)}],
    [{nameof(Node)}].[{nameof(Node.IsRoot)}],
    [{nameof(FileNode)}].[{nameof(FileNode.Extension)}],
    [{nameof(FileNode)}].[{nameof(FileNode.Content)}]
FROM [{nameof(Node)}]
LEFT JOIN [{nameof(FolderNode)}]
    ON [{nameof(FolderNode)}].[{nameof(FolderNode.Id)}]=[{nameof(Node)}].[{nameof(Node.Id)}]
LEFT JOIN [{nameof(FileNode)}]
    ON [{nameof(FileNode)}].[{nameof(FileNode.Id)}]=[{nameof(Node)}].[{nameof(Node.Id)}]";

    private const string SqlInsertNode = $@"
INSERT INTO [{nameof(Node)}]
(
    [{nameof(Node.Id)}],
    [{nameof(Node.Name)}],
    [{nameof(Node.ParentId)}],
    [{nameof(Node.Type)}]
)
VALUES
(
    @{nameof(Node.Id)},
    @{nameof(Node.Name)},
    @{nameof(Node.ParentId)},
    @{nameof(Node.Type)}
);";

    public FileSystemRepository(IOptions<FileSystemOptions> options)
    {
        _options = options;
    }

    private readonly IOptions<FileSystemOptions> _options;

    public async Task<IEnumerable<Node>> GetAllNodesAsync()
    {
        await using var connection = await OpenConnectionAsync();
        var nodes = new List<Node>();
        await using var reader = await connection.ExecuteReaderAsync(SqlSelectNode);
        var (folderParser, fileParser) = GetNodeRowParsers(reader);
        while (await reader.ReadAsync())
        {
            var node = ParseNode(reader, folderParser, fileParser);
            if (node is null)
            {
                continue;
            }

            nodes.Add(item: node);
        }

        return nodes;
    }

    public async Task<Node> GetNodeByIdAsync(Guid id)
    {
        await using var connection = await OpenConnectionAsync();
        await using var reader = await connection.ExecuteReaderAsync($@"
{SqlSelectNode}
WHERE [{nameof(Node)}].[{nameof(Node.Id)}]=@{nameof(id)}", new { id });
        var (folderParser, fileParser) = GetNodeRowParsers(reader);
        return await reader.ReadAsync() ? ParseNode(reader, folderParser, fileParser) : null;
    }

    public async Task InsertFolderNodeAsync(FolderNode folderNode)
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync($@"
BEGIN TRANSACTION;
{SqlInsertNode}
INSERT INTO [{nameof(FolderNode)}]
(
    [{nameof(FolderNode.Id)}]
)
VALUES
(
    @{nameof(FolderNode.Id)}
);
COMMIT;
", folderNode);
    }

    public async Task InsertFileNodeAsync(FileNode fileNode)
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync($@"
BEGIN TRANSACTION;
{SqlInsertNode}
INSERT INTO [{nameof(FileNode)}]
(
    [{nameof(FileNode.Id)}],
    [{nameof(FileNode.Extension)}],
    [{nameof(FileNode.Content)}]
)
VALUES
(
    @{nameof(FileNode.Id)},
    @{nameof(FileNode.Extension)},
    @{nameof(FileNode.Content)}
);
COMMIT;
", fileNode);
    }

    public async Task<Guid> ChangeNodeParentAsync(Guid nodeId, Guid parentId)
    {
        await using var connection = await OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<Guid>($@"
BEGIN TRANSACTION;

DECLARE @NodeName nvarchar(255);
SELECT @NodeName=[{nameof(Node)}].[{nameof(Node.Name)}]
FROM [{nameof(Node)}]
WHERE [{nameof(Node)}].[{nameof(Node.Id)}]=@{nameof(nodeId)};

UPDATE [{nameof(Node)}] SET
[{nameof(Node.ParentId)}]=@{nameof(parentId)}
WHERE [{nameof(Node.Id)}]=@{nameof(nodeId)}
  AND NOT EXISTS (SELECT *
                  FROM [{nameof(Node)}]
                  WHERE [{nameof(Node)}].[{nameof(Node.ParentId)}]=@{nameof(parentId)}
                    AND [{nameof(Node)}].[{nameof(Node.Name)}]=@NodeName);

COMMIT;

SELECT [{nameof(Node)}].[{nameof(Node.ParentId)}]
FROM [{nameof(Node)}]
WHERE [{nameof(Node)}].[{nameof(Node.Id)}]=@{nameof(nodeId)};
", new { nodeId, parentId });
    }

    public async Task DeleteFolderNodeAsync(
        Guid nodeId,
        IEnumerable<Guid> subNodeParentIds)
    {
        await using var connection = await OpenConnectionAsync();

        // Delete folder node.
        await connection.ExecuteAsync($@"
DELETE FROM [{nameof(Node)}]
WHERE [{nameof(Node)}].[{nameof(Node.Id)}]=@{nameof(nodeId)};
", new { nodeId });

        // Delete sub node using parent id in case of new direct sub node.
        // For new sub node that more than one layer,
        // ignore now and leave it to house keeping.
        foreach (var parentIds in subNodeParentIds.Chunk(DeleteChunkSize))
        {
            await connection.ExecuteAsync($@"
DELETE FROM [{nameof(Node)}]
WHERE [{nameof(Node)}].[{nameof(Node.ParentId)}] IN @{nameof(parentIds)}
", new { parentIds });
        }

        await ClearNodelessFileAndFolder(connection);
    }

    public async Task DeleteFileNodeAsync(Guid nodeId)
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync($@"
BEGIN TRANSACTION;
DELETE FROM [{nameof(Node)}]
WHERE [{nameof(Node)}].[{nameof(Node.Id)}]=@{nameof(nodeId)};
DELETE FROM [{nameof(FileNode)}]
WHERE [{nameof(FileNode)}].[{nameof(FileNode.Id)}]=@{nameof(nodeId)};
COMMIT;
", new { nodeId });
    }

    public async Task HouseKeepingOnceAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync($@"
DELETE FROM [{nameof(Node)}]
WHERE [{nameof(Node)}].[{nameof(Node.ParentId)}] NOT IN (
    SELECT [{nameof(Node.Id)}] FROM [{nameof(Node)}])");
        await ClearNodelessFileAndFolder(connection);
    }

    public async Task<long> GetInitialChangeTableVersion()
    {
        await using var connection = await OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>($@"
SELECT MAX([{nameof(ChangeTableModel<Guid>.SYS_CHANGE_VERSION)}])
FROM CHANGETABLE(CHANGES [{nameof(Node)}], 0) chg");
    }

    public async Task<IEnumerable<ChangeTableModel<Guid>>> FetchTableChanges(long version)
    {
        await using var connection = await OpenConnectionAsync();
        return await connection.QueryAsync<ChangeTableModel<Guid>>($@"
SELECT *
FROM CHANGETABLE(CHANGES [{nameof(Node)}], @version) chg", new { version });
    }

    private (Func<IDataReader, Node> folderParser, Func<IDataReader, Node> fileParser) GetNodeRowParsers(
        DbDataReader reader)
    {
        var folderParser = reader.GetRowParser<Node>(typeof(FolderNode));
        var fileParser = reader.GetRowParser<Node>(typeof(FileNode));
        return (folderParser, fileParser);
    }

    private Node ParseNode(DbDataReader reader, Func<IDataReader, Node> folderParser,
        Func<IDataReader, Node> fileParser)
    {
        var node = (Node.Types)reader.GetByte(0) switch
        {
            Node.Types.Folder => folderParser.Invoke(reader),
            Node.Types.File => fileParser.Invoke(reader),
            _ => null,
        };
        return node;
    }

    // ReSharper disable once IdentifierTypo
    private async Task ClearNodelessFileAndFolder(DbConnection connection = null)
    {
        var needClose = false;
        if (connection is null)
        {
            needClose = true;
            connection = await OpenConnectionAsync();
        }

        await connection.ExecuteAsync($@"
BEGIN TRANSACTION;
DELETE FROM [{nameof(FileNode)}]
WHERE [{nameof(FileNode)}].[{nameof(FileNode.Id)}] NOT IN (
    SELECT [{nameof(Node.Id)}] FROM [{nameof(Node)}]);
DELETE FROM [{nameof(FolderNode)}]
WHERE [{nameof(FolderNode)}].[{nameof(FolderNode.Id)}] NOT IN (
    SELECT [{nameof(Node.Id)}] FROM [{nameof(Node)}]);
COMMIT;");

        if (needClose)
        {
            await connection.DisposeAsync();
        }
    }

    private async Task<DbConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(_options.Value.DatabaseConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
