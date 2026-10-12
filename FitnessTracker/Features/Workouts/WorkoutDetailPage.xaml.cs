namespace FitnessTracker.Features.Workouts;

public partial class WorkoutDetailPage : ContentPage, IQueryAttributable
{
    public const string Route = nameof(WorkoutDetailPage);
    public const string WorkoutIdParameter = "workoutId";
    private readonly WorkoutDetailViewModel viewModel;
    private Guid workoutId;

    public WorkoutDetailPage(WorkoutDetailViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Shell předá navigační parametr; neplatná hodnota se později zobrazí jako chyba detailu.
        workoutId = query.TryGetValue(WorkoutIdParameter, out var value)
            && Guid.TryParse(value?.ToString(), out var parsedId)
            ? parsedId
            : Guid.Empty;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Příjem parametru je synchronní; skutečné načtení počkáme v životním cyklu stránky.
        // ViewModel ošetřuje chybu úložiště i neexistující nebo prázdné ID.
        await viewModel.LoadAsync(workoutId);
    }
}
