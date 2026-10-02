using System.Windows;

namespace RemoteLogViewer;

public partial class App : Application
{
    public App()
    {
        L.Init();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SmbConnection.ReleaseAll();
        base.OnExit(e);
    }
}
