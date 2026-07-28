using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SerpentsEyes.App.ViewModels;

namespace SerpentsEyes.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        // The game is a Windows title, but Core parses anywhere, so the window has to be
        // usable on Linux and macOS. The custom title bar is Windows-shaped: its caption
        // buttons sit on the right and it hides the system decorations, which is wrong on
        // macOS and unreliable under many Linux window managers.
        if (!OperatingSystem.IsWindows())
        {
            ExtendClientAreaToDecorationsHint = false;
            WindowDecorations = WindowDecorations.Full;
            CustomTitleBar.IsVisible = false;
        }

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    private void Reload_Click(object? sender, RoutedEventArgs e) => ViewModel.RefreshProfiles();

    private async void Open_Click(object? sender, RoutedEventArgs e) => await OpenFilePickerAsync();

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>
    /// Lets the user pick a save from anywhere. This is the only route in for a non-Steam or
    /// relocated install, and the only one at all on platforms where the game runs under Proton.
    /// </summary>
    private async Task OpenFilePickerAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a Serpent's Gaze save",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Serpent's Gaze save") { Patterns = ["*.sav"] },
                FilePickerFileTypes.All,
            ],
        });

        string? path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null)
        {
            ViewModel.OpenFile(path);
        }
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasSaveFile(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        string? path = SaveFilePath(e);
        if (path is not null)
        {
            ViewModel.OpenFile(path);
        }
        e.Handled = true;
    }

    private static bool HasSaveFile(DragEventArgs e) => SaveFilePath(e) is not null;

    private static string? SaveFilePath(DragEventArgs e)
        => e.DataTransfer.TryGetFiles()?
            .Select(f => f.TryGetLocalPath())
            .FirstOrDefault(p => p is not null && File.Exists(p));
}
