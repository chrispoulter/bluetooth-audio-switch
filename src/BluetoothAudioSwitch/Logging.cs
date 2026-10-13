using Serilog;
using Serilog.Events;

namespace BluetoothAudioSwitch;

/// <summary>
/// Shortcuts run btaudio minimised, so stderr is never seen; a small rolling log
/// file is the only way to diagnose a failed click after the fact.
/// </summary>
internal static class Logging
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({ProcessId}) {Message:lj}{NewLine}{Exception}";

    public static string LogFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Bluetooth Audio Switch",
            "Logs"
        );

    public static ILogger Create(bool verbose)
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(verbose ? LogEventLevel.Debug : LogEventLevel.Information)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            // shared: two shortcut clicks can run at the same time.
            .WriteTo.File(
                Path.Combine(LogFolder, "btaudio-.log"),
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 1024 * 1024,
                shared: true
            );

        if (verbose)
        {
            // stderr only, so stdout (e.g. the list table) stays clean.
            configuration.WriteTo.Console(
                outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: LogEventLevel.Verbose
            );
        }

        return configuration.CreateLogger();
    }
}
