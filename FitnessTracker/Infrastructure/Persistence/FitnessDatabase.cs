using SQLite;

namespace FitnessTracker.Infrastructure.Persistence;

// Jedno místo pro připojení a verze databáze; cesta přijde z MAUI nebo z integračního testu.
public sealed class FitnessDatabase
{
    public const string FileName = "fitness-tracker.db3";
    private const int CurrentSchemaVersion = 1;
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
