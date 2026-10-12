using System.CommandLine;
using BluetoothAudioSwitch.Bluetooth;

namespace BluetoothAudioSwitch.Commands;

internal static class AppCommands
{
    public static RootCommand Build(IBluetoothAudioService bluetooth, IShortcutService shortcuts)
    {
        var root = new RootCommand("Bluetooth Audio Switch - connect and disconnect paired Bluetooth audio devices.")
        {
            BuildList(bluetooth),
            BuildConnectionCommand("connect", "Connect a paired Bluetooth audio device.", bluetooth, ConnectionAction.Connect),
            BuildConnectionCommand("disconnect", "Disconnect a paired Bluetooth audio device.", bluetooth, ConnectionAction.Disconnect),
            BuildConnectionCommand("toggle", "Connect the device if it is disconnected, otherwise disconnect it.", bluetooth, ConnectionAction.Toggle),
            BuildShortcut(bluetooth, shortcuts),
        };

        return root;
    }

    private enum ConnectionAction
    {
        Connect,
        Disconnect,
        Toggle,
    }

    private static Argument<string> NameArgument() => new("name")
    {
        Description = "Device name (or a unique part of it), e.g. \"Chris's Buds4\".",
    };

    private static Command BuildList(IBluetoothAudioService bluetooth)
    {
        var all = new Option<bool>("--all", "-a")
        {
            Description = "Include paired devices that are not audio devices.",
        };

        var command = new Command("list", "List paired Bluetooth audio devices.") { all };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var output = parseResult.InvocationConfiguration.Output;
            var devices = (await bluetooth.GetPairedDevicesAsync(cancellationToken))
                .Where(d => d.IsAudio || parseResult.GetValue(all))
                .ToList();

            if (devices.Count == 0)
            {
                output.WriteLine("No paired Bluetooth audio devices found.");
                return ExitCodes.Success;
            }

            var nameWidth = Math.Max("NAME".Length, devices.Max(d => d.Name.Length));
            output.WriteLine($"{"NAME".PadRight(nameWidth)}  {"STATUS",-12}  TYPE");
            foreach (var device in devices)
            {
                var status = device.IsConnected ? "Connected" : "Disconnected";
                var type = device.IsAudio ? "Audio" : "Other";
                output.WriteLine($"{device.Name.PadRight(nameWidth)}  {status,-12}  {type}");
            }

            return ExitCodes.Success;
        });

        return command;
    }

    private static Command BuildConnectionCommand(string name, string description, IBluetoothAudioService bluetooth, ConnectionAction action)
    {
        var nameArgument = NameArgument();
        var command = new Command(name, description) { nameArgument };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var output = parseResult.InvocationConfiguration.Output;
            var error = parseResult.InvocationConfiguration.Error;

            var (device, exitCode) = await FindDeviceAsync(bluetooth, parseResult.GetValue(nameArgument)!, error, cancellationToken);
            if (device is null)
            {
                return exitCode;
            }

            var connect = action switch
            {
                ConnectionAction.Connect => true,
                ConnectionAction.Disconnect => false,
                _ => !device.IsConnected,
            };

            try
            {
                if (connect)
                {
                    bluetooth.Connect(device);
                    output.WriteLine($"Connecting to {device.Name}...");
                }
                else
                {
                    bluetooth.Disconnect(device);
                    output.WriteLine($"Disconnecting {device.Name}...");
                }

                return ExitCodes.Success;
            }
            catch (BluetoothAudioException ex)
            {
                error.WriteLine(ex.Message);
                return ExitCodes.OperationFailed;
            }
        });

        return command;
    }

    private static Command BuildShortcut(IBluetoothAudioService bluetooth, IShortcutService shortcuts)
    {
        var nameArgument = NameArgument();
        var desktop = new Option<bool>("--desktop", "-d") { Description = "Create shortcuts on the desktop (default)." };
        var startMenu = new Option<bool>("--start-menu", "-s") { Description = "Create shortcuts in the Start Menu." };
        var toggle = new Option<bool>("--toggle", "-t") { Description = "Create a single Toggle shortcut instead of Connect and Disconnect." };

        var command = new Command("shortcut", "Create desktop / Start Menu shortcuts that connect or disconnect a device.")
        {
            nameArgument,
            desktop,
            startMenu,
            toggle,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var output = parseResult.InvocationConfiguration.Output;
            var error = parseResult.InvocationConfiguration.Error;

            var (device, exitCode) = await FindDeviceAsync(bluetooth, parseResult.GetValue(nameArgument)!, error, cancellationToken);
            if (device is null)
            {
                return exitCode;
            }

            var folders = new List<string>();
            if (parseResult.GetValue(desktop) || !parseResult.GetValue(startMenu))
            {
                folders.Add(shortcuts.DesktopFolder);
            }

            if (parseResult.GetValue(startMenu))
            {
                folders.Add(shortcuts.StartMenuFolder);
            }

            string[] verbs = parseResult.GetValue(toggle) ? ["toggle"] : ["connect", "disconnect"];
            var quotedName = $"\"{device.Name.Replace("\"", string.Empty)}\"";

            foreach (var folder in folders)
            {
                foreach (var verb in verbs)
                {
                    var title = $"{char.ToUpperInvariant(verb[0])}{verb[1..]} {device.Name}";
                    var path = shortcuts.Create(folder, title, $"{verb} {quotedName}");
                    output.WriteLine($"Created {path}");
                }
            }

            return ExitCodes.Success;
        });

        return command;
    }

    private static async Task<(PairedDevice? Device, int ExitCode)> FindDeviceAsync(
        IBluetoothAudioService bluetooth, string name, TextWriter error, CancellationToken cancellationToken)
    {
        var audioDevices = (await bluetooth.GetPairedDevicesAsync(cancellationToken)).Where(d => d.IsAudio).ToList();
        var result = DeviceNameMatcher.Match(audioDevices, d => d.Name, name);

        switch (result.Kind)
        {
            case MatchKind.Found:
                return (result.Match, ExitCodes.Success);

            case MatchKind.Ambiguous:
                error.WriteLine($"'{name}' matches more than one device:");
                foreach (var candidate in result.Candidates)
                {
                    error.WriteLine($"  {candidate.Name}");
                }

                error.WriteLine("Use a more specific name.");
                return (null, ExitCodes.AmbiguousName);

            default:
                error.WriteLine($"No paired Bluetooth audio device matches '{name}'.");
                if (audioDevices.Count > 0)
                {
                    error.WriteLine("Paired audio devices:");
                    foreach (var device in audioDevices)
                    {
                        error.WriteLine($"  {device.Name}");
                    }
                }

                return (null, ExitCodes.DeviceNotFound);
        }
    }
}
