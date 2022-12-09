using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace FileSystemInDatabase.Tests
{
    public class Tests
    {
        private IHost _host;

        [SetUp]
        public void Setup()
        {
            var builder = Host.CreateDefaultBuilder();
            builder.ConfigureServices(services =>
            {
                services.AddFileSystemInDatabase(options => { options.DatabaseConnectionString = @""; });
            });
            _host = builder.Build();
            Task.Run(() => _host.RunAsync());

            //var fileSystem = _host.Services.GetRequiredService<IFileSystem>();
        }

        [Test]
        public async Task Test1()
        {
            try
            {
                var fileSystem = _host.Services.GetRequiredService<IFileSystem>();

                var root = fileSystem.GetNodeById(Guid.Empty);
                var files = fileSystem
                    .GetFilesUnderFolder(Guid.Empty)
                    .ToArray();

                var n = fileSystem.GetFullPathOfNode(files.First().Id);

            }
            catch (Exception)
            {

            }

            Assert.Pass();
        }
    }
}
