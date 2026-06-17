using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;
using WinRT;

namespace WAExport;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
