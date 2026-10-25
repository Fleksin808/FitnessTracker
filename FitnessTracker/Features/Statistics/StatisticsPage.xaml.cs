namespace FitnessTracker.Features.Statistics;

public partial class StatisticsPage : ContentPage
{
    private readonly StatisticsViewModel viewModel;

    public StatisticsPage(StatisticsViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
    }
}
