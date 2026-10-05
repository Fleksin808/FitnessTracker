using Microsoft.Extensions.DependencyInjection;

namespace FitnessTracker
{
    // Úplný název odlišuje třídu MAUI od našeho jmenného prostoru FitnessTracker.Application.
    public partial class App : Microsoft.Maui.Controls.Application
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
