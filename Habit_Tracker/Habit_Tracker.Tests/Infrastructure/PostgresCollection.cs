namespace Habit_Tracker.Tests.Infrastructure;

// xUnit runs classes in different collections in parallel but serializes classes within the
// same collection - putting every Postgres-backed test class here keeps them all sharing one
// container without racing each other for it.
[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres";
}
