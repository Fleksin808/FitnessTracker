using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using FitnessTracker.Domain.Workouts;
using FitnessTracker.Domain.Exercises;
using FitnessTracker.Infrastructure.Persistence;
using SQLite;

// Dva samostatné procesy ověří, že údaje nezůstaly pouze v paměti nebo v poolu připojení.
var persistedId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
var persistedTime = DateTimeOffset.Parse("2026-10-13T18:30:00.1234567+02:00", CultureInfo.InvariantCulture);
const string persistedName = "Nohy – O'Brien; DROP TABLE Workouts;";

if (args.Length == 2 && args[0] is "write" or "read")
{
    var childDatabase = new FitnessDatabase(args[1]);
    var childRepository = new SqliteWorkoutRepository(childDatabase);
    if (args[0] == "write")
    {
        await childRepository.AddAsync(Workout.Restore(persistedId, persistedName, persistedTime));
        var exercises = new SqliteExerciseRepository(childDatabase);
        await exercises.AddAsync(Exercise.Restore(persistedId, "Původní název cviku", false));
        var recording = new SqliteWorkoutLogRepository(childDatabase);
        await recording.AddExerciseAsync(persistedId, persistedId);
        var entry = (await recording.GetAsync(persistedId))!.Exercises.Single().Exercise;
        await recording.AddSetAsync(entry.Id, 8, 62500);
        await recording.FinishAsync(persistedId, persistedTime.AddHours(1));
        await exercises.UpdateAsync(Exercise.Restore(persistedId, persistedName, true));
    }
    else
    {
        var loaded = await childRepository.GetByIdAsync(persistedId);
        Check(loaded is not null && loaded.Id == persistedId && loaded.Name == persistedName,
            "Po restartu chybí původní ID nebo název.");
        Check(loaded!.StartedAt == persistedTime && loaded.StartedAt.Offset == TimeSpan.Zero,
            "Po restartu se změnil okamžik nebo přesnost času.");
        var exercises = new SqliteExerciseRepository(childDatabase);
        var exercise = await exercises.GetByIdAsync(persistedId);
        Check(exercise is { IsArchived: true } && exercise.Name == persistedName,
            "Po restartu se změnil název nebo archivace cviku.");
        Check((await exercises.GetAllAsync()).Count == 0 && (await exercises.GetAllAsync(true)).Count == 1,
            "Po restartu nefunguje filtr archivovaných cviků.");
        var recording = await new SqliteWorkoutLogRepository(childDatabase).GetAsync(persistedId);
        Check(recording!.Workout.FinishedAt == persistedTime.AddHours(1)
            && recording.Exercises.Single().Exercise.ExerciseNameSnapshot == "Původní název cviku"
            && recording.Exercises.Single().Sets.Single().LoadGrams == 62500,
            "Po restartu se změnil zaznamenaný cvik, série nebo konec tréninku.");
    }
    SQLiteAsyncConnection.ResetPool();
    return;
}

