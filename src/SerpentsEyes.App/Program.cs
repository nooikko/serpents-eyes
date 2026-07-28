using Avalonia;
using Avalonia.Threading;
using System;
using System.IO;

namespace SerpentsEyes.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception, fatal: true);

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Anything thrown while building the first window lands here. Without this the
            // process exits silently and the user sees nothing at all happen.
            Report(ex, fatal: true);
            Environment.ExitCode = 1;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace()
            .AfterSetup(_ => Dispatcher.UIThread.UnhandledException += OnDispatcherException);

    private static void OnDispatcherException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Keep the window alive: a viewer should never disappear because one interaction
        // hit an edge case. The crash log is the record of what happened.
        Report(e.Exception, fatal: false);
        e.Handled = true;
    }

    /// <summary>
    /// Writes the failure somewhere the user can find it and echoes it to stderr.
    /// </summary>
    private static void Report(Exception? exception, bool fatal)
    {
        if (exception is null)
        {
            return;
        }

        string message = $"[{DateTimeOffset.Now:u}] {(fatal ? "fatal" : "handled")}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
        Console.Error.Write(message);

        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SerpentsEyes");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "crash.log"), message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing useful left to do; stderr already has it.
        }
    }
}
