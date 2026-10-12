using BluetoothAudioSwitch.Interop;

namespace BluetoothAudioSwitch.Commands;

internal interface IShortcutService
{
    /// <summary>Creates a shortcut that runs btaudio with the given arguments and returns its path.</summary>
    string Create(string folder, string title, string arguments);

    string DesktopFolder { get; }

    string StartMenuFolder { get; }
}

internal sealed class ShortcutService : IShortcutService
{
    public string DesktopFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public string StartMenuFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Bluetooth Audio Switch"
        );

    public string Create(string folder, string title, string arguments)
    {
        var exePath =
            Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to determine the path of btaudio.exe.");

        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, SanitizeFileName(title) + ".lnk");
        var bluetoothIcon = Path.Combine(Environment.SystemDirectory, "bthprops.cpl");

        ShellLink.Create(path, exePath, arguments, title, bluetoothIcon, 0);
        return path;
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
