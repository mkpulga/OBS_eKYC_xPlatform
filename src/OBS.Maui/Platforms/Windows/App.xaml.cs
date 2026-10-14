namespace OBS.Maui.WinUI;
public partial class App : MauiWinUIApplication
{
    private readonly Mutex instance;
    public App()
    {
        instance = new Mutex(true, "Local\\OBS_eKYC", out var first);
        if (!first) Environment.Exit(0);
        InitializeComponent();
    }
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
