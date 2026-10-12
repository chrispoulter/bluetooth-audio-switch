using BluetoothAudioSwitch.Interop;

namespace BluetoothAudioSwitch.Commands;

internal interface IShortcutService
{
    /// <summary>
    /// Creates a shortcut that runs btaudio with the given arguments and returns its path.
    /// <paramref name="icon"/> names one of the embedded icons: connect, disconnect or toggle.
    /// </summary>
    string Create(string folder, string title, string arguments, string icon);

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

    private static string IconFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Bluetooth Audio Switch",
            "Icons"
        );

    public string Create(string folder, string title, string arguments, string icon)
    {
        var exePath =
            Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to determine the path of btaudio.exe.");

        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, SanitizeFileName(title) + ".lnk");

        ShellLink.Create(path, exePath, arguments, title, ExtractIcon(icon), 0);
        return path;
    }

    /// <summary>
    /// Shortcuts can't point at a managed resource, so the icon is written to a stable
    /// per-user folder. It stays valid even if btaudio.exe is later moved.
    /// </summary>
    private static string ExtractIcon(string icon)
    {
        using var resource =
            typeof(ShortcutService).Assembly.GetManifestResourceStream($"Icons.{icon}.ico")
            ?? throw new ArgumentException($"No embedded icon named '{icon}'.", nameof(icon));
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        var bytes = buffer.ToArray();

        Directory.CreateDirectory(IconFolder);
        var iconPath = Path.Combine(IconFolder, $"{icon}.ico");
        if (!File.Exists(iconPath) || !File.ReadAllBytes(iconPath).AsSpan().SequenceEqual(bytes))
        {
            File.WriteAllBytes(iconPath, bytes);
        }

        return iconPath;
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
