using System.CommandLine;
using BluetoothAudioSwitch.Bluetooth;
using BluetoothAudioSwitch.Commands;

namespace BluetoothAudioSwitch.Tests;

public class AppCommandsTests
{
    private readonly FakeBluetoothService _bluetooth = new(
        new PairedDevice("Chris's Buds4", IsConnected: false, ["filter-buds"]),
        new PairedDevice("Kitchen Speaker", IsConnected: true, ["filter-kitchen"]),
        new PairedDevice("Office Speaker", IsConnected: false, ["filter-office"]),
        new PairedDevice("Logitech Mouse", IsConnected: true, [])
    );

    private readonly FakeShortcutService _shortcuts = new();

    [Fact]
    public async Task List_shows_audio_devices_only()
    {
        var (exitCode, output, _) = await RunAsync("list");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Chris's Buds4", output);
        Assert.Contains("Connected", output);
        Assert.DoesNotContain("Logitech Mouse", output);
    }

    [Fact]
    public async Task List_all_includes_non_audio_devices()
    {
        var (_, output, _) = await RunAsync("list", "--all");

        Assert.Contains("Logitech Mouse", output);
    }

    [Fact]
    public async Task Connect_connects_matching_device()
    {
        var (exitCode, _, _) = await RunAsync("connect", "Chris Buds4");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(["connect Chris's Buds4"], _bluetooth.Calls);
    }

    [Fact]
    public async Task Disconnect_disconnects_matching_device()
    {
        var (exitCode, _, _) = await RunAsync("disconnect", "kitchen");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(["disconnect Kitchen Speaker"], _bluetooth.Calls);
    }

    [Theory]
    [InlineData("Chris's Buds4", "connect Chris's Buds4")]
    [InlineData("Kitchen Speaker", "disconnect Kitchen Speaker")]
    public async Task Toggle_flips_connection_state(string name, string expectedCall)
    {
        await RunAsync("toggle", name);

        Assert.Equal([expectedCall], _bluetooth.Calls);
    }

    [Fact]
    public async Task Unknown_device_returns_not_found()
    {
        var (exitCode, _, error) = await RunAsync("connect", "AirPods");

        Assert.Equal(ExitCodes.DeviceNotFound, exitCode);
        Assert.Contains("Chris's Buds4", error);
        Assert.Empty(_bluetooth.Calls);
    }

    [Fact]
    public async Task Non_audio_device_is_not_matched()
    {
        var (exitCode, _, _) = await RunAsync("connect", "Logitech");

        Assert.Equal(ExitCodes.DeviceNotFound, exitCode);
    }

    [Fact]
    public async Task Ambiguous_name_returns_ambiguous()
    {
        var (exitCode, _, error) = await RunAsync("connect", "speaker");

        Assert.Equal(ExitCodes.AmbiguousName, exitCode);
        Assert.Contains("Kitchen Speaker", error);
        Assert.Contains("Office Speaker", error);
        Assert.Empty(_bluetooth.Calls);
    }

    [Fact]
    public async Task Failed_operation_returns_operation_failed()
    {
        _bluetooth.FailWith = "Device out of range";

        var (exitCode, _, error) = await RunAsync("connect", "buds");

        Assert.Equal(ExitCodes.OperationFailed, exitCode);
        Assert.Contains("Device out of range", error);
    }

    [Fact]
    public async Task Missing_name_is_invalid_usage()
    {
        var (exitCode, _, _) = await RunAsync("connect");

        Assert.Equal(ExitCodes.InvalidUsage, exitCode);
    }

    [Fact]
    public async Task Shortcut_defaults_to_desktop_connect_and_disconnect()
    {
        var (exitCode, _, _) = await RunAsync("shortcut", "buds");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(
            [
                ("desktop", "Connect Chris's Buds4", "connect \"Chris's Buds4\""),
                ("desktop", "Disconnect Chris's Buds4", "disconnect \"Chris's Buds4\""),
            ],
            _shortcuts.Created
        );
    }

    [Fact]
    public async Task Shortcut_toggle_in_start_menu()
    {
        await RunAsync("shortcut", "buds", "--start-menu", "--toggle");

        Assert.Equal([("start-menu", "Toggle Chris's Buds4", "toggle \"Chris's Buds4\"")], _shortcuts.Created);
    }

    private async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var root = AppCommands.Build(_bluetooth, _shortcuts);

        var exitCode = await root.Parse(args)
            .InvokeAsync(new InvocationConfiguration { Output = output, Error = error });
        return (exitCode, output.ToString(), error.ToString());
    }

    private sealed class FakeBluetoothService(params PairedDevice[] devices) : IBluetoothAudioService
    {
        public List<string> Calls { get; } = [];

        public string? FailWith { get; set; }

        public Task<IReadOnlyList<PairedDevice>> GetPairedDevicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PairedDevice>>(devices);

        public void Connect(PairedDevice device) => Record("connect", device);

        public void Disconnect(PairedDevice device) => Record("disconnect", device);

        private void Record(string action, PairedDevice device)
        {
            if (FailWith is not null)
            {
                throw new BluetoothAudioException(FailWith);
            }

            Calls.Add($"{action} {device.Name}");
        }
    }

    private sealed class FakeShortcutService : IShortcutService
    {
        public List<(string Folder, string Title, string Arguments)> Created { get; } = [];

        public string DesktopFolder => "desktop";

        public string StartMenuFolder => "start-menu";

        public string Create(string folder, string title, string arguments)
        {
            Created.Add((folder, title, arguments));
            return $"{folder}\\{title}.lnk";
        }
    }
}
