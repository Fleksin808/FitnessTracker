using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitnessTracker.Application.Backup;
using FitnessTracker.Domain.Exercises;
using FitnessTracker.Domain.Workouts;
using FitnessTracker.Domain.WorkoutTemplates;
using SQLite;

namespace FitnessTracker.Infrastructure.Persistence;

/// <summary>Mapuje mezi verzovaným souborem JSON a aktuálním schématem SQLite.</summary>
public sealed class SqliteFitnessBackupService(FitnessDatabase database) : IFitnessBackupService
{
    private const int FormatVersion = 2;
    private const int MaximumFileBytes = 25 * 1024 * 1024;
    private const int MaximumRowsPerEntity = 100_000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public async Task<byte[]> ExportAsync()
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        BackupDocument? document = null;
        await connection.RunInTransactionAsync(transaction =>
        {
            // Jedna transakce zajistí, že všechny kolekce odpovídají stejnému okamžiku.
            document = new BackupDocument
            {
                FormatVersion = FormatVersion,
                ExportedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Workouts = transaction.Query<WorkoutRow>("SELECT * FROM Workouts")
                    .Select(row => ToBackup(row.ToWorkout())).OrderBy(row => row.StartedAt, StringComparer.Ordinal).ToList(),
                Exercises = transaction.Query<ExerciseRow>("SELECT * FROM Exercises")
                    .Select(row => ToBackup(row.ToExercise())).OrderBy(row => row.Id, StringComparer.Ordinal).ToList(),
                WorkoutExercises = transaction.Query<WorkoutExerciseRow>("SELECT * FROM WorkoutExercises")
                    .Select(row => ToBackup(row.ToExercise())).OrderBy(row => row.WorkoutId, StringComparer.Ordinal)
                    .ThenBy(row => row.Position).ToList(),
                WorkoutSets = transaction.Query<WorkoutSetRow>("SELECT * FROM WorkoutSets")
                    .Select(row => ToBackup(row.ToSet())).OrderBy(row => row.WorkoutExerciseId, StringComparer.Ordinal)
                    .ThenBy(row => row.Position).ToList(),
                WorkoutTemplates = transaction.Query<WorkoutTemplateRow>("SELECT * FROM WorkoutTemplates")
                    .Select(row => ToBackup(row.ToTemplate(transaction.Query<WorkoutTemplateExerciseRow>(
                        "SELECT * FROM WorkoutTemplateExercises WHERE TemplateId = ? ORDER BY Position", row.Id))))
                    .OrderBy(row => row.Id, StringComparer.Ordinal).ToList(),
                WorkoutTemplateExercises = transaction.Query<WorkoutTemplateExerciseRow>("SELECT * FROM WorkoutTemplateExercises")
                    .Select(row => ToBackup(row.ToExercise())).OrderBy(row => row.TemplateId, StringComparer.Ordinal)
                    .ThenBy(row => row.Position).ToList()
            };
        }).ConfigureAwait(false);

