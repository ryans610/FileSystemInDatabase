using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using Dapper;

// ReSharper disable CommentTypo

namespace FileSystemInDatabase.Tests;

[TestFixture]
public abstract class TestsBase
{
    protected const string DatabaseConnectionString =
        @"Server=localhost,1401;Database=FileSystem;User Id=sa;Password=!qaz2wsx;";

    #region TestDataGuid

    // ReSharper disable InconsistentNaming
    protected static readonly Guid s_folder_C = Guid.Parse("2aa61413-0c8c-447a-99e6-b6981df0b315");

    /// <summary>
    /// C:\Users
    /// </summary>
    protected static readonly Guid s_folder_Users = Guid.Parse("4bedd362-185f-45e0-9b10-33f4a99c6c19");

    /// <summary>
    /// C:\Users\Public
    /// </summary>
    protected static readonly Guid s_folder_UsersPublic = Guid.Parse("6a389c8e-ba2f-423c-9189-9315454fd4b7");

    /// <summary>
    /// C:\Users\Public\Documents
    /// </summary>
    protected static readonly Guid s_folder_UsersPublicDocuments = Guid.Parse("bbf12448-65f5-4115-b99f-d17a4fef2943");

    /// <summary>
    /// C:\Users\Public\Pictures
    /// </summary>
    protected static readonly Guid s_folder_UsersPublicPictures = Guid.Parse("0a7d802a-53f7-4286-915a-7731d2fec53c");

    /// <summary>
    /// C:\Users\Ryan
    /// </summary>
    protected static readonly Guid s_folder_UsersRyan = Guid.Parse("d49772c6-af9d-4bd8-beb4-17fbec56e4b3");

    /// <summary>
    /// C:\Users\Ryan\Pictures
    /// </summary>
    protected static readonly Guid s_folder_UsersRyanPictures = Guid.Parse("025efaae-1127-496c-8f05-e83d390bd6e2");

    /// <summary>
    /// C:\Users\Public\Pictures\a photo.jpg
    /// </summary>
    protected static readonly Guid s_file_a_photo = Guid.Parse("c75a4bd4-dc46-4a51-b5e8-267985d0a6f8");

    /// <summary>
    /// C:\Users\Public\Documents\some_note.txt
    /// </summary>
    protected static readonly Guid s_file_some_note = Guid.Parse("0188e2d7-ea03-4fbe-90f7-b989dad6b173");

    /// <summary>
    /// C:\Users\Public\Documents\some_note2.txt
    /// </summary>
    protected static readonly Guid s_file_some_note2 = Guid.Parse("58e8a31d-58d0-4564-897f-beca0a242b3c");

    /// <summary>
    /// C:\Users\Public\Documents\some_note.3.txt
    /// </summary>
    protected static readonly Guid s_file_some_note_3 = Guid.Parse("7144b84a-e5ef-4b3d-94b7-764414458363");

    /// <summary>
    /// C:\Users\Ryan\Documents\record20220315.docx
    /// </summary>
    protected static readonly Guid s_file_record20220315 = Guid.Parse("e64ee1a2-8370-4852-8ccd-f5d07c6043ea");

    /// <summary>
    /// C:\Users\Ryan\.bash_history
    /// </summary>
    protected static readonly Guid s_file_bash_history = Guid.Parse("46aaffa2-9488-466d-969a-2a0721725dc5");

    /// <summary>
    /// C:\Users\Ryan\.bash_profile
    /// </summary>
    protected static readonly Guid s_file_bash_profile = Guid.Parse("cb720f82-55ef-480f-a779-408bec69e91c");
    // ReSharper restore InconsistentNaming

    #endregion TestDataGuid

    protected IHost Host { get; private set; }

    [OneTimeSetUp]
    public async Task BaseOneTimeSetUpAsync()
    {
        try
        {
            await InitializeDatabaseAsync();
            await InitializeTablesAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    [SetUp]
    public async Task BaseSetupAsync()
    {
        try
        {
            await InitializeDataAsync();

            var builder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder();
            builder.ConfigureServices(services =>
            {
                services.AddFileSystemInDatabase(options =>
                {
                    options.DatabaseConnectionString = DatabaseConnectionString;
                    options.HouseKeepingInterval = TimeSpan.Zero; // ignore house keeping when unit test
                });
            });
            Host = await builder.StartAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    protected async Task<DbConnection> OpenDatabaseConnectionAsync()
    {
        var connection = new SqlConnection(DatabaseConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private Task InitializeDatabaseAsync()
    {
        return ExecuteSqlFromFileAsync("InitializeDatabase.sql");
    }

    private Task InitializeTablesAsync()
    {
        return ExecuteSqlFromFileAsync("InitializeTables.sql");
    }

    private static Task InitializeDataAsync()
    {
        return ExecuteSqlFromFileAsync("InitializeData.sql");
    }

    private static async Task ExecuteSqlFromFileAsync(string fileName)
    {
        var filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, fileName);
        var sql = await File.ReadAllTextAsync(filePath);
        await using var connection = new SqlConnection(@"Server=localhost,1401;User Id=sa;Password=!qaz2wsx;");
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql);
    }
}
