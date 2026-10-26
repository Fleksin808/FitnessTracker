using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FitnessTracker.Application.BodyMeasurements;
using FitnessTracker.Domain.BodyMeasurements;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.BodyMeasurements;

public sealed class BodyWeightMeasurementsViewModel : INotifyPropertyChanged
{
    private readonly IBodyWeightMeasurementRepository repository;
    private readonly ILogger<BodyWeightMeasurementsViewModel> logger;
    private readonly Command addCommand;
    private readonly Command reloadCommand;
    private string? weightKilogramsInput;
    private string? statusMessage;
    private string? errorMessage;
    private bool isBusy;

    public BodyWeightMeasurementsViewModel(IBodyWeightMeasurementRepository repository,
        ILogger<BodyWeightMeasurementsViewModel> logger)
    {
        this.repository = repository;
        this.logger = logger;
        addCommand = new Command(async () => await AddAsync(), () => !IsBusy);
        reloadCommand = new Command(async () => await LoadAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<BodyWeightMeasurementItem> Measurements { get; } = [];
    public ICommand AddCommand => addCommand;
    public ICommand ReloadCommand => reloadCommand;
    public string? WeightKilogramsInput { get => weightKilogramsInput; set => SetField(ref weightKilogramsInput, value); }
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
            logger.LogError(exception, "Nepodařilo se načíst měření tělesné hmotnosti.");
            ErrorMessage = "Historii měření se nepodařilo načíst. Zkus obnovit seznam.";
        }
        finally { IsBusy = false; }
    }

    public async Task AddAsync()
    {
        if (IsBusy || !TryParseWeight(WeightKilogramsInput, out long grams)) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            // Čas pořízení zachytíme při potvrzení formuláře a doména ho normalizuje do UTC.
            await repository.AddAsync(new BodyWeightMeasurement(DateTimeOffset.UtcNow, grams));
            WeightKilogramsInput = string.Empty;
            bool refreshed = await RefreshAsync();
            StatusMessage = refreshed ? "Měření uloženo."
                : "Měření je uloženo, ale seznam se nepodařilo obnovit. Použij Obnovit.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se uložit měření tělesné hmotnosti.");
            ErrorMessage = "Měření se nepodařilo uložit. Zkus to znovu.";
        }
        finally { IsBusy = false; }
    }

    public async Task DeleteAsync(BodyWeightMeasurementItem? item)
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
            logger.LogError(exception, "Nepodařilo se smazat měření {MeasurementId}.", item.Measurement.Id);
            ErrorMessage = exception is KeyNotFoundException
                ? "Toto měření už není dostupné. Obnov seznam."
                : "Měření se nepodařilo smazat. Zkus to znovu.";
        }
        finally { IsBusy = false; }
    }

    private bool TryParseWeight(string? text, out long grams)
    {
        grams = 0;
        const NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;
        if (string.IsNullOrWhiteSpace(text)
            || !decimal.TryParse(text.Trim(), style, CultureInfo.CurrentCulture, out decimal kilograms)
            || kilograms <= 0 || kilograms > (decimal)long.MaxValue / 1000m
            || decimal.Truncate(kilograms * 1000m) != kilograms * 1000m)
        {
            StatusMessage = "Zadej kladnou hmotnost v kg s přesností nejvýše na gramy a desetinným oddělovačem zařízení.";
            return false;
        }

        // Gram ukládáme jako celé číslo, aby desetinné kg neztratily přesnost v SQLite.
        grams = (long)(kilograms * 1000m);
        return true;
    }

    private async Task<bool> RefreshAsync()
    {
        try
        {
            var loaded = await repository.GetAllAsync();
            Measurements.Clear();
            foreach (var measurement in loaded) Measurements.Add(new BodyWeightMeasurementItem(measurement));
            ErrorMessage = null;
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se obnovit historii měření tělesné hmotnosti.");
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
