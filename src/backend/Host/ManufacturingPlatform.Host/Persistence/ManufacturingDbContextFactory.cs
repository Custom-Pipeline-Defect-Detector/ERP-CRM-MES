using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ManufacturingPlatform.Host.Persistence;

public sealed class ManufacturingDbContextFactory : IDesignTimeDbContextFactory<ManufacturingDbContext>
{
    public ManufacturingDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveProjectPath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = configuration.GetSection("Manufacturing").Get<ManufacturingOptions>() ?? new ManufacturingOptions();
        var builder = new DbContextOptionsBuilder<ManufacturingDbContext>();
        var provider = options.Database.Provider.Trim();
        var connectionString = options.Database.ConnectionString;

        if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            builder.UseSqlServer(connectionString);
        }
        else
        {
            builder.UseSqlite(connectionString);
        }

        return new ManufacturingDbContext(builder.Options);
    }

    private static string ResolveProjectPath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        if (File.Exists(Path.Combine(currentDirectory, "appsettings.json")))
        {
            return currentDirectory;
        }

        var projectPath = Path.Combine(currentDirectory, "src", "backend", "Host", "ManufacturingPlatform.Host");
        if (File.Exists(Path.Combine(projectPath, "appsettings.json")))
        {
            return projectPath;
        }

        return currentDirectory;
    }
}
