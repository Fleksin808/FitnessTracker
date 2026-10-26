namespace FitnessTracker.Application.Backup;

/// <summary>Počty záznamů ve validní záloze, které UI ukáže před potvrzením obnovy.</summary>
public sealed record BackupSummary(int WorkoutCount, int ExerciseCount, int WorkoutExerciseCount,
    int WorkoutSetCount, int WorkoutTemplateCount, int WorkoutTemplateExerciseCount,
    int BodyWeightMeasurementCount, DateTimeOffset ExportedAtUtc);

/// <summary>Exportuje a atomicky obnovuje úplný snímek lokálního deníku.</summary>
public interface IFitnessBackupService
{
    Task<byte[]> ExportAsync();
    Task<BackupSummary> ValidateAsync(ReadOnlyMemory<byte> json);
    Task RestoreAsync(ReadOnlyMemory<byte> json);
}
