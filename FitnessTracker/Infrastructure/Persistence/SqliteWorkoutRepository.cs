using FitnessTracker.Application.Workouts;
using FitnessTracker.Domain.Workouts;

namespace FitnessTracker.Infrastructure.Persistence;

public sealed class SqliteWorkoutRepository : IWorkoutRepository
{
    private readonly FitnessDatabase database;

    public SqliteWorkoutRepository(FitnessDatabase database)
    {
        this.database = database;
    }

    public async Task AddAsync(Workout workout)
    {
        ArgumentNullException.ThrowIfNull(workout);
        var row = WorkoutRow.FromWorkout(workout);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);

        await connection.RunInTransactionAsync(transaction =>
        {
            // Kontrola i zápis jsou atomické. Duplicitní ID nepřepíše původní záznam.
            if (transaction.ExecuteScalar<int>("SELECT COUNT(*) FROM Workouts WHERE Id = ?", row.Id) > 0)
                throw new InvalidOperationException("Trénink se stejným ID už existuje.");

            // Parametry oddělují hodnoty od SQL, takže apostrofy v názvu nejsou příkaz.
            transaction.Execute("INSERT INTO Workouts (Id, Name, StartedAt, FinishedAt) VALUES (?, ?, ?, ?)",
                row.Id, row.Name, row.StartedAt, row.FinishedAt);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Workout>> GetAllAsync()
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        var rows = await connection.QueryAsync<WorkoutRow>(
            "SELECT Id, Name, StartedAt, FinishedAt FROM Workouts").ConfigureAwait(false);

        // Doménové typy řadíme stejným porovnáním jako úložiště v paměti.
        // Vracíme samostatný snímek pouze pro čtení.
        return rows.Select(row => row.ToWorkout())
            .OrderByDescending(workout => workout.StartedAt)
            .ThenBy(workout => workout.Id)
            .ToList()
            .AsReadOnly();
    }

    public async Task<Workout?> GetByIdAsync(Guid id)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        var rows = await connection.QueryAsync<WorkoutRow>(
            "SELECT Id, Name, StartedAt, FinishedAt FROM Workouts WHERE Id = ?", id.ToString("D"))
            .ConfigureAwait(false);

        return rows.Count == 0 ? null : rows[0].ToWorkout();
    }
}
