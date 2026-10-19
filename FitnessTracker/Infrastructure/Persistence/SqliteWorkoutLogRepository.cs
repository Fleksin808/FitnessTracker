using System.Globalization;
using FitnessTracker.Application.Workouts;
using FitnessTracker.Domain.Workouts;
using SQLite;

namespace FitnessTracker.Infrastructure.Persistence;

public sealed class SqliteWorkoutLogRepository(FitnessDatabase database) : IWorkoutLogRepository
{
    public async Task<WorkoutLog?> GetAsync(Guid workoutId)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        WorkoutLog? result = null;
        await connection.RunInTransactionAsync(transaction =>
        {
            var workout = transaction.Query<WorkoutRow>("SELECT * FROM Workouts WHERE Id = ?", workoutId.ToString("D")).SingleOrDefault();
            if (workout is null)
                return;
            var exercises = transaction.Query<WorkoutExerciseRow>(
                "SELECT * FROM WorkoutExercises WHERE WorkoutId = ? ORDER BY Position", workout.Id);
            var sets = transaction.Query<WorkoutSetRow>("""
                SELECT s.* FROM WorkoutSets s
                JOIN WorkoutExercises e ON s.WorkoutExerciseId = e.Id
                WHERE e.WorkoutId = ? ORDER BY s.Position
                """, workout.Id).ToLookup(row => row.WorkoutExerciseId);
            // Všechny části pocházejí ze stejného okamžiku, nikoliv z několika nezávislých čtení.
            result = new WorkoutLog(workout.ToWorkout(), exercises.Select(row =>
                new WorkoutExerciseLog(row.ToExercise(), sets[row.Id].Select(set => set.ToSet()))));
        }).ConfigureAwait(false);
        return result;
    }

    public async Task AddExerciseAsync(Guid workoutId, Guid exerciseId)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            RequireOpenWorkout(transaction, workoutId.ToString("D"));
            var exercise = transaction.Query<ExerciseRow>("SELECT * FROM Exercises WHERE Id = ?", exerciseId.ToString("D")).SingleOrDefault();
            if (exercise is null || exercise.IsArchived)
                throw new WorkoutRecordingException("Cvik už není v aktivním katalogu. Obnov nabídku cviků.");
            int position = checked(transaction.ExecuteScalar<int>(
                "SELECT COALESCE(MAX(Position), 0) FROM WorkoutExercises WHERE WorkoutId = ?", workoutId.ToString("D")) + 1);
            var entry = new WorkoutExercise(Guid.NewGuid(), workoutId, exerciseId, exercise.Name, position);
            transaction.Execute("INSERT INTO WorkoutExercises VALUES (?, ?, ?, ?, ?)",
                entry.Id.ToString("D"), entry.WorkoutId.ToString("D"), entry.ExerciseId.ToString("D"), entry.ExerciseNameSnapshot, entry.Position);
        }).ConfigureAwait(false);
    }

    public async Task AddSetAsync(Guid workoutExerciseId, int repetitions, long? loadGrams)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            var exercise = RequireExercise(transaction, workoutExerciseId);
            int position = checked(transaction.ExecuteScalar<int>(
                "SELECT COALESCE(MAX(Position), 0) FROM WorkoutSets WHERE WorkoutExerciseId = ?", exercise.Id) + 1);
            var set = new WorkoutSet(Guid.NewGuid(), workoutExerciseId, position, repetitions, loadGrams);
            transaction.Execute("INSERT INTO WorkoutSets VALUES (?, ?, ?, ?, ?)",
                set.Id.ToString("D"), exercise.Id, set.Position, set.Repetitions, set.LoadGrams);
        }).ConfigureAwait(false);
    }

    public async Task MoveExerciseAsync(Guid workoutExerciseId, int direction)
    {
        ValidateDirection(direction);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            var exercise = RequireExercise(transaction, workoutExerciseId);
            SwapPositions(transaction, "WorkoutExercises", "WorkoutId", exercise.Id, exercise.WorkoutId, exercise.Position, direction);
        }).ConfigureAwait(false);
    }

    public async Task MoveSetAsync(Guid setId, int direction)
    {
        ValidateDirection(direction);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            var set = transaction.Query<WorkoutSetRow>("SELECT * FROM WorkoutSets WHERE Id = ?", setId.ToString("D")).SingleOrDefault()
                ?? throw new KeyNotFoundException("Série není dostupná.");
            RequireExercise(transaction, Guid.ParseExact(set.WorkoutExerciseId, "D"));
            SwapPositions(transaction, "WorkoutSets", "WorkoutExerciseId", set.Id, set.WorkoutExerciseId, set.Position, direction);
        }).ConfigureAwait(false);
    }

    public async Task FinishAsync(Guid workoutId, DateTimeOffset finishedAt)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            var row = transaction.Query<WorkoutRow>("SELECT * FROM Workouts WHERE Id = ?", workoutId.ToString("D")).SingleOrDefault()
                ?? throw new KeyNotFoundException("Trénink není dostupný.");
            var workout = row.ToWorkout();
            if (workout.IsFinished)
                return;
            if (finishedAt < workout.StartedAt)
                throw new WorkoutRecordingException("Konec tréninku nesmí předcházet začátku. Zkontroluj čas zařízení.");
            var finished = workout.Finish(finishedAt);
            transaction.Execute("UPDATE Workouts SET FinishedAt = ? WHERE Id = ?",
                finished.FinishedAt!.Value.ToString("O", CultureInfo.InvariantCulture), row.Id);
        }).ConfigureAwait(false);
    }

    private static Workout RequireOpenWorkout(SQLiteConnection transaction, string workoutId)
    {
        var row = transaction.Query<WorkoutRow>("SELECT * FROM Workouts WHERE Id = ?", workoutId).SingleOrDefault()
            ?? throw new KeyNotFoundException("Trénink není dostupný.");
        var workout = row.ToWorkout();
        if (workout.IsFinished)
            throw new WorkoutRecordingException("Trénink už je ukončený. Další záznam do něj není povolen.");
        return workout;
    }

    private static WorkoutExerciseRow RequireExercise(SQLiteConnection transaction, Guid id)
    {
        var exercise = transaction.Query<WorkoutExerciseRow>("SELECT * FROM WorkoutExercises WHERE Id = ?", id.ToString("D")).SingleOrDefault()
            ?? throw new KeyNotFoundException("Zaznamenaný cvik není dostupný.");
        RequireOpenWorkout(transaction, exercise.WorkoutId);
        return exercise;
    }

    private static void ValidateDirection(int direction)
    {
        if (direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(direction));
    }

    private static void SwapPositions(SQLiteConnection transaction, string table, string parentColumn,
        string id, string parentId, int position, int direction)
    {
        // Identifikátory tabulek jsou výhradně pevné interní hodnoty, uživatelská data jsou parametry.
        string? neighborId = transaction.ExecuteScalar<string>(
            $"SELECT Id FROM {table} WHERE {parentColumn} = ? AND Position = ?", parentId, checked(position + direction));
        if (neighborId is null)
            return;
        int temporary = checked(transaction.ExecuteScalar<int>(
            $"SELECT MAX(Position) FROM {table} WHERE {parentColumn} = ?", parentId) + 1);
        // Volná kladná pozice dovolí prohození bez porušení UNIQUE i CHECK během transakce.
        transaction.Execute($"UPDATE {table} SET Position = ? WHERE Id = ?", temporary, id);
        transaction.Execute($"UPDATE {table} SET Position = ? WHERE Id = ?", position, neighborId);
        transaction.Execute($"UPDATE {table} SET Position = ? WHERE Id = ?", checked(position + direction), id);
    }
}
