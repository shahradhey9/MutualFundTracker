using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GWT.Infrastructure.Data;

/// <summary>
/// Design-time factory used exclusively by EF Core CLI tools (dotnet ef migrations).
/// It reads the connection string from appsettings.Development.json so that the tools
/// do not require Redis or any other infrastructure at migration time.
/// </summary>
public class GwtDbContextFactory : IDesignTimeDbContextFactory<GwtDbContext>
{
    public GwtDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "GWT.Api"))
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=gwt_db;Username=postgres;Password=password";

        var optionsBuilder = new DbContextOptionsBuilder<GwtDbContext>();
        optionsBuilder.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(GwtDbContext).Assembly.FullName));

        return new GwtDbContext(optionsBuilder.Options);
    }
}
