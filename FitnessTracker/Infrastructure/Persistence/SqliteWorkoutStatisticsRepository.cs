using FitnessTracker.Application.Statistics;
using SQLite;

namespace FitnessTracker.Infrastructure.Persistence;

public sealed class SqliteWorkoutStatisticsRepository(FitnessDatabase database) : IWorkoutStatisticsRepository
{
    public async Task<WorkoutStatisticsSnapshot> GetAllTimeAsync()
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        WorkoutStatisticsSnapshot? result = null;
        await connection.RunInTransactionAsync(transaction =>
        {
            int workoutCount = transaction.ExecuteScalar<int>("SELECT COUNT(*) FROM Workouts");
            int finishedCount = transaction.ExecuteScalar<int>("SELECT COUNT(*) FROM Workouts WHERE FinishedAt IS NOT NULL");
            var sets = transaction.Query<WorkoutSetRow>("SELECT * FROM WorkoutSets");

            // Neznámou zátěž (null) z objemu vynecháme; výslovná nula se počítá jako nula.
            decimal volume = sets.Aggregate(0m, (sum, set) => set.LoadGrams is { } grams
                ? checked(sum + grams / 1000m * set.Repetitions)
                : sum);
            result = new WorkoutStatisticsSnapshot(workoutCount, finishedCount, sets.Count, volume);
        }).ConfigureAwait(false);

        return result ?? throw new InvalidOperationException("Nepodařilo se načíst statistiky tréninků.");
    }
}
