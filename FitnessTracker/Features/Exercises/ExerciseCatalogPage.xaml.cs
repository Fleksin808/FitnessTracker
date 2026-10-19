namespace FitnessTracker.Features.Exercises;

public partial class ExerciseCatalogPage : ContentPage
{
    // Typovaný přístup slouží bindingům příkazů uvnitř šablony položky seznamu.
    public ExerciseCatalogViewModel ViewModel { get; }

    public ExerciseCatalogPage(ExerciseCatalogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Stránka řídí životní cyklus; operaci a její chyby řeší ViewModel.
        await ViewModel.LoadAsync();
    }
}
