namespace FitnessTracker
{
    public partial class AppShell : Shell
    {
        public AppShell(Features.Workouts.WorkoutPage workoutPage)
        {
            InitializeComponent();

            // Globální route umožní otevřít detail nad seznamem a vrátit se systémovým tlačítkem zpět.
            Routing.RegisterRoute(Features.Workouts.WorkoutDetailPage.Route, typeof(Features.Workouts.WorkoutDetailPage));

            // DI sestaví stránku i ViewModel; Shell řeší pouze umístění v navigaci.
            WorkoutsContent.Content = workoutPage;
        }
    }
}
