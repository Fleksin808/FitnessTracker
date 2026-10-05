namespace FitnessTracker
{
    public partial class AppShell : Shell
    {
        public AppShell(Features.Workouts.WorkoutPage workoutPage)
        {
            InitializeComponent();

            // DI sestaví stránku i ViewModel; Shell řeší pouze umístění v navigaci.
            WorkoutsContent.Content = workoutPage;
        }
    }
}
