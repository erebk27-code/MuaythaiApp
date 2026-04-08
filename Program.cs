using Avalonia;
using System;
using System.IO;
using System.Threading.Tasks;
using Velopack;

namespace MuaythaiApp;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
                StartupLogger.Log(exception, "Unhandled exception");
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            StartupLogger.Log(eventArgs.Exception, "Unobserved task exception");
            eventArgs.SetObserved();
        };

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            StartupLogger.Log(ex, "Fatal exception in Program.Main");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

internal static class StartupLogger
{
    public static void Log(string message)
    {
        try
        {
            var path = Path.Combine(AppPaths.GetLogsDirectory(), "startup.log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(path, entry);
        }
        catch
        {
        }
    }

    public static void Log(Exception exception, string context)
    {
        try
        {
            var path = Path.Combine(AppPaths.GetLogsDirectory(), "startup.log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(path, entry);
        }
        catch
        {
        }
    }
}
