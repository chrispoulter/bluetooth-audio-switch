using System.Runtime.InteropServices;
using BluetoothAudioSwitch.Interop;
using Serilog;
using Windows.Devices.Enumeration;
using WinRtBluetoothDevice = Windows.Devices.Bluetooth.BluetoothDevice;

namespace BluetoothAudioSwitch.Bluetooth;

/// <summary>
/// Finds paired Bluetooth devices and (re)connects or disconnects their audio
/// profiles using the Bluetooth audio kernel-streaming property set
/// (KSPROPSETID_BtAudio ONESHOT_RECONNECT / ONESHOT_DISCONNECT) — the same
/// mechanism Windows uses for the "Connect" button in Settings.
/// </summary>
internal sealed class BluetoothAudioService : IBluetoothAudioService
{
    private const string IsConnectedProperty = "System.Devices.Aep.IsConnected";
    private const string ContainerIdProperty = "System.Devices.Aep.ContainerId";

    // Resolved per call: Log.Logger is configured after this service is created.
    private static ILogger Logger => Log.ForContext<BluetoothAudioService>();

    private sealed record AudioEndpoint(
        string FilterId,
        Guid? ContainerId,
        string? Name,
        bool IsActive,
        bool IsBluetoothFilter
    );

    public async Task<IReadOnlyList<PairedDevice>> GetPairedDevicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var endpoints = GetAudioEndpoints();

        var paired = await DeviceInformation
            .FindAllAsync(
                WinRtBluetoothDevice.GetDeviceSelectorFromPairingState(true),
                [IsConnectedProperty, ContainerIdProperty],
                DeviceInformationKind.AssociationEndpoint
            )
            .AsTask(cancellationToken);

        var devices = new List<PairedDevice>();
        var claimed = new HashSet<AudioEndpoint>();

        foreach (var info in paired)
        {
            var containerId =
                info.Properties.TryGetValue(ContainerIdProperty, out var c) && c is Guid g
                    ? g
                    : (Guid?)null;
            var deviceEndpoints = containerId is null
                ? []
                : endpoints.Where(e => e.ContainerId == containerId).ToList();
            claimed.UnionWith(deviceEndpoints);

            var isConnected =
                info.Properties.TryGetValue(IsConnectedProperty, out var ic) && ic is bool b
                    ? b
                    : deviceEndpoints.Any(e => e.IsActive);

            Logger.Debug(
                "Paired device {Name}: container {ContainerId}, connected {IsConnected}, {EndpointCount} audio endpoint(s)",
                info.Name,
                containerId,
                isConnected,
                deviceEndpoints.Count
            );

            devices.Add(new PairedDevice(info.Name, isConnected, FilterIds(deviceEndpoints)));
        }

        // Bluetooth audio endpoints whose container could not be matched to a paired
        // device are still usable; group them by the name Core Audio reports.
        foreach (
            var group in endpoints
                .Except(claimed)
                .Where(e => e.IsBluetoothFilter && e.Name is not null)
                .GroupBy(e => e.Name!)
        )
        {
            Logger.Debug(
                "Unmatched Bluetooth endpoints grouped as {Name}: {FilterIds}",
                group.Key,
                FilterIds(group)
            );

            devices.Add(new PairedDevice(group.Key, group.Any(e => e.IsActive), FilterIds(group)));
        }

        return devices.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public void Connect(PairedDevice device) =>
        SendOneShot(device, KsBluetoothAudio.OneShotReconnect);

    public void Disconnect(PairedDevice device) =>
        SendOneShot(device, KsBluetoothAudio.OneShotDisconnect);

