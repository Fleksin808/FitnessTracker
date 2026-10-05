namespace FitnessTracker.Features.Workouts;

public partial class WorkoutPage : ContentPage
{
    public WorkoutPage(WorkoutViewModel viewModel)
    {
        InitializeComponent();
        // DI předá ViewModel; BindingContext je zdrojem dat a příkazů pro XAML.
        BindingContext = viewModel;
    }
}