        if (document is null)
            throw new InvalidOperationException("Nepodařilo se sestavit obsah zálohy.");
        if (document.Workouts.Count > MaximumRowsPerEntity || document.Exercises.Count > MaximumRowsPerEntity
            || document.WorkoutExercises.Count > MaximumRowsPerEntity || document.WorkoutSets.Count > MaximumRowsPerEntity
            || document.WorkoutTemplates!.Count > MaximumRowsPerEntity
            || document.WorkoutTemplateExercises!.Count > MaximumRowsPerEntity)
            throw new InvalidOperationException("Záloha obsahuje příliš mnoho záznamů pro formát JSON v2.");

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        if (bytes.Length > MaximumFileBytes)
            throw new InvalidOperationException("Záloha překračuje limit 25 MiB.");
        return bytes;
    }

    public Task<BackupSummary> ValidateAsync(ReadOnlyMemory<byte> json)
    {
        BackupDocument document = ParseAndValidate(json);
        DateTimeOffset exportedAt = ParseUtcTimestamp(document.ExportedAtUtc, "exportedAtUtc");
        return Task.FromResult(new BackupSummary(document.Workouts.Count, document.Exercises.Count,
            document.WorkoutExercises.Count, document.WorkoutSets.Count, document.WorkoutTemplates?.Count ?? 0,
            document.WorkoutTemplateExercises?.Count ?? 0, exportedAt));
    }

    public async Task RestoreAsync(ReadOnlyMemory<byte> json)
    {
        // Validace proběhne celá před otevřením transakce; chybný soubor se databáze nedotkne.
        BackupDocument document = ParseAndValidate(json);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            // Smažeme nejprve tréninky, aby jejich kaskády odstranily návazná data před katalogem.
            transaction.Execute("DELETE FROM Workouts");
            transaction.Execute("DELETE FROM WorkoutTemplates");
            transaction.Execute("DELETE FROM Exercises");

            foreach (var row in document.Exercises)
                transaction.Execute("INSERT INTO Exercises (Id, Name, IsArchived) VALUES (?, ?, ?)",
                    row.Id, row.Name, row.IsArchived ? 1 : 0);
            foreach (var row in document.WorkoutTemplates ?? [])
                transaction.Execute("INSERT INTO WorkoutTemplates (Id, Name) VALUES (?, ?)", row.Id, row.Name);
            foreach (var row in document.WorkoutTemplateExercises ?? [])
                transaction.Execute("INSERT INTO WorkoutTemplateExercises (Id, TemplateId, ExerciseId, Position) VALUES (?, ?, ?, ?)",
                    row.Id, row.TemplateId, row.ExerciseId, row.Position);
            foreach (var row in document.Workouts)
            {
                var workout = ToDomain(row);
                var databaseRow = WorkoutRow.FromWorkout(workout);
                transaction.Execute("INSERT INTO Workouts (Id, Name, StartedAt, FinishedAt) VALUES (?, ?, ?, ?)",
                    databaseRow.Id, databaseRow.Name, databaseRow.StartedAt, databaseRow.FinishedAt);
            }
            foreach (var row in document.WorkoutExercises)
                transaction.Execute("INSERT INTO WorkoutExercises (Id, WorkoutId, ExerciseId, ExerciseNameSnapshot, Position) VALUES (?, ?, ?, ?, ?)",
                    row.Id, row.WorkoutId, row.ExerciseId, row.ExerciseNameSnapshot, row.Position);
            foreach (var row in document.WorkoutSets)
                transaction.Execute("INSERT INTO WorkoutSets (Id, WorkoutExerciseId, Position, Repetitions, LoadGrams) VALUES (?, ?, ?, ?, ?)",
                    row.Id, row.WorkoutExerciseId, row.Position, row.Repetitions, row.LoadGrams);
        }).ConfigureAwait(false);
    }

    private static BackupDocument ParseAndValidate(ReadOnlyMemory<byte> json)
    {
        if (json.Length is 0 or > MaximumFileBytes)
            throw new InvalidDataException("Soubor zálohy je prázdný nebo překračuje limit 25 MiB.");

        BackupDocument document;
        try
        {
            document = JsonSerializer.Deserialize<BackupDocument>(json.Span, JsonOptions)
                ?? throw new InvalidDataException("Soubor neobsahuje JSON zálohu.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Soubor zálohy nemá platný formát JSON v1 nebo v2.", exception);
        }

        if (document.FormatVersion is not (1 or FormatVersion))
            throw new InvalidDataException($"Verze zálohy {document.FormatVersion} není podporována.");
        if (document.Workouts is null || document.Exercises is null
            || document.WorkoutExercises is null || document.WorkoutSets is null)
            throw new InvalidDataException("V záloze chybí povinné pole nebo obsahuje null místo seznamu.");
        if (document.Workouts.Count > MaximumRowsPerEntity || document.Exercises.Count > MaximumRowsPerEntity
            || document.WorkoutExercises.Count > MaximumRowsPerEntity || document.WorkoutSets.Count > MaximumRowsPerEntity)
            throw new InvalidDataException("Záloha obsahuje příliš mnoho záznamů.");

        if (document.FormatVersion == 1 && (document.WorkoutTemplates is not null || document.WorkoutTemplateExercises is not null))
            throw new InvalidDataException("Záloha v1 nesmí obsahovat pole šablon.");
        if (document.FormatVersion == FormatVersion
            && (document.WorkoutTemplates is null || document.WorkoutTemplateExercises is null))
            throw new InvalidDataException("V záloze v2 chybí povinná pole šablon.");
        if ((document.WorkoutTemplates?.Count ?? 0) > MaximumRowsPerEntity
            || (document.WorkoutTemplateExercises?.Count ?? 0) > MaximumRowsPerEntity)
            throw new InvalidDataException("Záloha obsahuje příliš mnoho šablon nebo položek šablon.");

        _ = ParseUtcTimestamp(document.ExportedAtUtc, "exportedAtUtc");
        var workouts = new Dictionary<Guid, Workout>();
        foreach (var row in document.Workouts)
        {
            if (row is null) throw new InvalidDataException("Seznam tréninků obsahuje prázdnou položku.");
            Guid id = ParseId(row.Id, "workout.id");
            if (!workouts.TryAdd(id, ToDomain(row))) throw new InvalidDataException("Záloha obsahuje duplicitní ID tréninku.");
        }

        var exercises = new HashSet<Guid>();
        foreach (var row in document.Exercises)
        {
            if (row is null) throw new InvalidDataException("Seznam cviků obsahuje prázdnou položku.");
            Guid id = ParseId(row.Id, "exercise.id");
            if (!exercises.Add(id)) throw new InvalidDataException("Záloha obsahuje duplicitní ID cviku.");
            _ = Exercise.Restore(id, row.Name, row.IsArchived);
        }

        var workoutExerciseIds = new HashSet<Guid>();
        var exercisePositions = new HashSet<(Guid WorkoutId, int Position)>();
        foreach (var row in document.WorkoutExercises)
        {
            if (row is null) throw new InvalidDataException("Seznam cviků v trénincích obsahuje prázdnou položku.");
            Guid id = ParseId(row.Id, "workoutExercise.id");
            Guid workoutId = ParseId(row.WorkoutId, "workoutExercise.workoutId");
            Guid exerciseId = ParseId(row.ExerciseId, "workoutExercise.exerciseId");
            if (!workouts.ContainsKey(workoutId) || !exercises.Contains(exerciseId))
                throw new InvalidDataException("Záznam cviku odkazuje na neexistující trénink nebo definici cviku.");
            if (!workoutExerciseIds.Add(id)) throw new InvalidDataException("Záloha obsahuje duplicitní ID výskytu cviku.");
            if (!exercisePositions.Add((workoutId, row.Position)))
                throw new InvalidDataException("Záloha obsahuje duplicitní pořadí cviku v tréninku.");
            _ = new WorkoutExercise(id, workoutId, exerciseId, row.ExerciseNameSnapshot, row.Position);
        }

        var setIds = new HashSet<Guid>();
        var setPositions = new HashSet<(Guid WorkoutExerciseId, int Position)>();
        foreach (var row in document.WorkoutSets)
        {
            if (row is null) throw new InvalidDataException("Seznam sérií obsahuje prázdnou položku.");
            Guid id = ParseId(row.Id, "workoutSet.id");
            Guid ownerId = ParseId(row.WorkoutExerciseId, "workoutSet.workoutExerciseId");
            if (!workoutExerciseIds.Contains(ownerId))
                throw new InvalidDataException("Série odkazuje na neexistující výskyt cviku.");
            if (!setIds.Add(id)) throw new InvalidDataException("Záloha obsahuje duplicitní ID série.");
            if (!setPositions.Add((ownerId, row.Position)))
                throw new InvalidDataException("Záloha obsahuje duplicitní pořadí série.");
            _ = new WorkoutSet(id, ownerId, row.Position, row.Repetitions, row.LoadGrams);
        }

        var templateRows = document.WorkoutTemplates ?? [];
        var templateIds = new HashSet<Guid>();
        foreach (var row in templateRows)
        {
            if (row is null) throw new InvalidDataException("Seznam šablon obsahuje prázdnou položku.");
            Guid id = ParseId(row.Id, "workoutTemplate.id");
            if (!templateIds.Add(id)) throw new InvalidDataException("Záloha obsahuje duplicitní ID šablony.");
            if (string.IsNullOrWhiteSpace(row.Name)) throw new InvalidDataException("Název šablony nesmí být prázdný.");
        }

        var templateExerciseIds = new HashSet<Guid>();
        var templatePositions = new HashSet<(Guid TemplateId, int Position)>();
        var templateExercisesByOwner = new Dictionary<Guid, List<WorkoutTemplateExercise>>();
        foreach (var row in document.WorkoutTemplateExercises ?? [])
        {
            if (row is null) throw new InvalidDataException("Seznam položek šablon obsahuje prázdnou položku.");
            Guid id = ParseId(row.Id, "workoutTemplateExercise.id");
            Guid templateId = ParseId(row.TemplateId, "workoutTemplateExercise.templateId");
            Guid exerciseId = ParseId(row.ExerciseId, "workoutTemplateExercise.exerciseId");
            if (!templateIds.Contains(templateId) || !exercises.Contains(exerciseId))
                throw new InvalidDataException("Položka šablony odkazuje na neexistující šablonu nebo cvik.");
            if (!templateExerciseIds.Add(id)) throw new InvalidDataException("Záloha obsahuje duplicitní ID položky šablony.");
            if (!templatePositions.Add((templateId, row.Position)))
                throw new InvalidDataException("Záloha obsahuje duplicitní pořadí cviku v šabloně.");
            var item = new WorkoutTemplateExercise(id, templateId, exerciseId, row.Position);
            if (!templateExercisesByOwner.TryGetValue(templateId, out var ownerItems))
                templateExercisesByOwner[templateId] = ownerItems = [];
            ownerItems.Add(item);
        }

        foreach (var row in templateRows)
        {
            Guid id = ParseId(row.Id, "workoutTemplate.id");
            if (!templateExercisesByOwner.TryGetValue(id, out var items))
                throw new InvalidDataException("Šablona musí obsahovat alespoň jeden cvik.");
            _ = WorkoutTemplate.Restore(id, row.Name, items);
        }

        return document;
    }

    private static Guid ParseId(string? value, string field)
    {
        if (value is null || !Guid.TryParseExact(value, "D", out Guid id)
            || !string.Equals(id.ToString("D"), value, StringComparison.Ordinal))
            throw new InvalidDataException($"Pole {field} neobsahuje kanonické GUID ve formátu D.");
        return id;
    }

    private static DateTimeOffset ParseUtcTimestamp(string? value, string field)
    {
        if (value is null || !DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTimeOffset timestamp) || timestamp.Offset != TimeSpan.Zero)
            throw new InvalidDataException($"Pole {field} neobsahuje čas UTC ve formátu O.");
        return timestamp;
    }

    private static Workout ToDomain(WorkoutBackupRow row)
    {
        DateTimeOffset startedAt = ParseUtcTimestamp(row.StartedAt, "workout.startedAt");
        DateTimeOffset? finishedAt = row.FinishedAt is null
            ? null : ParseUtcTimestamp(row.FinishedAt, "workout.finishedAt");
        return Workout.Restore(ParseId(row.Id, "workout.id"), row.Name, startedAt, finishedAt);
    }

    private static WorkoutBackupRow ToBackup(Workout workout) => new()
    {
        Id = workout.Id.ToString("D"), Name = workout.Name,
        StartedAt = workout.StartedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        FinishedAt = workout.FinishedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
    };

    private static ExerciseBackupRow ToBackup(Exercise exercise) => new()
    {
        Id = exercise.Id.ToString("D"), Name = exercise.Name, IsArchived = exercise.IsArchived
    };

    private static WorkoutExerciseBackupRow ToBackup(WorkoutExercise exercise) => new()
    {
        Id = exercise.Id.ToString("D"), WorkoutId = exercise.WorkoutId.ToString("D"),
        ExerciseId = exercise.ExerciseId.ToString("D"), ExerciseNameSnapshot = exercise.ExerciseNameSnapshot,
        Position = exercise.Position
    };

    private static WorkoutSetBackupRow ToBackup(WorkoutSet set) => new()
    {
        Id = set.Id.ToString("D"), WorkoutExerciseId = set.WorkoutExerciseId.ToString("D"),
        Position = set.Position, Repetitions = set.Repetitions, LoadGrams = set.LoadGrams
    };

    private static WorkoutTemplateBackupRow ToBackup(WorkoutTemplate template) => new()
    {
        Id = template.Id.ToString("D"), Name = template.Name
    };

    private static WorkoutTemplateExerciseBackupRow ToBackup(WorkoutTemplateExercise exercise) => new()
    {
        Id = exercise.Id.ToString("D"), TemplateId = exercise.TemplateId.ToString("D"),
        ExerciseId = exercise.ExerciseId.ToString("D"), Position = exercise.Position
    };

    private sealed class BackupDocument
    {
        public BackupDocument() { }
        public required int FormatVersion { get; init; }
        public required string ExportedAtUtc { get; init; }
        public required List<WorkoutBackupRow> Workouts { get; init; }
        public required List<ExerciseBackupRow> Exercises { get; init; }
        public required List<WorkoutExerciseBackupRow> WorkoutExercises { get; init; }
        public required List<WorkoutSetBackupRow> WorkoutSets { get; init; }
        // Nullable is intentional: older JSON v1 backups do not have these properties.
        public List<WorkoutTemplateBackupRow>? WorkoutTemplates { get; init; }
        public List<WorkoutTemplateExerciseBackupRow>? WorkoutTemplateExercises { get; init; }
    }

    private sealed class WorkoutBackupRow
    {
        public WorkoutBackupRow() { }
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string StartedAt { get; init; }
        public required string? FinishedAt { get; init; }
    }

    private sealed class ExerciseBackupRow
    {
        public ExerciseBackupRow() { }
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required bool IsArchived { get; init; }
    }

    private sealed class WorkoutExerciseBackupRow
    {
        public WorkoutExerciseBackupRow() { }
        public required string Id { get; init; }
        public required string WorkoutId { get; init; }
        public required string ExerciseId { get; init; }
        public required string ExerciseNameSnapshot { get; init; }
        public required int Position { get; init; }
    }

    private sealed class WorkoutSetBackupRow
    {
        public WorkoutSetBackupRow() { }
        public required string Id { get; init; }
        public required string WorkoutExerciseId { get; init; }
        public required int Position { get; init; }
        public required int Repetitions { get; init; }
        public required long? LoadGrams { get; init; }
    }

    private sealed class WorkoutTemplateBackupRow
    {
        public WorkoutTemplateBackupRow() { }
        public required string Id { get; init; }
        public required string Name { get; init; }
    }

    private sealed class WorkoutTemplateExerciseBackupRow
    {
        public WorkoutTemplateExerciseBackupRow() { }
        public required string Id { get; init; }
        public required string TemplateId { get; init; }
        public required string ExerciseId { get; init; }
        public required int Position { get; init; }
    }
}
