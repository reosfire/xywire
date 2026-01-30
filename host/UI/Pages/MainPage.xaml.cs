namespace UI.Pages;

public partial class MainPage : ContentPage
{
    private int _count = 0;

    public MainPage()
    {
        InitializeComponent();
    }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        _count++;

        if (_count == 1)
            CounterLabel.Text = $"Clicked {_count} time";
        else
            CounterLabel.Text = $"Clicked {_count} times";

        SemanticScreenReader.Announce(CounterLabel.Text);
    }
}