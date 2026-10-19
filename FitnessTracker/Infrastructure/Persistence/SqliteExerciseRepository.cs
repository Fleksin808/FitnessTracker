using FitnessTracker.Application.Exercises;
using FitnessTracker.Domain.Exercises;

namespace FitnessTracker.Infrastructure.Persistence;

public sealed class SqliteExerciseRepository : IExerciseRepository
{
    private readonly FitnessDatabase database;

    public SqliteExerciseRepository(FitnessDatabase database) => this.database = database;

    public async Task AddAsync(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        var row = ExerciseRow.FromExercise(exercise);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            if (transaction.ExecuteScalar<int>("SELECT COUNT(*) FROM Exercises WHERE Id = ?", row.Id) > 0)
                throw new InvalidOperationException("Cvik se stejným ID už existuje.");

            // Parametry chrání dotaz i při apostrofech v názvu; stav je uložen jako 0 nebo 1.
            transaction.Execute("INSERT INTO Exercises (Id, Name, IsArchived) VALUES (?, ?, ?)",
                row.Id, row.Name, row.IsArchived ? 1 : 0);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Exercise>> GetAllAsync(bool includeArchived = false)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        // Archivaci filtrujeme už v databázi. Konkrétní pořadí má stejný význam na všech platformách.
        var rows = await connection.QueryAsync<ExerciseRow>(includeArchived
            ? "SELECT Id, Name, IsArchived FROM Exercises"
            : "SELECT Id, Name, IsArchived FROM Exercises WHERE IsArchived = 0").ConfigureAwait(false);
        return rows.Select(row => row.ToExercise())
            .OrderBy(exercise => exercise.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(exercise => exercise.Id)
            .ToList().AsReadOnly();
    }

    public async Task<Exercise?> GetByIdAsync(Guid id)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        var rows = await connection.QueryAsync<ExerciseRow>(
            "SELECT Id, Name, IsArchived FROM Exercises WHERE Id = ?", id.ToString("D")).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0].ToExercise();
    }

    public async Task UpdateAsync(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        var row = ExerciseRow.FromExercise(exercise);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        // UPDATE nepoužívá upsert: překlep v ID nesmí potichu vytvořit jiný cvik.
        int changed = await connection.ExecuteAsync(
            "UPDATE Exercises SET Name = ?, IsArchived = ? WHERE Id = ?",
            row.Name, row.IsArchived ? 1 : 0, row.Id).ConfigureAwait(false);
        if (changed == 0)
            throw new KeyNotFoundException("Cvik už není dostupný. Obnov seznam.");
    }
}
