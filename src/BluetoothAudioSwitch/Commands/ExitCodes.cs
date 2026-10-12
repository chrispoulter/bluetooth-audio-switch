namespace BluetoothAudioSwitch.Commands;

internal static class ExitCodes
{
    public const int Success = 0;

    /// <summary>Invalid command line (returned by System.CommandLine).</summary>
    public const int InvalidUsage = 1;

    public const int DeviceNotFound = 2;
    public const int AmbiguousName = 3;
    public const int OperationFailed = 4;
}
