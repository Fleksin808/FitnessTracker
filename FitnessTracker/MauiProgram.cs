using Microsoft.Extensions.Logging;

namespace FitnessTracker
{
    using Features.Workouts;
    using Application.Workouts;
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

            // Jedna instance úložiště sdílí tréninky po celý běh aplikace i mezi stránkami.
            builder.Services.AddSingleton<IWorkoutRepository, InMemoryWorkoutRepository>();

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
