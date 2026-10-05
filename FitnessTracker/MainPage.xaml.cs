namespace FitnessTracker
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void OnWorkoutClicked(object? sender, EventArgs e)
        {
            string? workoutName = WorkoutNameEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(workoutName))
            {
                WorkoutResultLabel.Text = "Nejdřív zadej název tréninku.";
                return;
            }

            WorkoutResultLabel.Text = $"Tvůj trénink: {workoutName}";
        }
    }
}
