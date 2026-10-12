namespace BluetoothAudioSwitch.Bluetooth;

/// <summary>A paired Bluetooth device.</summary>
/// <param name="Name">Display name, e.g. "Chris's Buds4".</param>
/// <param name="IsConnected">Whether the device is currently connected.</param>
/// <param name="AudioFilterIds">Kernel-streaming filter IDs used to connect/disconnect audio. Empty for non-audio devices.</param>
internal sealed record PairedDevice(string Name, bool IsConnected, IReadOnlyList<string> AudioFilterIds)
{
    public bool IsAudio => AudioFilterIds.Count > 0;
}

internal interface IBluetoothAudioService
{
    Task<IReadOnlyList<PairedDevice>> GetPairedDevicesAsync(CancellationToken cancellationToken = default);

    void Connect(PairedDevice device);

    void Disconnect(PairedDevice device);
}

internal sealed class BluetoothAudioException(string message) : Exception(message);
