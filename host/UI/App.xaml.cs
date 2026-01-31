namespace UI;

public partial class App : Application
{
    private readonly Pages.MainPage _mainPage;
    
    public App(Pages.MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }
    
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new NavigationPage(_mainPage));
    }
}