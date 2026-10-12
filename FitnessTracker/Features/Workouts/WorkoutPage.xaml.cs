using FitnessTracker.Domain.Workouts;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.Workouts;

public partial class WorkoutPage : ContentPage
{
    private readonly WorkoutViewModel viewModel;
    private readonly ILogger<WorkoutPage> logger;
    private bool isNavigating;

    public WorkoutPage(WorkoutViewModel viewModel, ILogger<WorkoutPage> logger)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.logger = logger;
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

    private async void OnWorkoutSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Workout workout)
            return;

        // Zrušení výběru dovolí po návratu otevřít i stejný trénink znovu.
        if (sender is CollectionView collectionView)
            collectionView.SelectedItem = null;

        if (isNavigating || viewModel.IsLoading || viewModel.IsSaving)
            return;

        isNavigating = true;
        try
        {
            // Navigaci řeší stránka; detail dostane pouze stabilní ID a sám načte data.
            await Shell.Current.GoToAsync(
                $"{WorkoutDetailPage.Route}?{WorkoutDetailPage.WorkoutIdParameter}={workout.Id:D}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se otevřít detail tréninku.");
            await DisplayAlertAsync("Detail tréninku", "Detail se nepodařilo otevřít. Zkus to znovu.", "OK");
        }
        finally
        {
            isNavigating = false;
        }
    }
}
