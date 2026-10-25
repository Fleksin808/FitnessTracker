using FitnessTracker.Domain.Workouts;
using FitnessTracker.Features.Workouts;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.WorkoutHistory;

public partial class WorkoutHistoryPage : ContentPage
{
    private readonly WorkoutHistoryViewModel viewModel;
    private readonly ILogger<WorkoutHistoryPage> logger;
    private bool isNavigating;

    public WorkoutHistoryPage(WorkoutHistoryViewModel viewModel, ILogger<WorkoutHistoryPage> logger)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.logger = logger;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
    }

    private async void OnWorkoutSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not WorkoutHistoryItem item) return;
        if (sender is CollectionView collectionView) collectionView.SelectedItem = null;
        if (isNavigating || viewModel.IsLoading) return;

        isNavigating = true;
        try
        {
            // Historie sdílí stejný detail přes ID; záznamy se nenačítají duplicitně do stránky.
            await Shell.Current.GoToAsync(
                $"{WorkoutDetailPage.Route}?{WorkoutDetailPage.WorkoutIdParameter}={item.Workout.Id:D}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se otevřít trénink z historie.");
            await DisplayAlertAsync("Historie", "Detail tréninku se nepodařilo otevřít.", "OK");
        }
        finally { isNavigating = false; }
    }
}
