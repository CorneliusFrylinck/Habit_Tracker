using Habit_Tracker.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Habit_Tracker.Tests.Infrastructure;

// One real Postgres container shared across every test in the "Postgres" collection - the
// Dapper queries use Postgres-specific syntax (LEAST, FILTER, ::int, ANY(array)) that an
// in-memory or SQLite provider can't stand in for.
public class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("blazorapp_test")
        .WithUsername("blazorapp_test")
        .WithPassword("blazorapp_test")
        .Build();

    private string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await container.DisposeAsync();

    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    public NpgsqlDataSource CreateDataSource() => NpgsqlDataSource.Create(ConnectionString);
}
