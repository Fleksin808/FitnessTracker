using System.ComponentModel;
using System.Runtime.CompilerServices;
using FitnessTracker.Application.Backup;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.Backup;

/// <summary>Stav a orchestrace zálohy; systémové okno pro soubory zůstává ve View.</summary>
public sealed class BackupViewModel(IFitnessBackupService backupService, ILogger<BackupViewModel> logger)
    : INotifyPropertyChanged
{
    private bool isBusy;
    private string statusMessage = "Záloha obsahuje tréninky, katalog cviků a jejich historii.";

    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (isBusy == value) return;
            isBusy = value;
            OnPropertyChanged();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIdle)));
        }
    }
    public bool IsIdle => !IsBusy;
    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public async Task ExportAsync(Func<byte[], Task> shareBackupAsync)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            byte[] json = await backupService.ExportAsync();
            await shareBackupAsync(json);
            StatusMessage = "Záloha je připravena ke sdílení.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se vytvořit nebo sdílet zálohu.");
            StatusMessage = "Zálohu se nepodařilo vytvořit nebo sdílet.";
        }
        finally { IsBusy = false; }
    }

    public async Task RestoreAsync(ReadOnlyMemory<byte> json, Func<BackupSummary, Task<bool>> confirmReplacementAsync)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            BackupSummary summary = await backupService.ValidateAsync(json);
            if (!await confirmReplacementAsync(summary))
            {
                StatusMessage = "Obnova byla zrušena. Současná data zůstala beze změny.";
                return;
            }

            await backupService.RestoreAsync(json);
            StatusMessage = $"Obnova dokončena: {summary.WorkoutCount} tréninků a {summary.ExerciseCount} cviků.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se ověřit nebo obnovit zálohu.");
            StatusMessage = exception is InvalidDataException
                ? exception.Message
                : "Záloha je neplatná nebo ji nelze obnovit. Současná data zůstala zachována.";
        }
        finally { IsBusy = false; }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
