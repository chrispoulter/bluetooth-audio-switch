using System.CommandLine;
using System.Reflection;
using System.Runtime.InteropServices;
using BluetoothAudioSwitch;
using BluetoothAudioSwitch.Bluetooth;
using BluetoothAudioSwitch.Commands;
using Serilog;

var root = AppCommands.Build(new BluetoothAudioService(), new ShortcutService());
var parseResult = root.Parse(args);

Log.Logger = Logging.Create(parseResult.GetValue(AppCommands.Verbose));

try
{
    Log.Information(
        "btaudio {Version} ({Architecture}) on {OS}: {Args}",
        typeof(Program)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion,
        RuntimeInformation.ProcessArchitecture.ToString(),
        Environment.OSVersion.VersionString,
        args
    );

    var exitCode = await parseResult.InvokeAsync(
        new InvocationConfiguration { EnableDefaultExceptionHandler = false }
    );

    Log.Information("Exit code {ExitCode}", exitCode);
    return exitCode;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
    Console.Error.WriteLine(ex.Message);

    // Matches System.CommandLine's default exception handler.
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
