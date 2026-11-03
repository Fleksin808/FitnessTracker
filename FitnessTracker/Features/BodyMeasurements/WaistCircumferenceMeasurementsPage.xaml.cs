using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.BodyMeasurements;

public partial class WaistCircumferenceMeasurementsPage : ContentPage
{
    private readonly ILogger<WaistCircumferenceMeasurementsPage> logger;
    public WaistCircumferenceMeasurementsViewModel ViewModel { get; }

    public WaistCircumferenceMeasurementsPage(WaistCircumferenceMeasurementsViewModel viewModel,
        ILogger<WaistCircumferenceMeasurementsPage> logger)
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
        if (sender is not Button { BindingContext: WaistCircumferenceMeasurementItem item }) return;
        try
        {
            bool confirmed = await DisplayAlertAsync("Smazat měření?",
                $"Opravdu chceš smazat hodnotu {item.CircumferenceText} z {item.MeasuredAtText}?",
                "Smazat", "Zrušit");
            if (confirmed) await ViewModel.DeleteAsync(item);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se potvrdit smazání měření obvodu pasu.");
            await DisplayAlertAsync("Měření", "Operaci s měřením se nepodařilo dokončit.", "OK");
        }
    }
}
