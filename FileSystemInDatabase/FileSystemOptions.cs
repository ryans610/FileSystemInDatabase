using System.ComponentModel.DataAnnotations;

namespace FileSystemInDatabase;

public record FileSystemOptions
{
    [Required]
    public string DatabaseConnectionString { get; set; }

    /// <summary>
    /// House keeping 程序執行間隔。預設為 10 分鐘。
    /// 如果值為 <see cref="TimeSpan.Zero"/> 則不會執行。
    /// </summary>
    public TimeSpan HouseKeepingInterval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 檢查資料表更新間隔。預設為 10 秒。
    /// </summary>
    public TimeSpan TrackingChangeInterval { get; set; } = TimeSpan.FromSeconds(10);
}
