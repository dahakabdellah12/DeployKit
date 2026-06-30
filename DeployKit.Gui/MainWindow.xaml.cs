using System.IO;
using System.Windows;
using System.Windows.Input;
using DeployKit.Gui.Helpers;
using DeployKit.Gui.ViewModels;

namespace DeployKit.Gui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm == null) return;

        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.D1:
                case Key.NumPad1:
                    vm.NavigateTo(0);
                    e.Handled = true;
                    break;
                case Key.D2:
                case Key.NumPad2:
                    vm.NavigateTo(1);
                    e.Handled = true;
                    break;
                case Key.O:
                    HandleBrowse(vm);
                    e.Handled = true;
                    break;
            }
        }
        else if (e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key == Key.T)
            {
                vm.ToggleThemeCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private static void HandleBrowse(MainViewModel vm)
    {
        if (vm.SelectedNavIndex == 1)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                if (string.IsNullOrEmpty(vm.BuildVM.OldDir))
                    vm.BuildVM.OldDir = dialog.FolderName;
                else
                    vm.BuildVM.NewDir = dialog.FolderName;
            }
        }
    }

    private void ReleaseItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ReleaseRecord record)
        {
            if (File.Exists(record.ZipPath))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = record.ZipPath,
                    UseShellExecute = true
                });
        }
    }
}
