namespace FitnessTracker.Features.Statistics;

public partial class StatisticsPage : ContentPage
{
    private readonly StatisticsViewModel viewModel;
    private bool isUpdatingReminderSwitch;

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
        isUpdatingReminderSwitch = true;
        WeeklyReminderSwitch.IsToggled = viewModel.ReminderEnabled;
        isUpdatingReminderSwitch = false;
    }

    private async void OnWeeklyReminderToggled(object? sender, ToggledEventArgs e)
    {
        if (isUpdatingReminderSwitch) return;
        await viewModel.SetReminderEnabledAsync(e.Value);
        isUpdatingReminderSwitch = true;
        WeeklyReminderSwitch.IsToggled = viewModel.ReminderEnabled;
        isUpdatingReminderSwitch = false;
    }
}