    private static IReadOnlyList<string> FilterIds(IEnumerable<AudioEndpoint> endpoints) =>
        endpoints.Select(e => e.FilterId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static void SendOneShot(PairedDevice device, uint propertyId)
    {
        if (!device.IsAudio)
        {
            throw new BluetoothAudioException($"'{device.Name}' is not a Bluetooth audio device.");
        }

        var enumerator = CreateEnumerator();
        var succeeded = 0;
        var errors = new List<string>();

        foreach (var filterId in device.AudioFilterIds)
        {
            try
            {
                enumerator.GetDevice(filterId, out var filterDevice);
                var iid = ComIds.KsControlIid;
                filterDevice.Activate(ref iid, ComIds.ClsCtxAll, IntPtr.Zero, out var instance);
                var ksControl = (IKsControl)instance;

                var property = new KsProperty
                {
                    Set = KsBluetoothAudio.PropertySet,
                    Id = propertyId,
                    Flags = KsBluetoothAudio.KsPropertyTypeGet,
                };

                var hr = ksControl.KsProperty(
                    ref property,
                    (uint)Marshal.SizeOf<KsProperty>(),
                    IntPtr.Zero,
                    0,
                    out _
                );

                Logger.Debug("KsProperty on {FilterId} returned 0x{HResult:X8}", filterId, hr);

                if (hr >= 0)
                {
                    succeeded++;
                }
                else
                {
                    errors.Add($"0x{hr:X8}");
                }
            }
            catch (COMException ex)
            {
                Logger.Debug(ex, "KsProperty on {FilterId} threw", filterId);
                errors.Add($"0x{ex.HResult:X8}");
            }
        }

        Logger.Information(
            "{Request} for {Device}: {Succeeded} of {Total} filter(s) accepted",
            propertyId == KsBluetoothAudio.OneShotReconnect ? "Reconnect" : "Disconnect",
            device.Name,
            succeeded,
            device.AudioFilterIds.Count
        );

        if (errors.Count > 0)
        {
            Logger.Warning("Rejected with HRESULT(s) {Errors}", errors.Distinct());
        }

        if (succeeded == 0)
        {
            throw new BluetoothAudioException(
                $"Windows rejected the request for '{device.Name}' (HRESULT {string.Join(", ", errors.Distinct())}). "
                    + "Make sure the device is switched on and in range."
            );
        }
    }

    private static IMMDeviceEnumerator CreateEnumerator()
    {
        var type = Type.GetTypeFromCLSID(ComIds.MMDeviceEnumeratorClsid, throwOnError: true)!;
        return (IMMDeviceEnumerator)Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// All audio endpoints that could belong to a Bluetooth device. Endpoints are
    /// matched to paired devices by container ID rather than filter path, because
    /// with Bluetooth audio offload (e.g. Intel SST) the Hands-Free endpoints are
    /// wired to the audio controller's filter, not a BTHENUM/BTHHFENUM one.
    /// </summary>
    private static List<AudioEndpoint> GetAudioEndpoints()
    {
        var enumerator = CreateEnumerator();
        enumerator.EnumAudioEndpoints(
            EDataFlow.All,
            DeviceState.Active | DeviceState.Unplugged,
            out var collection
        );
        collection.GetCount(out var count);

        var endpoints = new List<AudioEndpoint>();
        for (uint i = 0; i < count; i++)
        {
            collection.Item(i, out var endpoint);
            var filterId = GetConnectedFilterId(endpoint);
            if (filterId is null)
            {
                continue;
            }

            endpoint.GetState(out var state);
            endpoint.OpenPropertyStore(ComIds.StgmRead, out var store);
            var containerId = ReadGuid(store, PropertyKeys.DeviceContainerId);
            var name = ReadString(store, PropertyKeys.DeviceInterfaceFriendlyName);

            Logger.Debug(
                "Endpoint {Name}: filter {FilterId}, container {ContainerId}, state {State}, Bluetooth filter {IsBluetoothFilter}",
                name,
                filterId,
                containerId,
                state,
                IsBluetoothFilter(filterId)
            );

            endpoints.Add(
                new AudioEndpoint(
                    filterId,
                    containerId,
                    name,
                    state == DeviceState.Active,
                    IsBluetoothFilter(filterId)
                )
            );
        }

        return endpoints;
    }

    /// <summary>The device ID of the kernel-streaming filter the endpoint is wired to.</summary>
    private static string? GetConnectedFilterId(IMMDevice endpoint)
    {
        try
        {
            var iid = ComIds.DeviceTopologyIid;
            endpoint.Activate(ref iid, ComIds.ClsCtxAll, IntPtr.Zero, out var instance);
            var topology = (IDeviceTopology)instance;
            topology.GetConnector(0, out var connector);
            connector.GetDeviceIdConnectedTo(out var filterId);
            return filterId;
        }
        catch (COMException ex)
        {
            Logger.Debug("Endpoint topology unavailable: 0x{HResult:X8}", ex.HResult);
            return null;
        }
    }

    // Classic Bluetooth audio filters live under BTHENUM (A2DP) or BTHHFENUM (Hands-Free).
    private static bool IsBluetoothFilter(string filterId) =>
        filterId.Contains(@"\\?\bthenum", StringComparison.OrdinalIgnoreCase)
        || filterId.Contains(@"\\?\bthhfenum", StringComparison.OrdinalIgnoreCase);

    private static string? ReadString(IPropertyStore store, PropertyKey key) =>
        ReadValue(store, key, v => v.AsString());

    private static Guid? ReadGuid(IPropertyStore store, PropertyKey key) =>
        ReadValue(store, key, v => v.AsGuid());

    private static T? ReadValue<T>(
        IPropertyStore store,
        PropertyKey key,
        Func<PropVariant, T?> read
    )
    {
        try
        {
            store.GetValue(ref key, out var value);
            try
            {
                return read(value);
            }
            finally
            {
                NativeMethods.PropVariantClear(ref value);
            }
        }
        catch (COMException ex)
        {
            Logger.Debug(
                "Property {FormatId},{PropertyId} unavailable: 0x{HResult:X8}",
                key.FormatId,
                key.PropertyId,
                ex.HResult
            );

            return default;
        }
    }
}
