namespace FitnessTracker.Features.Workouts;

public partial class WorkoutPage : ContentPage
{
    private readonly WorkoutViewModel viewModel;

    public WorkoutPage(WorkoutViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        // DI předá ViewModel; BindingContext je zdrojem dat a příkazů pro XAML.
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Životní cyklus patří stránce; načítání a ošetření chyb zůstávají ve ViewModelu.
        // Při návratu na stránku znovu načteme i tréninky přidané jinde.
        await viewModel.LoadWorkoutsAsync();
    }
}
