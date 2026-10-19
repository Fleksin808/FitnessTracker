using System.Globalization;
using FitnessTracker.Application.Workouts;
using FitnessTracker.Domain.Exercises;
using FitnessTracker.Domain.Workouts;
using FitnessTracker.Infrastructure.Persistence;
using SQLite;

internal static class WorkoutLogChecks
{
    public static async Task RunAsync(string directory)
    {
        var database = new FitnessDatabase(Path.Combine(directory, "recording.db3"));
        var workouts = new SqliteWorkoutRepository(database);
        var catalog = new SqliteExerciseRepository(database);
        var recording = new SqliteWorkoutLogRepository(database);
        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var workout = new Workout("Celý trénink", start);
        var exercise = new Exercise("Bench press");
        await workouts.AddAsync(workout);
        await catalog.AddAsync(exercise);
        Check(await recording.GetAsync(Guid.NewGuid()) is null, "Chybějící trénink není null.");
        await ExpectAsync<KeyNotFoundException>(() => recording.AddExerciseAsync(Guid.NewGuid(), exercise.Id));
        await recording.AddExerciseAsync(workout.Id, exercise.Id);
        var initial = (await recording.GetAsync(workout.Id))!;
        Guid firstId = initial.Exercises.Single().Exercise.Id;
        await catalog.UpdateAsync(exercise.WithName("Nový název").WithArchived(true));
        await ExpectAsync<WorkoutRecordingException>(() => recording.AddExerciseAsync(workout.Id, exercise.Id));
        await recording.AddSetAsync(firstId, 8, 62500);
        await recording.AddSetAsync(firstId, 10, null);
        await recording.AddSetAsync(firstId, 12, 0);
        await ExpectAsync<ArgumentOutOfRangeException>(() => recording.AddSetAsync(firstId, 0, 1));
        await ExpectAsync<ArgumentOutOfRangeException>(() => recording.AddSetAsync(firstId, 1, -1));
        var snapshot = (await recording.GetAsync(workout.Id))!;
        Check(snapshot.Exercises.Single().Exercise.ExerciseNameSnapshot == "Bench press", "Přejmenování změnilo historii.");
        Check(snapshot.Exercises[0].Sets.Count == 3 && snapshot.Exercises[0].Sets[0].LoadGrams == 62500
            && snapshot.Exercises[0].Sets[1].LoadGrams is null && snapshot.Exercises[0].Sets[2].LoadGrams == 0,
            "Neplatná série se uložila nebo se ztratila přesnost či význam null/0.");
        await catalog.UpdateAsync(exercise.WithName("Nový název"));
        await recording.AddExerciseAsync(workout.Id, exercise.Id);
        var second = (await recording.GetAsync(workout.Id))!.Exercises[1].Exercise;
        Check(second.Id != firstId && second.ExerciseNameSnapshot == "Nový název", "Opakovaný výskyt cviku ztratil vlastní identitu nebo název.");
        await recording.MoveExerciseAsync(firstId, -1);
        await recording.MoveExerciseAsync(second.Id, 1);
        await recording.MoveExerciseAsync(firstId, 1);
        var swapped = (await recording.GetAsync(workout.Id))!;
        Check(swapped.Exercises[0].Exercise.Id == second.Id && swapped.Exercises[1].Exercise.Id == firstId,
            "Prohození cviků selhalo.");
        Check(initial.Exercises[0].Exercise.Position == 1 && initial.Exercises[0].Sets.Count == 0, "Změnil se dřívější snímek.");
        var setId = snapshot.Exercises[0].Sets[1].Id;
        await recording.MoveSetAsync(setId, -1);
        var reordered = (await recording.GetAsync(workout.Id))!.Exercises.Single(x => x.Exercise.Id == firstId);
        Check(reordered.Sets[0].Id == setId && reordered.Sets[0].Position == 1, "Prohození sérií selhalo.");
        await recording.MoveSetAsync(setId, -1);
        await ExpectAsync<ArgumentOutOfRangeException>(() => recording.MoveSetAsync(setId, 0));

        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => recording.AddSetAsync(firstId, 5, 1000)));
        reordered = (await recording.GetAsync(workout.Id))!.Exercises.Single(x => x.Exercise.Id == firstId);
        Check(reordered.Sets.Select(set => set.Position).SequenceEqual(Enumerable.Range(1, 15)), "Souběžné zápisy porušily pořadí sérií.");

        var connection = await database.GetConnectionAsync();
        await connection.ExecuteAsync("""
            CREATE TRIGGER FailReorder BEFORE UPDATE OF Position ON WorkoutExercises
            WHEN NEW.Position = 1 BEGIN SELECT RAISE(ABORT, 'test rollback'); END
            """);
        await ExpectAsync<SQLiteException>(() => recording.MoveExerciseAsync(second.Id, 1));
        var afterRollback = (await recording.GetAsync(workout.Id))!;
        Check(afterRollback.Exercises[0].Exercise.Id == second.Id && afterRollback.Exercises[1].Exercise.Position == 2,
            "Neúspěšné prohození ponechalo dočasné pořadí.");
        await connection.ExecuteAsync("DROP TRIGGER FailReorder");
        await ExpectAsync<SQLiteException>(() => connection.ExecuteAsync("DELETE FROM Exercises WHERE Id = ?", exercise.Id.ToString("D")));
        await ExpectAsync<WorkoutRecordingException>(() => recording.FinishAsync(workout.Id, start.AddSeconds(-1)));
        var end = start.AddMinutes(45);
        await recording.FinishAsync(workout.Id, end);
        await recording.FinishAsync(workout.Id, end.AddMinutes(1));
        Check((await recording.GetAsync(workout.Id))!.Workout.FinishedAt == end, "Opakované ukončení přepsalo konec.");
        Check((await workouts.GetByIdAsync(workout.Id))!.IsFinished, "Běžné úložiště tréninků nečte ukončení.");
        await ExpectAsync<WorkoutRecordingException>(() => recording.AddExerciseAsync(workout.Id, exercise.Id));
        await ExpectAsync<WorkoutRecordingException>(() => recording.AddSetAsync(firstId, 1, null));
        await ExpectAsync<WorkoutRecordingException>(() => recording.MoveExerciseAsync(firstId, -1));
        await ExpectAsync<WorkoutRecordingException>(() => recording.MoveSetAsync(setId, 1));

        // Budoucí smazání tréninku musí odstranit jen jeho záznamy, nikoliv katalog cviků.
        await connection.ExecuteAsync("DELETE FROM Workouts WHERE Id = ?", workout.Id.ToString("D"));
        Check(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM WorkoutSets") == 0
            && await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM WorkoutExercises") == 0
            && await catalog.GetByIdAsync(exercise.Id) is not null, "Kaskáda porušila integritu dat.");

        string oldPath = Path.Combine(directory, "version2.db3");
        using (var old = new SQLiteConnection(oldPath))
        {
            CreateVersion2(old);
            old.Execute("INSERT INTO Workouts VALUES (?, ?, ?)", workout.Id.ToString("D"), workout.Name, start.ToString("O", CultureInfo.InvariantCulture));
            old.Execute("INSERT INTO Exercises VALUES (?, ?, ?)", exercise.Id.ToString("D"), exercise.Name, 1);
        }
        var upgraded = new FitnessDatabase(oldPath);
        var oldLog = await new SqliteWorkoutLogRepository(upgraded).GetAsync(workout.Id);
        Check(oldLog is not null && !oldLog.Workout.IsFinished && oldLog.Exercises.Count == 0, "Migrace 2 → 3 změnila původní trénink.");
        Check((await new SqliteExerciseRepository(upgraded).GetByIdAsync(exercise.Id))!.IsArchived, "Migrace změnila katalog.");

        string badPath = Path.Combine(directory, "migration3-failure.db3");
        using (var old = new SQLiteConnection(badPath))
        {
            CreateVersion2(old);
            old.Execute("CREATE TABLE WorkoutSets (Marker TEXT)");
            old.Execute("INSERT INTO WorkoutSets VALUES ('zachovat')");
        }
        await ExpectAsync<SQLiteException>(async () => { await new SqliteWorkoutLogRepository(new FitnessDatabase(badPath)).GetAsync(workout.Id); });
        using (var old = new SQLiteConnection(badPath))
        {
            Check(old.ExecuteScalar<int>("PRAGMA user_version") == 2, "Neúspěšná migrace 3 změnila verzi.");
            Check(old.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Workouts') WHERE name = 'FinishedAt'") == 0,
                "Neúspěšná migrace ponechala přidaný sloupec.");
            Check(old.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'WorkoutExercises'") == 0,
                "Neúspěšná migrace ponechala část nového schématu.");
            Check(old.ExecuteScalar<string>("SELECT Marker FROM WorkoutSets") == "zachovat", "Neúspěšná migrace ztratila data.");
        }
        Console.WriteLine("PASS LOG: kopie názvu, série, přesné gramy/null/0, opakovaný cvik, pořadí, rollback přesunu, souběh, ukončení, FK/kaskády, migrace 2 → 3 a její rollback.");
    }

    private static void CreateVersion2(SQLiteConnection connection)
    {
        connection.Execute("CREATE TABLE Workouts (Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, StartedAt TEXT NOT NULL)");
        connection.Execute("CREATE TABLE Exercises (Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, IsArchived INTEGER NOT NULL)");
        connection.Execute("PRAGMA user_version = 2");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static async Task ExpectAsync<TException>(Func<Task> operation) where TException : Exception
    {
        try { await operation(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Chybí výjimka {typeof(TException).Name}.");
    }
}
