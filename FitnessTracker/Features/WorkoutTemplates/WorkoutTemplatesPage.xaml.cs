using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.WorkoutTemplates;

public partial class WorkoutTemplatesPage : ContentPage
{
    private readonly ILogger<WorkoutTemplatesPage> logger;
    private bool isNavigating;
    public WorkoutTemplatesViewModel ViewModel { get; }

    public WorkoutTemplatesPage(WorkoutTemplatesViewModel viewModel, ILogger<WorkoutTemplatesPage> logger)
    {
        ViewModel = viewModel;
        this.logger = logger;
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }

    private async void OnUseTemplateClicked(object? sender, EventArgs e)
    {
        if (isNavigating || sender is not Button { BindingContext: WorkoutTemplateItem item }) return;
        isNavigating = true;
        try
        {
            Guid? workoutId = await ViewModel.CreateWorkoutAsync(item);
            if (workoutId is null) return;
            await Shell.Current.GoToAsync(
                $"{Features.Workouts.WorkoutDetailPage.Route}?{Features.Workouts.WorkoutDetailPage.WorkoutIdParameter}={workoutId:D}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se otevřít nově vytvořený trénink.");
            await DisplayAlertAsync("Nový trénink", "Trénink vznikl, ale jeho detail se nepodařilo otevřít.", "OK");
        }
        finally { isNavigating = false; }
    }

    private async void OnDeleteTemplateClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: WorkoutTemplateItem item }) return;
        bool confirmed = await DisplayAlertAsync("Smazat šablonu?",
            $"Opravdu chceš smazat šablonu „{item.Name}“? Již vytvořené tréninky zůstanou zachovány.",
            "Smazat", "Zrušit");
        if (confirmed) ViewModel.DeleteCommand.Execute(item);
    }
}