// Každé spuštění používá vlastní novou složku; databáze aplikace se testy nedotýkají.
string directory = Path.Combine(AppContext.BaseDirectory, "check-data-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    string path = Path.Combine(directory, "normal.db3");
    var database = new FitnessDatabase(path);
    var repository = new SqliteWorkoutRepository(database);
    Check((await repository.GetAllAsync()).Count == 0, "Nová databáze není prázdná.");
    var connection = await database.GetConnectionAsync();
    Check(await connection.ExecuteScalarAsync<int>("PRAGMA user_version") == 3, "Chybí migrace 3.");
    Check(await connection.ExecuteScalarAsync<int>("PRAGMA foreign_keys") == 1, "Cizí klíče nejsou zapnuté.");

    var oldest = new Workout("  Starší trénink  ", persistedTime.AddDays(-1));
    var newer = Workout.Restore(Guid.Parse("00000002-0000-0000-0000-000000000000"), persistedName, persistedTime);
    var sameTime = Workout.Restore(Guid.Parse("00000001-0000-0000-0000-000000000000"), persistedName, persistedTime);
    await repository.AddAsync(oldest);
    var snapshot = await repository.GetAllAsync();

    // Lokalizace zařízení nesmí měnit uložený formát.
    var previousCulture = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");
        await repository.AddAsync(newer);
        await repository.AddAsync(sameTime);
    }
    finally { CultureInfo.CurrentCulture = previousCulture; }

    var all = await repository.GetAllAsync();
    Check(all.Count == 3 && all[0].Id == sameTime.Id && all[1].Id == newer.Id && all[2].Id == oldest.Id,
        "Nesprávné řazení podle času a ID.");
    Check(snapshot.Count == 1 && snapshot[0].Name == "Starší trénink", "Snímek nebo normalizace názvu selhaly.");
    Check(all is ICollection<Workout> { IsReadOnly: true }, "Seznam není pouze pro čtení.");
    var raw = await connection.QueryAsync<WorkoutRow>("SELECT Id, Name, StartedAt FROM Workouts WHERE Id = ?", newer.Id.ToString("D"));
    Check(raw[0].StartedAt == persistedTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), "Chybný databázový formát času.");
    Check(await repository.GetByIdAsync(Guid.NewGuid()) is null, "Chybějící ID nevrací null.");
    await ExpectAsync<ArgumentNullException>(() => repository.AddAsync(null!));
    await ExpectAsync<InvalidOperationException>(() => repository.AddAsync(newer));
    Check((await repository.GetAllAsync()).Count == 3, "Duplicitní přidání změnilo data.");

    await connection.CloseAsync();
    var reopened = new SqliteWorkoutRepository(new FitnessDatabase(path));
    var restored = await reopened.GetByIdAsync(oldest.Id);
    Check(restored is not null && restored.Id == oldest.Id && restored.StartedAt == oldest.StartedAt,
        "Opakované otevření změnilo identitu nebo čas.");
    Check((await reopened.GetAllAsync()).Count == 3, "Opakovaná migrace změnila data.");

    // Několik prvních volání najednou ověří ochranu inicializace i zápisů.
    var concurrent = new SqliteWorkoutRepository(new FitnessDatabase(Path.Combine(directory, "concurrent.db3")));
    await Task.WhenAll(Enumerable.Range(0, 20).Select(i => concurrent.AddAsync(new Workout($"Trénink {i}", persistedTime))));
    Check((await concurrent.GetAllAsync()).Count == 20, "Souběžná inicializace nebo zápis ztratily data.");
    var duplicate = new Workout("Souběžný zápis stejného ID", persistedTime);
    var added = await Task.WhenAll(TryAddAsync(concurrent, duplicate), TryAddAsync(concurrent, duplicate));
    Check(added.Count(success => success) == 1, "Stejné ID bylo přidáno vícekrát.");

    string newerPath = Path.Combine(directory, "newer.db3");
    using (var rawDatabase = new SQLiteConnection(newerPath))
    {
        rawDatabase.Execute("CREATE TABLE Sentinel (Value TEXT)");
        rawDatabase.Execute("INSERT INTO Sentinel VALUES ('zachovat')");
        rawDatabase.Execute("PRAGMA user_version = 99");
    }
    var newerRepository = new SqliteWorkoutRepository(new FitnessDatabase(newerPath));
    await ExpectAsync<InvalidOperationException>(async () => { await newerRepository.GetAllAsync(); });
    using (var rawDatabase = new SQLiteConnection(newerPath))
    {
        Check(rawDatabase.ExecuteScalar<int>("PRAGMA user_version") == 99, "Novější verze byla přepsána.");
        Check(rawDatabase.ExecuteScalar<string>("SELECT Value FROM Sentinel") == "zachovat", "Novější databáze ztratila data.");
    }

    string brokenPath = Path.Combine(directory, "migration-failure.db3");
    using (var rawDatabase = new SQLiteConnection(brokenPath))
    {
        rawDatabase.Execute("CREATE TABLE Workouts (Id TEXT)");
        rawDatabase.Execute("INSERT INTO Workouts VALUES ('zachovat')");
    }
    var brokenDatabase = new FitnessDatabase(brokenPath);
    var brokenRepository = new SqliteWorkoutRepository(brokenDatabase);
    await ExpectAsync<SQLiteException>(async () => { await brokenRepository.GetAllAsync(); });
    using (var rawDatabase = new SQLiteConnection(brokenPath))
    {
        Check(rawDatabase.ExecuteScalar<int>("PRAGMA user_version") == 0, "Neúspěšná migrace změnila verzi.");
        Check(rawDatabase.ExecuteScalar<string>("SELECT Id FROM Workouts") == "zachovat", "Neúspěšná migrace smazala data.");
        // Odstraníme pouze umělou konfliktní tabulku tohoto testu a ověříme další pokus.
        rawDatabase.Execute("DROP TABLE Workouts");
    }
    Check((await brokenRepository.GetAllAsync()).Count == 0, "Inicializaci nelze po chybě zopakovat.");

    string corruptPath = Path.Combine(directory, "corrupt-row.db3");
    var corruptDatabase = new FitnessDatabase(corruptPath);
    var corruptConnection = await corruptDatabase.GetConnectionAsync();
    await corruptConnection.ExecuteAsync("INSERT INTO Workouts (Id, Name, StartedAt) VALUES (?, ?, ?)", persistedId.ToString("D"), "Chybný čas", "not-a-date");
    await ExpectAsync<FormatException>(async () => { await new SqliteWorkoutRepository(corruptDatabase).GetByIdAsync(persistedId); });

    string processPath = Path.Combine(directory, "restart.db3");
    await RunChildAsync("write", processPath);
    await RunChildAsync("read", processPath);
    await ExerciseChecks.RunAsync(directory);
    await WorkoutLogChecks.RunAsync(directory);
    Console.WriteLine("PASS: migrace, ID, Unicode/SQL parametry, přesný UTC čas, řazení, snapshot, duplicity, souběh, opětovné otevření, restart procesu, novější verze, rollback, retry a poškozený záznam.");
}
finally
{
    SQLiteAsyncConnection.ResetPool();
    Directory.Delete(directory, recursive: true);
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task ExpectAsync<TException>(Func<Task> action) where TException : Exception
{
    try { await action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"Očekávaná výjimka {typeof(TException).Name} nenastala.");
}

static async Task<bool> TryAddAsync(SqliteWorkoutRepository repository, Workout workout)
{
    try { await repository.AddAsync(workout); return true; }
    catch (InvalidOperationException) { return false; }
}

static async Task RunChildAsync(string mode, string path)
{
    var start = new ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true
    };
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    start.ArgumentList.Add(mode);
    start.ArgumentList.Add(path);
    using var process = Process.Start(start)!;
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    Check(process.ExitCode == 0, $"Proces {mode} selhal: {await output} {await error}");
}
