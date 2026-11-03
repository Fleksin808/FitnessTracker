using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.BodyMeasurements;

public partial class BodyMeasurementsPage : ContentPage
{
    private readonly ILogger<BodyMeasurementsPage> logger;
    public BodyMeasurementsViewModel ViewModel { get; }

    public BodyMeasurementsPage(BodyMeasurementsViewModel viewModel, ILogger<BodyMeasurementsPage> logger)
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

    private async void OnDeleteMeasurementClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: BodyMeasurementItem item }) return;
        try
        {
            bool confirmed = await DisplayAlertAsync("Smazat měření?",
                $"Opravdu chceš smazat {item.BodyPartName}: {item.CircumferenceText} z {item.MeasuredAtText}?",
                "Smazat", "Zrušit");
            if (confirmed) await ViewModel.DeleteAsync(item);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se potvrdit smazání tělesné metriky.");
            await DisplayAlertAsync("Měření", "Operaci s měřením se nepodařilo dokončit.", "OK");
        }
    }
}
