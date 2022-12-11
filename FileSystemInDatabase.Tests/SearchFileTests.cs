using System.Collections.Generic;

namespace FileSystemInDatabase.Tests;

public class SearchFileTests : TestsBase
{
    private IFileSystem _fileSystem;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = Host.Services.GetRequiredService<IFileSystem>();
    }

    [Test]
    public void TestSearchFilesFromC()
    {
        var exceptedFiles = new[]
        {
            s_file_some_note_3,
            s_file_record20220315,
        };
        TestSearchFiles(s_folder_C, exceptedFiles);
    }

    [Test]
    public void TestSearchFilesFromRyan()
    {
        var exceptedFiles = new[]
        {
            s_file_record20220315,
        };
        TestSearchFiles(s_folder_Ryan, exceptedFiles);
    }

    private void TestSearchFiles(Guid folder, IEnumerable<Guid> exceptedFiles)
    {
        // arrange

        // act
        var searchResult = _fileSystem.SearchFilesUnderFolderAndSubFolders(
            folder,
            x => x.FullName.Contains('3')).ToArray();
        var result = searchResult.Select(x => x.Id);

        // assert
        CollectionAssert.AreEquivalent(exceptedFiles, result);
    }
}
