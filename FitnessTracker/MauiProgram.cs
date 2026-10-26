using Microsoft.Extensions.Logging;

namespace FitnessTracker
{
    using Features.Workouts;
    using Application.Workouts;
    using Application.Exercises;
    using Features.Exercises;
    using Features.WorkoutHistory;
    using Application.Backup;
    using Features.Backup;
    using Application.Statistics;
    using Features.Statistics;
    using Application.WorkoutTemplates;
    using Features.WorkoutTemplates;
    using Application.BodyMeasurements;
    using Features.BodyMeasurements;
    using Infrastructure.Persistence;
    using Microsoft.Extensions.DependencyInjection;

    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // MAUI vybere datový adresář podle platformy; soubor přežije běžný restart aplikace.
            builder.Services.AddSingleton(_ => new FitnessDatabase(
                Path.Combine(FileSystem.AppDataDirectory, FitnessDatabase.FileName)));
            // ViewModely stále používají stejné rozhraní; měníme pouze implementaci úložiště.
            builder.Services.AddSingleton<IWorkoutRepository, SqliteWorkoutRepository>();
            builder.Services.AddSingleton<IWorkoutLogRepository, SqliteWorkoutLogRepository>();
            builder.Services.AddSingleton<IExerciseRepository, SqliteExerciseRepository>();
            builder.Services.AddSingleton<IFitnessBackupService, SqliteFitnessBackupService>();
            builder.Services.AddSingleton<IWorkoutStatisticsRepository, SqliteWorkoutStatisticsRepository>();
            builder.Services.AddSingleton<IWorkoutTemplateRepository, SqliteWorkoutTemplateRepository>();
            builder.Services.AddSingleton<IBodyWeightMeasurementRepository, SqliteBodyWeightMeasurementRepository>();
            builder.Services.AddTransient<BodyWeightMeasurementsViewModel>();
            builder.Services.AddTransient<BodyWeightMeasurementsPage>();
            builder.Services.AddTransient<WorkoutTemplatesViewModel>();
            builder.Services.AddTransient<WorkoutTemplatesPage>();
            builder.Services.AddTransient<BackupViewModel>();
            builder.Services.AddTransient<BackupPage>();
            builder.Services.AddTransient<StatisticsViewModel>();
            builder.Services.AddTransient<StatisticsPage>();
            builder.Services.AddTransient<ExerciseCatalogViewModel>();
            builder.Services.AddTransient<ExerciseCatalogPage>();
            builder.Services.AddTransient<WorkoutHistoryViewModel>();
            builder.Services.AddTransient<WorkoutHistoryPage>();

            // Transient znamená novou instanci při každém vyžádání z DI.
            // DI podle konstruktorů sestaví AppShell → WorkoutPage → WorkoutViewModel.
            builder.Services.AddTransient<WorkoutViewModel>();
            builder.Services.AddTransient<WorkoutPage>();
            // Každé otevření detailu dostane vlastní stav; repository zůstává sdílené.
            builder.Services.AddTransient<WorkoutDetailViewModel>();
            builder.Services.AddTransient<WorkoutDetailPage>();
            builder.Services.AddTransient<AppShell>();

            return builder.Build();
        }
    }
}
