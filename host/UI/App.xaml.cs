namespace UI;

public partial class App : Application
{
    public App(Pages.MainPage mainPage)
    {
        InitializeComponent();
        MainPage = new NavigationPage(mainPage);
    }
}