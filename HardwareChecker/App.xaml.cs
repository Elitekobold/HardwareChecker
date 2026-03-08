using System.Windows;
using SQLitePCL;

namespace HardwareChecker
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Batteries_V2.Init();
            base.OnStartup(e);
        }
    }
}
