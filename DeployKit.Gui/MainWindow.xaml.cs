using System.IO;
using System.Windows;
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

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm == null) return;

        if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.D1:
                case System.Windows.Input.Key.NumPad1:
                    vm.NavigateTo(0);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.D2:
                case System.Windows.Input.Key.NumPad2:
                    vm.NavigateTo(1);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.D3:
                case System.Windows.Input.Key.NumPad3:
                    vm.NavigateTo(2);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.O:
                    HandleBrowse(vm);
                    e.Handled = true;
                    break;
            }
        }
        else if (e.KeyboardDevice.Modifiers == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
        {
            if (e.Key == System.Windows.Input.Key.T)
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
        else if (vm.SelectedNavIndex == 2)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "ZIP files (*.zip)|*.zip" };
            if (dialog.ShowDialog() == true)
                vm.ApplyVM.PackagePath = dialog.FileName;
        }
    }

    private void Content_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Content_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files &&
            files.Length > 0)
        {
            var path = files[0];

            if (Directory.Exists(path))
            {
                var vm = DataContext as MainViewModel;
                if (vm == null) return;

                var navIndex = vm.SelectedNavIndex;

                if (navIndex == 0)
                {
                    // Home page - navigate to build with this path
                    vm.NavigateTo(1);
                    vm.BuildVM.OldDir = path;
                }
                else if (navIndex == 1)
                {
                    // Build page
                    if (string.IsNullOrEmpty(vm.BuildVM.OldDir))
                        vm.BuildVM.OldDir = path;
                    else
                        vm.BuildVM.NewDir = path;
                }
                else if (navIndex == 2)
                {
                    // Apply page
                    if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        vm.ApplyVM.PackagePath = path;
                    else
                        vm.ApplyVM.TargetDir = path;
                }
            }
            else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var vm = DataContext as MainViewModel;
                if (vm == null) return;

                if (vm.SelectedNavIndex != 2)
                {
                    vm.NavigateTo(2);
                }
                vm.ApplyVM.PackagePath = path;
            }

            e.Handled = true;
        }
    }
}
