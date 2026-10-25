using FitnessTracker.Domain.WorkoutTemplates;

namespace FitnessTracker.Application.WorkoutTemplates;

/// <summary>Operace se šablonami a atomické vytvoření samostatného tréninku z plánu.</summary>
public interface IWorkoutTemplateRepository
{
    Task<IReadOnlyList<WorkoutTemplate>> GetAllAsync();
    Task AddAsync(WorkoutTemplate template);
    Task UpdateAsync(WorkoutTemplate template);
    Task DeleteAsync(Guid templateId);
    Task<Guid> CreateWorkoutAsync(Guid templateId, DateTimeOffset startedAt);
}

public sealed class WorkoutTemplateException(string message) : InvalidOperationException(message);
