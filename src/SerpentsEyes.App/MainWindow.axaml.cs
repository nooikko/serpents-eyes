using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SerpentsEyes.App.ViewModels;

namespace SerpentsEyes.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    private void Reload_Click(object? sender, RoutedEventArgs e) => ViewModel.RefreshProfiles();

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
