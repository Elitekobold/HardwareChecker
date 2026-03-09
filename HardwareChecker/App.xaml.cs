using System.Windows;
using SQLitePCL;

namespace HardwareChecker
{
    public partial class App : Application
    {
        private static bool _sqliteInitialized;
        private static readonly object SqliteInitLock = new object();

        protected override void OnStartup(StartupEventArgs e)
        {
            EnsureSqliteProviderInitialized();
            base.OnStartup(e);
        }

        private static void EnsureSqliteProviderInitialized()
        {
            if (_sqliteInitialized)
            {
                return;
            }

            lock (SqliteInitLock)
            {
                if (_sqliteInitialized)
                {
                    return;
                }

                raw.SetProvider(new SQLite3Provider_e_sqlite3());
                raw.FreezeProvider();


                _sqliteInitialized = true;
            }
        }
    }
}
