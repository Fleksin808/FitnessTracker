using System;
using System.ComponentModel;
using FitnessTracker.Application.Workouts;
using FitnessTracker.Domain.Workouts;

namespace FitnessTracker.Features.Workouts
{
    public class WorkoutDetailViewModel : INotifyPropertyChanged
    {
        private readonly IWorkoutRepository repository;

        public event PropertyChangedEventHandler? PropertyChanged;
        public Workout? Workout { get; private set; }

        public async Task LoadAsync(Guid id)
        {
            // Načtení tréninku z úložiště; ViewModel nezná konkrétní způsob ukládání.
            Workout = await repository.GetByIdAsync(id);
        }

        public WorkoutDetailViewModel(IWorkoutRepository repository)
        {
            // DI předá úložiště; ViewModel nezná konkrétní způsob ukládání.
            this.repository = repository;
        }
    }
}
