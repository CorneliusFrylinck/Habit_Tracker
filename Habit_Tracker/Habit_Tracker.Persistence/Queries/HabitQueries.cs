using Habit_Tracker.Application.DTOs;
using Habit_Tracker.Application.Queries;
using Dapper;
using Npgsql;

namespace Habit_Tracker.Persistence.Queries;

public class HabitQueries(NpgsqlDataSource dataSource) : IHabitQueries
{
    private const string HabitsSql = """
        SELECT
            h."Id",
            h."Name",
            h."IsCompletable",
            h."CompletionMethod",
            h."TargetValue",
            h."Unit",
            h."UnitPlural",
            h."TargetType",
            h."ParentHabitId",
            h."CreatedAt",
            COALESCE(
                CASE
                    WHEN h."CompletionMethod" = 1 AND h."TargetValue" > 0 AND h."TargetType" = 1
                        THEN ROUND(AVG(LEAST(100.0, 100.0 * e."Amount" / h."TargetValue")) FILTER (WHERE e."Amount" IS NOT NULL))::int
                    WHEN h."CompletionMethod" = 1 AND h."TargetValue" > 0
                        THEN LEAST(100, ROUND(100.0 * COALESCE(SUM(e."Amount"), 0) / h."TargetValue"))::int
                    ELSE ROUND(100.0 * COUNT(*) FILTER (WHERE e."IsCompleted") / NULLIF(COUNT(e."Id"), 0))::int
                END,
                0
            ) AS "CompletionPercentage"
        FROM "Habits" h
        LEFT JOIN "HabitEntries" e ON e."HabitId" = h."Id"
        WHERE h."UserId" = @UserId
        GROUP BY h."Id", h."Name", h."IsCompletable", h."CompletionMethod", h."TargetValue", h."Unit", h."UnitPlural", h."TargetType", h."ParentHabitId", h."CreatedAt"
        ORDER BY h."CreatedAt"
        """;

    private const string EntriesForHabitsSql = """
        SELECT "Id", "HabitId", "TrackedAt", "IsCompleted", "Amount"
        FROM "HabitEntries"
        WHERE "HabitId" = ANY(@HabitIds)
        ORDER BY "TrackedAt" DESC
        """;

    public async Task<IReadOnlyList<HabitDto>> GetHabitsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(HabitsSql, new { UserId = userId }, cancellationToken: cancellationToken);
        var habits = await connection.QueryAsync<HabitDto>(command);
        return habits.AsList();
    }

    public async Task<HabitTreeNodeDto?> GetHabitTreeAsync(Guid habitId, Guid userId, CancellationToken cancellationToken = default)
    {
        var allHabits = await GetHabitsForUserAsync(userId, cancellationToken);
        var habitsById = allHabits.ToDictionary(h => h.Id);

        if (!habitsById.TryGetValue(habitId, out var rootDto))
        {
            return null;
        }

        var childrenByParent = allHabits
            .Where(h => h.ParentHabitId is not null)
            .ToLookup(h => h.ParentHabitId!.Value);

        var root = BuildNode(rootDto, childrenByParent);

        var leafIds = new List<Guid>();
        CollectLeafIds(root, leafIds);

        if (leafIds.Count > 0)
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var command = new CommandDefinition(EntriesForHabitsSql, new { HabitIds = leafIds }, cancellationToken: cancellationToken);
            var entries = await connection.QueryAsync<HabitEntryDto>(command);
            AttachEntries(root, entries.ToLookup(e => e.HabitId));
        }

        return root;
    }

    private static HabitTreeNodeDto BuildNode(HabitDto dto, ILookup<Guid, HabitDto> childrenByParent)
    {
        var node = new HabitTreeNodeDto
        {
            Id = dto.Id,
            Name = dto.Name,
            IsCompletable = dto.IsCompletable,
            CompletionMethod = dto.CompletionMethod,
            TargetValue = dto.TargetValue,
            Unit = dto.Unit,
            UnitPlural = dto.UnitPlural,
            TargetType = dto.TargetType,
            CompletionPercentage = dto.CompletionPercentage,
        };

        foreach (var child in childrenByParent[dto.Id])
        {
            node.SubHabits.Add(BuildNode(child, childrenByParent));
        }

        return node;
    }

    private static void CollectLeafIds(HabitTreeNodeDto node, List<Guid> leafIds)
    {
        if (node.SubHabits.Count == 0)
        {
            leafIds.Add(node.Id);
            return;
        }

        foreach (var child in node.SubHabits)
        {
            CollectLeafIds(child, leafIds);
        }
    }

    private static void AttachEntries(HabitTreeNodeDto node, ILookup<Guid, HabitEntryDto> entriesByHabit)
    {
        if (node.SubHabits.Count == 0)
        {
            node.Entries = entriesByHabit[node.Id].ToList();
            return;
        }

        foreach (var child in node.SubHabits)
        {
            AttachEntries(child, entriesByHabit);
        }
    }
}
