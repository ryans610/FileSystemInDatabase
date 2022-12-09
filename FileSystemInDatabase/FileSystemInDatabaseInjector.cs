using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FileSystemInDatabase;

public static class FileSystemInDatabaseInjector
{
    [PublicAPI]
    public static IServiceCollection AddFileSystemInDatabase(
        this IServiceCollection services,
        Action<FileSystemOptions> configureOptions)
    {
        services.AddOptions<FileSystemOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations();
        services.AddSingleton<FileSystemRepository>();

        services.AddHostedService<FileSystem>();
        services.AddSingleton<IFileSystem>(s => s
            .GetServices<IHostedService>()
            .OfType<FileSystem>()
            .First());

        return services;
    }
}
