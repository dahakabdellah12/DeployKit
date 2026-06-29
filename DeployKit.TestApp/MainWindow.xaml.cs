using System.Windows;
using DeployKit.Integration;
using DK = DeployKit.Integration.DeployKit;

namespace DeployKit.TestApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var ver = typeof(MainWindow).Assembly.GetName().Version;
        var version = ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
        VersionText.Text = $"الإصدار الحالي: v{version}";
    }

    private async void OnCheckClick(object sender, RoutedEventArgs e)
    {
        CheckBtn.IsEnabled = false;
        StatusText.Text = "جارٍ الفحص...";

        var result = await DK.CheckAsync();

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            StatusText.Text = $"خطأ: {result.ErrorMessage}";
        }
        else if (result.HasUpdate)
        {
            StatusText.Text = $"يوجد تحديث v{result.LatestVersion}!";
        }
        else
        {
            StatusText.Text = "لا توجد تحديثات متاحة";
        }

        CheckBtn.IsEnabled = true;
    }
}
