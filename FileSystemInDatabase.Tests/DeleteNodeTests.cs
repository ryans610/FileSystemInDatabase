// ReSharper disable CommentTypo
// ReSharper disable ConvertToConstant.Local

using System.Collections.Generic;
using Dapper;

namespace FileSystemInDatabase.Tests;

public class DeleteNodeTests : TestsBase
{
    private IFileSystem _fileSystem;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = Host.Services.GetRequiredService<IFileSystem>();
    }

    [Test]
    public async Task TestDeleteFile()
    {
        // arrange

        // act
        Node nodeAfter;
        IEnumerable<Guid> fileNodeFromDatabaseAfter;
        IEnumerable<Node> nodeInFolderAfter;
        IEnumerable<Guid> nodeInFolderFromDatabaseAfter;
        try
        {
            await _fileSystem.DeleteFileAsync(s_file_a_photo);
            nodeAfter = _fileSystem.GetNodeById(s_file_a_photo);
            fileNodeFromDatabaseAfter = await GetFileIdFromDatabaseAsync(new[] { s_file_a_photo });
            nodeInFolderAfter = _fileSystem.GetNodesUnderFolder(s_folder_UsersPublicPictures);
            nodeInFolderFromDatabaseAfter = await GetNodeIdUnderFolderFromDatabaseAsync(s_folder_UsersPublicPictures);
        }
        catch (Exception)
        {
            throw;
        }

        // assert
        Assert.IsNull(nodeAfter);
        Assert.IsEmpty(fileNodeFromDatabaseAfter);
        Assert.IsEmpty(nodeInFolderAfter);
        Assert.IsEmpty(nodeInFolderFromDatabaseAfter);
    }

    [Test]
    public async Task TestDeleteFolder()
    {
        // arrange
        var exceptedNodeInParentFolderAfter = new[] { s_folder_UsersRyan };
        var exceptedFileIdsAfter = new[]
        {
            s_file_record20220315,
            s_file_bash_history,
            s_file_bash_profile,
        };

        // act
        Node nodeAfter;
        IEnumerable<Guid> nodeIdInParentFolderAfter;
        IEnumerable<Guid> fileIdsAfter;
        try
        {
            await _fileSystem.DeleteFolderAsync(s_folder_UsersPublic);
            nodeAfter = _fileSystem.GetNodeById(s_folder_UsersPublic);
            nodeIdInParentFolderAfter = _fileSystem.GetNodesUnderFolder(s_folder_Users).Select(x => x.Id);
            fileIdsAfter = await GetFileIdFromDatabaseAsync(new[]
            {
                s_file_some_note,
                s_file_some_note2,
                s_file_some_note_3,
                s_file_a_photo,
                s_file_record20220315,
                s_file_bash_history,
                s_file_bash_profile,
            });
        }
        catch (Exception)
        {
            throw;
        }

        // assert
        Assert.IsNull(nodeAfter);
        CollectionAssert.AreEquivalent(exceptedNodeInParentFolderAfter, nodeIdInParentFolderAfter);
        CollectionAssert.AreEquivalent(exceptedFileIdsAfter, fileIdsAfter);
    }

    private async Task<IEnumerable<Guid>> GetFileIdFromDatabaseAsync(IEnumerable<Guid> fileIds)
    {
        await using var connection = await OpenDatabaseConnectionAsync();
        return await connection.QueryAsync<Guid>($@"
SELECT [{nameof(FileNode)}].[{nameof(FileNode.Id)}]
FROM [{nameof(FileNode)}]
WHERE [{nameof(FileNode)}].[{nameof(Node.Id)}] in @{nameof(fileIds)}", new { fileIds });
    }

    private async Task<IEnumerable<Guid>> GetNodeIdUnderFolderFromDatabaseAsync(Guid folderId)
    {
        await using var connection = await OpenDatabaseConnectionAsync();
        return await connection.QueryAsync<Guid>($@"
SELECT [{nameof(Node)}].[{nameof(Node.Id)}]
FROM [{nameof(Node)}]
WHERE [{nameof(Node)}].[{nameof(Node.ParentId)}]=@{nameof(folderId)}", new { folderId });
    }
}
