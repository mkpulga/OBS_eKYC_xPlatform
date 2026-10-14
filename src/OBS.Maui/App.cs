using OBS.Core;

namespace OBS.Maui;
public sealed class App(MainPage page, SessionController session) : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(page) { Title = "OBS_eKYC" };
        window.Stopped += (_, _) => session.Pause();
        window.Resumed += async (_, _) => await session.ResumeAsync();
        window.Destroying += (_, _) => { page.Shutdown(); session.Dispose(); };
#if WINDOWS
        var confirmedClose = false;
        var asking = false;
        window.HandlerChanged += (_, _) =>
        {
            if (window.Handler?.PlatformView is not Microsoft.Maui.MauiWinUIWindow native) return;
            native.AppWindow.Closing += async (_, args) =>
            {
                if (confirmedClose) return;
                args.Cancel = true;
                if (asking) return;
                asking = true;
                try
                {
                    if (await page.DisplayAlert("Close application", "Are you sure you want to close the application?", "Yes", "No"))
                    {
                        await session.LogoutAsync(); confirmedClose = true; native.AppWindow.Destroy();
                    }
                }
                finally { asking = false; }
            };
        };
#endif
        return window;
    }
}
