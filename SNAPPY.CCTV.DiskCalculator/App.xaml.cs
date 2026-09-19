using System.Windows;
namespace SNAPPY.CCTV.DiskCalculator;
public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(e.Exception.ToString(), "SNAPPY - Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }
}
