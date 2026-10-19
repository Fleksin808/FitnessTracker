using System.Globalization;
using FitnessTracker.Domain.Exercises;
using FitnessTracker.Infrastructure.Persistence;
using SQLite;

internal static class ExerciseChecks
{
    public static async Task RunAsync(string directory)
    {
        foreach (var name in new string?[] { null, "", "  ", "\t\n" })
            Expect<ArgumentException>(() => new Exercise(name!));
        Expect<ArgumentException>(() => Exercise.Restore(Guid.Empty, "Dřep", false));
        var original = new Exercise("  Dřep  ");
        var renamed = original.WithName("Dřep s osou");
        Check(original.Name == "Dřep" && renamed.Name == "Dřep s osou" && renamed.Id == original.Id,
            "Změna názvu porušila identitu nebo původní snímek.");
        Expect<ArgumentException>(() => original.WithName(" "));

        var database = new FitnessDatabase(Path.Combine(directory, "exercises.db3"));
        var repository = new SqliteExerciseRepository(database);
        Check((await repository.GetAllAsync()).Count == 0, "Nový katalog není prázdný.");
        await repository.AddAsync(original);
        var beforeChange = await repository.GetAllAsync();
        await repository.UpdateAsync(renamed);
        Check(beforeChange[0].Name == "Dřep", "Uložená změna upravila dřívější snímek.");
        Check((await repository.GetByIdAsync(original.Id))!.Name == renamed.Name, "Přejmenování se neuložilo.");
        var archived = renamed.WithArchived(true);
        await repository.UpdateAsync(archived);
        Check((await repository.GetAllAsync()).Count == 0, "Archivovaný cvik je v aktivní nabídce.");
        var all = await repository.GetAllAsync(true);
        Check(all.Count == 1 && all[0].IsArchived && all[0].Id == original.Id, "Archivace smazala cvik nebo ID.");
        Check(all is ICollection<Exercise> { IsReadOnly: true }, "Katalog není snímek pouze pro čtení.");
        Check((await repository.GetByIdAsync(original.Id))!.IsArchived, "Hledání podle ID ignoruje archiv.");
        await repository.UpdateAsync(archived.WithArchived(false));
        Check((await repository.GetAllAsync()).Count == 1, "Obnovení cviku nefunguje.");

        var first = Exercise.Restore(Guid.Parse("00000001-0000-0000-0000-000000000000"), "bench", false);
        var second = Exercise.Restore(Guid.Parse("00000002-0000-0000-0000-000000000000"), "Bench", false);
        await repository.AddAsync(second);
        await repository.AddAsync(first);
        all = await repository.GetAllAsync();
        Check(all.Count == 3 && all[0].Id == first.Id && all[1].Id == second.Id,
            "Řazení ignorující velikost písmen a shodu názvů nefunguje.");
        await ExpectAsync<InvalidOperationException>(() => repository.AddAsync(first));
        await ExpectAsync<ArgumentNullException>(() => repository.AddAsync(null!));
        await ExpectAsync<ArgumentNullException>(() => repository.UpdateAsync(null!));
        await ExpectAsync<KeyNotFoundException>(() => repository.UpdateAsync(new Exercise("Neexistující")));
        Check((await repository.GetAllAsync(true)).Count == 3, "Chybná aktualizace vytvořila nový záznam.");
        Check(await repository.GetByIdAsync(Guid.NewGuid()) is null, "Nenalezený cvik není null.");

        var connection = await database.GetConnectionAsync();
        await ExpectAsync<SQLiteException>(() => connection.ExecuteAsync(
            "UPDATE Exercises SET IsArchived = 2 WHERE Id = ?", original.Id.ToString("D")));
        await connection.CloseAsync();
        var reopened = new SqliteExerciseRepository(new FitnessDatabase(databasePath: Path.Combine(directory, "exercises.db3")));
        Check((await reopened.GetAllAsync()).Count == 3, "Opakované otevření změnilo katalog.");

        // Připravíme skutečné schéma předchozí verze i s tréninkem a provedeme upgrade.
        string upgradePath = Path.Combine(directory, "version1.db3");
        string workoutId = Guid.NewGuid().ToString("D");
        string time = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using (var old = new SQLiteConnection(upgradePath))
        {
            old.Execute("CREATE TABLE Workouts (Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, StartedAt TEXT NOT NULL)");
            old.Execute("INSERT INTO Workouts VALUES (?, ?, ?)", workoutId, "Původní trénink", time);
            old.Execute("PRAGMA user_version = 1");
        }
        var upgraded = new FitnessDatabase(upgradePath);
        Check((await new SqliteExerciseRepository(upgraded).GetAllAsync()).Count == 0, "Upgrade nepřipravil katalog.");
        var workout = await new SqliteWorkoutRepository(upgraded).GetByIdAsync(Guid.Parse(workoutId));
        Check(workout?.Name == "Původní trénink" && workout.StartedAt.ToString("O", CultureInfo.InvariantCulture) == time,
            "Migrace 1 → 2 změnila původní trénink.");
        Check(await (await upgraded.GetConnectionAsync()).ExecuteScalarAsync<int>("PRAGMA user_version") == 2,
            "Upgrade neuložil verzi 2.");

        // Konflikt v migraci 2 musí zachovat verzi 1 a všechna její data.
        string conflictPath = Path.Combine(directory, "migration2-failure.db3");
        using (var old = new SQLiteConnection(conflictPath))
        {
            old.Execute("CREATE TABLE Workouts (Id TEXT)");
            old.Execute("INSERT INTO Workouts VALUES ('zachovat')");
            old.Execute("CREATE TABLE Exercises (Marker TEXT)");
            old.Execute("INSERT INTO Exercises VALUES ('také zachovat')");
            old.Execute("PRAGMA user_version = 1");
        }
        var conflicted = new SqliteExerciseRepository(new FitnessDatabase(conflictPath));
        await ExpectAsync<SQLiteException>(async () => { await conflicted.GetAllAsync(); });
        using (var old = new SQLiteConnection(conflictPath))
        {
            Check(old.ExecuteScalar<int>("PRAGMA user_version") == 1, "Neúspěšná migrace 2 změnila verzi.");
            Check(old.ExecuteScalar<string>("SELECT Id FROM Workouts") == "zachovat", "Migrace 2 ztratila trénink.");
            Check(old.ExecuteScalar<string>("SELECT Marker FROM Exercises") == "také zachovat", "Konfliktní data byla přepsána.");
        }
        Console.WriteLine("PASS: cviky, validace, přejmenování, archivace/obnovení, výchozí filtr, řazení, identita, snapshot, migrace 1 → 2 a zachování dat při chybě.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Expect<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Chybí výjimka {typeof(TException).Name}.");
    }

    private static async Task ExpectAsync<TException>(Func<Task> action) where TException : Exception
    {
        try { await action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Chybí výjimka {typeof(TException).Name}.");
    }
}
