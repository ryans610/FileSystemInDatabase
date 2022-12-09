using System.ComponentModel.DataAnnotations;

namespace FileSystemInDatabase;

public record FileSystemOptions
{
    [Required]
    public string DatabaseConnectionString { get; set; }
}
