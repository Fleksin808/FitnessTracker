using FitnessTracker.Application.Backup;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.Backup;

public partial class BackupPage : ContentPage
{
    private const int MaximumFileBytes = 25 * 1024 * 1024;
    private readonly BackupViewModel viewModel;
    private readonly ILogger<BackupPage> logger;
    private bool isFileFlowActive;

    public BackupPage(BackupViewModel viewModel, ILogger<BackupPage> logger)
    {
        this.viewModel = viewModel;
        this.logger = logger;
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (isFileFlowActive || viewModel.IsBusy) return;
        isFileFlowActive = true;
        try
        {
            await viewModel.ExportAsync(async bytes =>
            {
                string fileName = $"fitnesstracker-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                string path = Path.Combine(FileSystem.CacheDirectory, fileName);
                try
                {
                    await File.WriteAllBytesAsync(path, bytes);
                    // Systémové sdílení nechá uživatele zvolit, kam si vlastní zálohu uloží.
                    await Share.Default.RequestAsync(new ShareFileRequest("Záloha FitnessTracker",
                        new ShareFile(path, "application/json")));
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                }
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se připravit soubor zálohy.");
            await DisplayAlertAsync("Záloha", "Soubor zálohy se nepodařilo připravit.", "OK");
        }
        finally { isFileFlowActive = false; }
    }

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        if (isFileFlowActive || viewModel.IsBusy) return;
        isFileFlowActive = true;
        try
        {
            FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Vyber zálohu FitnessTracker ve formátu JSON"
            });
            if (file is null) return;

            byte[] bytes = await ReadWithLimitAsync(await file.OpenReadAsync());
            await viewModel.RestoreAsync(bytes, async summary =>
                await DisplayAlertAsync("Nahradit současná data?",
                    $"Záloha obsahuje {summary.WorkoutCount} tréninků, {summary.ExerciseCount} cviků, " +
                    $"{summary.WorkoutExerciseCount} zaznamenaných cviků a {summary.WorkoutSetCount} sérií. " +
                    $"Dále obsahuje {summary.WorkoutTemplateCount} šablon a " +
                    $"{summary.BodyWeightMeasurementCount} měření tělesné hmotnosti. " +
                    "Pokračováním nahradíš celý současný deník.",
                    "Nahradit data", "Zrušit"));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se vybrat nebo načíst soubor zálohy.");
            await DisplayAlertAsync("Obnova zálohy", exception is InvalidDataException
                ? exception.Message : "Soubor se nepodařilo načíst. Současná data zůstala beze změny.", "OK");
        }
        finally { isFileFlowActive = false; }
    }

    private static async Task<byte[]> ReadWithLimitAsync(Stream source)
    {
        await using Stream input = source;
        using var destination = new MemoryStream();
        byte[] buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            if (destination.Length + read > MaximumFileBytes)
                throw new InvalidDataException("Soubor zálohy překračuje limit 25 MiB.");
            await destination.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
        }
        return destination.ToArray();
    }
}
