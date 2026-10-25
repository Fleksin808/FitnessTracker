namespace FitnessTracker
{
    public partial class AppShell : Shell
    {
        public AppShell(Features.Workouts.WorkoutPage workoutPage,
            Features.WorkoutHistory.WorkoutHistoryPage workoutHistoryPage,
            Features.Exercises.ExerciseCatalogPage exerciseCatalogPage,
            Features.Backup.BackupPage backupPage,
            Features.Statistics.StatisticsPage statisticsPage,
            Features.WorkoutTemplates.WorkoutTemplatesPage templatesPage)
        {
            InitializeComponent();

            // Globální route umožní otevřít detail nad seznamem a vrátit se systémovým tlačítkem zpět.
            Routing.RegisterRoute(Features.Workouts.WorkoutDetailPage.Route, typeof(Features.Workouts.WorkoutDetailPage));

            // DI sestaví stránku i ViewModel; Shell řeší pouze umístění v navigaci.
            WorkoutsContent.Content = workoutPage;
            HistoryContent.Content = workoutHistoryPage;
            ExercisesContent.Content = exerciseCatalogPage;
            BackupContent.Content = backupPage;
            StatisticsContent.Content = statisticsPage;
            TemplatesContent.Content = templatesPage;
        }
    }
}
