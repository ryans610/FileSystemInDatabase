// ReSharper disable CommentTypo
// ReSharper disable ConvertToConstant.Local

using Dapper;

namespace FileSystemInDatabase.Tests;

public class MoveNodeTests : TestsBase
{
    private IFileSystem _fileSystem;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = Host.Services.GetRequiredService<IFileSystem>();
    }

    [Test]
    public async Task TestMoveFile()
    {
        // arrange
        var exceptedParentId = s_folder_RyanPictures;

        // act
        Guid parentIdAfterMove;
        Guid parentIdFromDbAfterMove;
        try
        {
            await _fileSystem.MoveNodeToFolderAsync(s_file_some_note2, s_folder_RyanPictures);
            parentIdAfterMove = _fileSystem.GetNodeById(s_file_some_note2).ParentId;
            parentIdFromDbAfterMove = await GetParentIdFromDatabaseAsync(s_file_some_note2);
        }
        catch (Exception)
        {
            throw;
        }

        // assert
        Assert.That(parentIdAfterMove, Is.EqualTo(exceptedParentId));
        Assert.That(parentIdFromDbAfterMove, Is.EqualTo(exceptedParentId));
    }

    [Test]
    public async Task TestMoveFolder()
    {
        // arrange
        var exceptedParentId = s_folder_PublicDocuments;
        // path of this file begin with C:\Users\Ryan\Documents\record20220315.docx
        var exceptedFileRecord20220315FullPath = @"C:\Users\Public\Documents\Ryan\Documents\record20220315.docx";

        // act
        Guid parentIdAfterMove;
        Guid parentIdFromDbAfterMove;
        string fileRecord20220315FullPath;
        try
        {
            await _fileSystem.MoveNodeToFolderAsync(s_folder_Ryan, s_folder_PublicDocuments);
            parentIdAfterMove = _fileSystem.GetNodeById(s_folder_Ryan).ParentId;
            parentIdFromDbAfterMove = await GetParentIdFromDatabaseAsync(s_folder_Ryan);
            fileRecord20220315FullPath = _fileSystem.GetFullPathOfNode(s_file_record20220315);
        }
        catch (Exception)
        {
            throw;
        }

        // assert
        Assert.That(parentIdAfterMove, Is.EqualTo(exceptedParentId));
        Assert.That(parentIdFromDbAfterMove, Is.EqualTo(exceptedParentId));
        Assert.That(exceptedFileRecord20220315FullPath, Is.EqualTo(fileRecord20220315FullPath));
    }

    private async Task<Guid> GetParentIdFromDatabaseAsync(Guid nodeId)
    {
        await using var connection = await OpenDatabaseConnectionAsync();
        return await connection.ExecuteScalarAsync<Guid>($@"
SELECT [{nameof(Node)}].[{nameof(Node.ParentId)}]
FROM [{nameof(Node)}]
WHERE [{nameof(Node)}].[{nameof(Node.Id)}]=@{nameof(nodeId)}", new { nodeId });
    }
}
