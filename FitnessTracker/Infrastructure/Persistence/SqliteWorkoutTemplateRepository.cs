using FitnessTracker.Application.WorkoutTemplates;
using FitnessTracker.Domain.Workouts;
using FitnessTracker.Domain.WorkoutTemplates;
using SQLite;

namespace FitnessTracker.Infrastructure.Persistence;

public sealed class SqliteWorkoutTemplateRepository(FitnessDatabase database) : IWorkoutTemplateRepository
{
    public async Task<IReadOnlyList<WorkoutTemplate>> GetAllAsync()
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        List<WorkoutTemplate> templates = [];
        await connection.RunInTransactionAsync(transaction =>
        {
            var rows = transaction.Query<WorkoutTemplateRow>("SELECT * FROM WorkoutTemplates ORDER BY Name COLLATE NOCASE, Id");
            var exercises = transaction.Query<WorkoutTemplateExerciseRow>(
                "SELECT * FROM WorkoutTemplateExercises ORDER BY TemplateId, Position").ToLookup(row => row.TemplateId);
            templates = rows.Select(row => row.ToTemplate(exercises[row.Id])).ToList();
        }).ConfigureAwait(false);
        return templates.AsReadOnly();
    }

    public async Task AddAsync(WorkoutTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            ValidateActiveExercises(transaction, template);
            if (transaction.ExecuteScalar<int>("SELECT COUNT(*) FROM WorkoutTemplates WHERE Id = ?", template.Id.ToString("D")) > 0)
                throw new InvalidOperationException("Šablona se stejným ID už existuje.");

            transaction.Execute("INSERT INTO WorkoutTemplates (Id, Name) VALUES (?, ?)", template.Id.ToString("D"), template.Name);
            InsertExercises(transaction, template);
        }).ConfigureAwait(false);
    }

    public async Task UpdateAsync(WorkoutTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            if (transaction.ExecuteScalar<int>("SELECT COUNT(*) FROM WorkoutTemplates WHERE Id = ?", template.Id.ToString("D")) == 0)
                throw new KeyNotFoundException("Šablona už není dostupná.");
            ValidateActiveExercises(transaction, template);
            transaction.Execute("UPDATE WorkoutTemplates SET Name = ? WHERE Id = ?", template.Name, template.Id.ToString("D"));
            transaction.Execute("DELETE FROM WorkoutTemplateExercises WHERE TemplateId = ?", template.Id.ToString("D"));
            InsertExercises(transaction, template);
        }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid templateId)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            int deleted = transaction.Execute("DELETE FROM WorkoutTemplates WHERE Id = ?", templateId.ToString("D"));
            if (deleted == 0) throw new KeyNotFoundException("Šablona už není dostupná.");
        }).ConfigureAwait(false);
    }

    public async Task<Guid> CreateWorkoutAsync(Guid templateId, DateTimeOffset startedAt)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        Guid workoutId = Guid.Empty;
        await connection.RunInTransactionAsync(transaction =>
        {
            var template = transaction.Query<WorkoutTemplateRow>(
                "SELECT * FROM WorkoutTemplates WHERE Id = ?", templateId.ToString("D")).SingleOrDefault()
                ?? throw new KeyNotFoundException("Šablona už není dostupná.");
            var plannedRows = transaction.Query<WorkoutTemplateExerciseRow>(
                "SELECT * FROM WorkoutTemplateExercises WHERE TemplateId = ? ORDER BY Position", template.Id);
            var workout = new Workout(template.Name, startedAt);
            var workoutRow = WorkoutRow.FromWorkout(workout);
            transaction.Execute("INSERT INTO Workouts (Id, Name, StartedAt, FinishedAt) VALUES (?, ?, ?, ?)",
                workoutRow.Id, workoutRow.Name, workoutRow.StartedAt, workoutRow.FinishedAt);

            foreach (var planned in plannedRows)
            {
                var exercise = transaction.Query<ExerciseRow>(
                    "SELECT * FROM Exercises WHERE Id = ?", planned.ExerciseId).SingleOrDefault();
                if (exercise is null || exercise.IsArchived)
                    throw new WorkoutTemplateException("Šablona obsahuje archivovaný nebo nedostupný cvik. Obnov ho v katalogu nebo uprav šablonu.");

                // Skutečný trénink dostane nové záznamy a aktuální snímek názvu; plán zůstane samostatný.
                var entry = new WorkoutExercise(Guid.NewGuid(), workout.Id, Guid.ParseExact(exercise.Id, "D"),
                    exercise.Name, planned.Position);
                transaction.Execute("INSERT INTO WorkoutExercises (Id, WorkoutId, ExerciseId, ExerciseNameSnapshot, Position) VALUES (?, ?, ?, ?, ?)",
                    entry.Id.ToString("D"), entry.WorkoutId.ToString("D"), entry.ExerciseId.ToString("D"),
                    entry.ExerciseNameSnapshot, entry.Position);
            }

            workoutId = workout.Id;
        }).ConfigureAwait(false);
        return workoutId;
    }

    private static void ValidateActiveExercises(SQLiteConnection transaction, WorkoutTemplate template)
    {
        foreach (var planned in template.Exercises)
        {
            var exercise = transaction.Query<ExerciseRow>("SELECT * FROM Exercises WHERE Id = ?", planned.ExerciseId.ToString("D"))
                .SingleOrDefault();
            if (exercise is null || exercise.IsArchived)
                throw new WorkoutTemplateException("Šablona může obsahovat pouze cviky z aktivního katalogu.");
        }
    }

    private static void InsertExercises(SQLiteConnection transaction, WorkoutTemplate template)
    {
        foreach (var item in template.Exercises)
            transaction.Execute("INSERT INTO WorkoutTemplateExercises (Id, TemplateId, ExerciseId, Position) VALUES (?, ?, ?, ?)",
                item.Id.ToString("D"), item.TemplateId.ToString("D"), item.ExerciseId.ToString("D"), item.Position);
    }
}
