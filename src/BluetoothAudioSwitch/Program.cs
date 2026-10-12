using BluetoothAudioSwitch.Bluetooth;
using BluetoothAudioSwitch.Commands;

var root = AppCommands.Build(new BluetoothAudioService(), new ShortcutService());
return await root.Parse(args).InvokeAsync();
