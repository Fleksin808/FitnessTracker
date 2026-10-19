using Microsoft.Extensions.Logging;

namespace FitnessTracker
{
    using Features.Workouts;
    using Application.Workouts;
    using Application.Exercises;
    using Features.Exercises;
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
            builder.Services.AddTransient<ExerciseCatalogViewModel>();
            builder.Services.AddTransient<ExerciseCatalogPage>();

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
