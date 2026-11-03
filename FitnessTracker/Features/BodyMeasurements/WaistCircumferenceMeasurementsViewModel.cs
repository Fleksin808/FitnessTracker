using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FitnessTracker.Application.BodyMeasurements;
using FitnessTracker.Domain.BodyMeasurements;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.BodyMeasurements;

public sealed class WaistCircumferenceMeasurementsViewModel : INotifyPropertyChanged
{
    private readonly IWaistCircumferenceMeasurementRepository repository;
    private readonly ILogger<WaistCircumferenceMeasurementsViewModel> logger;
    private readonly Command addCommand;
    private readonly Command reloadCommand;
    private string? centimetersInput;
    private string? statusMessage;
    private string? errorMessage;
    private bool isBusy;

    public WaistCircumferenceMeasurementsViewModel(IWaistCircumferenceMeasurementRepository repository,
        ILogger<WaistCircumferenceMeasurementsViewModel> logger)
    {
        this.repository = repository;
        this.logger = logger;
        addCommand = new Command(async () => await AddAsync(), () => !IsBusy);
        reloadCommand = new Command(async () => await LoadAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<WaistCircumferenceMeasurementItem> Measurements { get; } = [];
    public ICommand AddCommand => addCommand;
    public ICommand ReloadCommand => reloadCommand;
    public string? CentimetersInput { get => centimetersInput; set => SetField(ref centimetersInput, value); }
    public string? StatusMessage { get => statusMessage; private set => SetField(ref statusMessage, value); }
    public string? ErrorMessage { get => errorMessage; private set => SetField(ref errorMessage, value); }
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetField(ref isBusy, value)) return;
            addCommand.ChangeCanExecute();
            reloadCommand.ChangeCanExecute();
            OnPropertyChanged(nameof(CanInteract));
        }
    }
    public bool CanInteract => !IsBusy;

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try { await RefreshAsync(); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se načíst měření obvodu pasu.");
            ErrorMessage = "Historii měření se nepodařilo načíst. Zkus obnovit seznam.";
        }
        finally { IsBusy = false; }
    }

    public async Task AddAsync()
    {
        if (IsBusy || !TryParseCentimeters(CentimetersInput, out long millimeters)) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            // Pořizovací čas vzniká při uložení; model jej normalizuje na UTC.
            await repository.AddAsync(new WaistCircumferenceMeasurement(DateTimeOffset.UtcNow, millimeters));
            CentimetersInput = string.Empty;
            bool refreshed = await RefreshAsync();
            StatusMessage = refreshed ? "Měření uloženo."
                : "Měření je uloženo, ale seznam se nepodařilo obnovit. Použij Obnovit.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se uložit měření obvodu pasu.");
            ErrorMessage = "Měření se nepodařilo uložit. Zkus to znovu.";
        }
        finally { IsBusy = false; }
    }

    public async Task DeleteAsync(WaistCircumferenceMeasurementItem? item)
    {
        if (IsBusy || item is null) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            await repository.DeleteAsync(item.Measurement.Id);
            bool refreshed = await RefreshAsync();
            StatusMessage = refreshed ? "Měření smazáno."
                : "Měření je smazáno, ale seznam se nepodařilo obnovit. Použij Obnovit.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se smazat měření obvodu pasu {MeasurementId}.", item.Measurement.Id);
            ErrorMessage = exception is KeyNotFoundException
                ? "Toto měření už není dostupné. Obnov seznam."
                : "Měření se nepodařilo smazat. Zkus to znovu.";
        }
        finally { IsBusy = false; }
    }

    private bool TryParseCentimeters(string? text, out long millimeters)
    {
        millimeters = 0;
        const NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;
        if (string.IsNullOrWhiteSpace(text)
            || !decimal.TryParse(text.Trim(), style, CultureInfo.CurrentCulture, out decimal centimeters)
            || centimeters <= 0 || centimeters > (decimal)long.MaxValue / 10m
            || decimal.Truncate(centimeters * 10m) != centimeters * 10m)
        {
            StatusMessage = "Zadej kladný obvod v cm s přesností nejvýše na milimetry a desetinným oddělovačem zařízení.";
            return false;
        }

        // Centimetry převedeme na celé milimetry, aby SQLite neukládalo desetinné hodnoty.
        millimeters = (long)(centimeters * 10m);
        return true;
    }

    private async Task<bool> RefreshAsync()
    {
        try
        {
            var loaded = await repository.GetAllAsync();
            Measurements.Clear();
            foreach (var measurement in loaded) Measurements.Add(new WaistCircumferenceMeasurementItem(measurement));
            ErrorMessage = null;
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se obnovit historii měření obvodu pasu.");
            ErrorMessage = "Historii měření se nepodařilo načíst. Zkus obnovit seznam.";
            return false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
