using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FitnessTracker.Features.Workouts;

// Rozhraní umožňuje bindingu reagovat na změny vlastností po vytvoření stránky.
public sealed class WorkoutViewModel : INotifyPropertyChanged
{
    private string? workoutName = string.Empty;
    private string workoutResult = "Zatím nemáš zadaný trénink.";

    public WorkoutViewModel()
    {
        // Příkaz propojí požadavek z UI s metodou bez znalosti konkrétního tlačítka.
        ConfirmWorkoutCommand = new Command(ConfirmWorkout);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? WorkoutName
    {
        get => workoutName;
        set
        {
            // Stejná hodnota nevyžaduje další oznámení ani aktualizaci zobrazení.
            if (workoutName == value)
                return;

            workoutName = value;
            OnPropertyChanged();
        }
    }

    public string WorkoutResult
    {
        get => workoutResult;
        private set
        {
            if (workoutResult == value)
                return;

            workoutResult = value;
            // Bez oznámení by Label stále zobrazoval původní text.
            OnPropertyChanged();
        }
    }

    public ICommand ConfirmWorkoutCommand { get; }

    private void ConfirmWorkout()
    {
        // Odstraníme okrajové mezery; null může přijít z prázdného vstupního pole.
        string? name = WorkoutName?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            WorkoutResult = "Nejdřív zadej název tréninku.";
            // Neplatný vstup ukončí operaci před zobrazením úspěšného výsledku.
            return;
        }

        // Zatím měníme jen stav obrazovky; model a úložiště přidáme v kroku 2.
        WorkoutResult = $"Tvůj trénink: {name}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        // CallerMemberName doplní název vlastnosti místo ručně zapsaného řetězce.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
