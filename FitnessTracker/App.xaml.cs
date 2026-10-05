using Microsoft.Extensions.DependencyInjection;

namespace FitnessTracker
{
    public partial class App : Application
    {
        private readonly IServiceProvider services;

        public App(IServiceProvider services)
        {
            InitializeComponent();
            this.services = services;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Vstupní bod získá nový Shell z DI pro každé okno; formuláře tak nesdílejí stav.
            return new Window(services.GetRequiredService<AppShell>());
        }
    }
}
