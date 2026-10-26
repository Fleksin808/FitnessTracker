using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.BodyMeasurements;

public partial class BodyWeightMeasurementsPage : ContentPage
{
    private readonly ILogger<BodyWeightMeasurementsPage> logger;
    public BodyWeightMeasurementsViewModel ViewModel { get; }

    public BodyWeightMeasurementsPage(BodyWeightMeasurementsViewModel viewModel,
        ILogger<BodyWeightMeasurementsPage> logger)
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
        if (sender is not Button { BindingContext: BodyWeightMeasurementItem item }) return;
        try
        {
            // Potvrzení chrání historii před smazáním záznamu při náhodném klepnutí.
            bool confirmed = await DisplayAlertAsync("Smazat měření?",
                $"Opravdu chceš smazat hodnotu {item.WeightText} z {item.MeasuredAtText}?",
                "Smazat", "Zrušit");
            if (confirmed) await ViewModel.DeleteAsync(item);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se potvrdit smazání měření.");
            await DisplayAlertAsync("Měření", "Operaci s měřením se nepodařilo dokončit.", "OK");
        }
    }
}
