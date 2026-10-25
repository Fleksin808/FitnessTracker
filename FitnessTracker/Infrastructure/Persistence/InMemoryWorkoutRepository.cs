using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitnessTracker.Application.Workouts;
using FitnessTracker.Domain.Workouts;

namespace FitnessTracker.Infrastructure.Persistence;

// Implementuje smlouvu úložiště; data existují pouze po dobu života této instance.
public sealed class InMemoryWorkoutRepository : IWorkoutRepository
{
    // Klíčem je ID tréninku. Stejné názvy mohou existovat, stejné ID nikoliv.
    private readonly Dictionary<Guid, Workout> workouts = new();

    // Sdílenou kolekci chráníme pro případ souběžného přístupu z více volání.
    private readonly object syncRoot = new();

    public Task AddAsync(Workout workout)
    {
        ArgumentNullException.ThrowIfNull(workout);

        lock (syncRoot)
        {
            // TryAdd odmítne duplicitní klíč, aniž by přepsal původní trénink.
            if (!workouts.TryAdd(workout.Id, workout))
                throw new InvalidOperationException("Trénink se stejným ID už existuje.");
        }

        // Zápis do paměti už je hotový; nepotřebujeme await ani nové vlákno.
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Workout>> GetAllAsync()
    {
        lock (syncRoot)
        {
            // ToList vytvoří snímek: další přidání nezmění dříve vrácený seznam.
            // AsReadOnly zabrání volajícímu upravovat tento seznam.
            IReadOnlyList<Workout> snapshot = workouts.Values
                .OrderByDescending(workout => workout.StartedAt)
                .ThenBy(workout => workout.Id)
                .ToList()
                .AsReadOnly();

            // Výsledek je připravený ihned, ale zachováme Task požadovaný rozhraním.
            return Task.FromResult(snapshot);
        }
    }

    public Task<Workout?> GetByIdAsync(Guid id)
    {
        lock (syncRoot)
        {
            // Při nenalezeném ID dostane workout hodnotu null podle smlouvy rozhraní.
            workouts.TryGetValue(id, out Workout? workout);
            return Task.FromResult(workout);
        }
    }

    public Task DeleteAsync(Guid id)
    {
        lock (syncRoot)
        {
            if (!workouts.Remove(id))
                throw new KeyNotFoundException("Trénink není dostupný.");
        }
        return Task.CompletedTask;
    }
}
