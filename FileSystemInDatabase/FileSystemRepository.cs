using System.Data.Common;
using System.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Options;

namespace FileSystemInDatabase;

internal class FileSystemRepository
{
    private const int DeleteChunkSize = 500;

    public FileSystemRepository(IOptions<FileSystemOptions> options)
    {
        _options = options;
    }

    private readonly IOptions<FileSystemOptions> _options;

    public async Task<IEnumerable<Node>> GetAllNodesAsync()
    {
        await using var connection = await OpenConnectionAsync();
        var nodes = new List<Node>();
        await using var reader = await connection.ExecuteReaderAsync($@"
SELECT
    [{nameof(Node.Type)}]
FROM []");
        var folderParser = reader.GetRowParser<Node>(typeof(FolderNode));
        var fileParser = reader.GetRowParser<Node>(typeof(FileNode));
        while (await reader.ReadAsync())
        {
            var node = (Node.Types)reader.GetInt32(0) switch
            {
                Node.Types.Folder => folderParser.Invoke(reader),
                Node.Types.File => fileParser.Invoke(reader),
                _ => null,
            };
            if (node is null)
            {
                continue;
            }

            nodes.Add(node);
        }

        return nodes;
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
WHERE [{nameof(Node)}].[{nameof(Node.ParentId)}] in @{nameof(parentIds)}
", new { ids = parentIds });
        }

        // Delete folders and files that has no mapping nodes.
        await connection.ExecuteAsync($@"
DELETE F
FROM [{nameof(FolderNode)}] F
LEFT JOIN [{nameof(Node)}] N
    ON N.[{nameof(Node.Id)}]=F.[{nameof(FolderNode.Id)}]
WHERE N.[{nameof(Node.Id)}] IS NULL;
DELETE F
FROM [{nameof(FileNode)}] F
LEFT JOIN [{nameof(Node)}] N
    ON N.[{nameof(Node.Id)}]=F.[{nameof(FileNode.Id)}]
WHERE N.[{nameof(Node.Id)}] IS NULL;");
    }

    public async Task DeleteFileNodeAsync(Guid nodeId)
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync($@"
DELETE FROM [{nameof(Node)}]
WHERE [{nameof(Node)}].[{nameof(Node.Id)}]=@{nameof(nodeId)};
DELETE FROM [{nameof(FileNode)}]
WHERE [{nameof(FileNode)}].[{nameof(FileNode.Id)}]=@{nameof(nodeId)};
", new { nodeId });
    }

    private async Task<DbConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(_options.Value.DatabaseConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
