using SQLite;

namespace FitnessTracker.Infrastructure.Persistence;

// Jedno místo pro připojení a verze databáze; cesta přijde z MAUI nebo z integračního testu.
public sealed class FitnessDatabase
{
    public const string FileName = "fitness-tracker.db3";
    private const int CurrentSchemaVersion = 4;
    private readonly SQLiteAsyncConnection connection;
    private readonly SemaphoreSlim initializationGate = new(1, 1);
    private bool initialized;

    public FitnessDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        connection = new SQLiteAsyncConnection(databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
    }

    internal async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        // Současné načtení seznamu a detailu nesmí spustit dvě inicializace.
        await initializationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!initialized)
            {
                await connection.SetBusyTimeoutAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                // Cizí klíče zapínáme mimo transakci pro toto připojení; využijí je další moduly.
                await connection.ExecuteAsync("PRAGMA foreign_keys = ON").ConfigureAwait(false);
                await connection.RunInTransactionAsync(database =>
                {
                    int version = database.ExecuteScalar<int>("PRAGMA user_version");
                    if (version < 0 || version > CurrentSchemaVersion)
                        throw new InvalidOperationException(
                            $"Nepodporovaná verze databáze {version}. Aktualizuj aplikaci.");

                    if (version == 0)
                    {
                        // Migrace 0 → 1: vytvoření tabulky i změna verze jsou jedna transakce.
                        // Existující neočekávanou tabulku nepřepisujeme ani nemažeme.
                        database.Execute("""
                            CREATE TABLE Workouts (
                                Id TEXT NOT NULL PRIMARY KEY,
                                Name TEXT NOT NULL CHECK (length(trim(Name)) > 0),
                                StartedAt TEXT NOT NULL
                            )
                            """);
                        database.Execute("PRAGMA user_version = 1");
                        version = 1;
                    }

                    if (version == 1)
                    {
                        // Migrace 1 → 2 přidá katalog a ponechá tabulku i data tréninků beze změny.
                        database.Execute("""
                            CREATE TABLE Exercises (
                                Id TEXT NOT NULL PRIMARY KEY,
                                Name TEXT NOT NULL CHECK (length(trim(Name)) > 0),
                                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK (IsArchived IN (0, 1))
                            )
                            """);
                        database.Execute("PRAGMA user_version = 2");
                        version = 2;
                    }

                    if (version == 2)
                    {
                        // Migrace 2 → 3 zachová existující tréninky a katalog a přidá jejich návaznosti.
                        database.Execute("ALTER TABLE Workouts ADD COLUMN FinishedAt TEXT NULL CHECK (FinishedAt IS NULL OR FinishedAt >= StartedAt)");
                        database.Execute("""
                            CREATE TABLE WorkoutExercises (
                                Id TEXT NOT NULL PRIMARY KEY,
                                WorkoutId TEXT NOT NULL REFERENCES Workouts(Id) ON DELETE CASCADE,
                                ExerciseId TEXT NOT NULL REFERENCES Exercises(Id) ON DELETE RESTRICT,
                                ExerciseNameSnapshot TEXT NOT NULL CHECK (length(trim(ExerciseNameSnapshot)) > 0),
                                Position INTEGER NOT NULL CHECK (Position > 0),
                                UNIQUE (WorkoutId, Position)
                            )
                            """);
                        database.Execute("""
                            CREATE TABLE WorkoutSets (
                                Id TEXT NOT NULL PRIMARY KEY,
                                WorkoutExerciseId TEXT NOT NULL REFERENCES WorkoutExercises(Id) ON DELETE CASCADE,
                                Position INTEGER NOT NULL CHECK (Position > 0),
                                Repetitions INTEGER NOT NULL CHECK (Repetitions > 0),
                                LoadGrams INTEGER NULL CHECK (LoadGrams IS NULL OR LoadGrams >= 0),
                                UNIQUE (WorkoutExerciseId, Position)
                            )
                            """);
                        database.Execute("PRAGMA user_version = 3");
                        version = 3;
                    }

                    if (version == 3)
                    {
                        // Migrace 3 → 4 odděluje plán cviků od skutečně zaznamenaného výkonu.
                        database.Execute("""
                            CREATE TABLE WorkoutTemplates (
                                Id TEXT NOT NULL PRIMARY KEY,
                                Name TEXT NOT NULL CHECK (length(trim(Name)) > 0)
                            )
                            """);
                        database.Execute("""
                            CREATE TABLE WorkoutTemplateExercises (
                                Id TEXT NOT NULL PRIMARY KEY,
                                TemplateId TEXT NOT NULL REFERENCES WorkoutTemplates(Id) ON DELETE CASCADE,
                                ExerciseId TEXT NOT NULL REFERENCES Exercises(Id) ON DELETE RESTRICT,
                                Position INTEGER NOT NULL CHECK (Position > 0),
                                UNIQUE (TemplateId, Position)
                            )
                            """);
                        database.Execute("PRAGMA user_version = 4");
                    }
                }).ConfigureAwait(false);

                // Při chybě zůstane false: další pokus může inicializaci zopakovat.
                initialized = true;
            }

            return connection;
        }
        finally
        {
            initializationGate.Release();
        }
    }
}
