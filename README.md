# Bluetooth Audio Switch

[![CI](https://github.com/chrispoulter/bluetooth-audio-switch/actions/workflows/ci.yml/badge.svg)](https://github.com/chrispoulter/bluetooth-audio-switch/actions/workflows/ci.yml)

`btaudio` is a small Windows command-line tool that connects or disconnects paired Bluetooth audio devices (headphones, earbuds, speakers) by name.

Use it to make desktop or Start Menu shortcuts that switch your headphones with one click, so you don't have to go through Settings > Bluetooth & devices.

## Usage

```text
btaudio list                         List paired Bluetooth audio devices
btaudio list --all                   ...including non-audio devices (mice, keyboards, ...)
btaudio connect "Chris's Buds4"      Connect a device
btaudio disconnect "Chris's Buds4"   Disconnect a device
btaudio toggle "Chris's Buds4"       Connect if disconnected, otherwise disconnect
btaudio shortcut "Chris's Buds4"     Create Connect/Disconnect shortcuts on the desktop
btaudio --help                       Show help (works for every command: btaudio connect --help)
```

```text
> btaudio list
NAME           STATUS        TYPE
Chris's Buds4  Disconnected  Audio
```

### Device names

Names are matched loosely, so you rarely need to type the full name:

- Case and extra spaces are ignored. Curly apostrophes (`’`) count as straight ones (`'`).
- An exact match always wins.
- Otherwise any unique part of the name works: `btaudio connect buds`.
- Each word can also be the start of a word in the name: `btaudio connect "chris buds"`.

If a name matches more than one device, `btaudio` lists the matches and does nothing.

### Shortcuts

```text
btaudio shortcut "Chris's Buds4"                      Desktop: "Connect Chris's Buds4" + "Disconnect Chris's Buds4"
btaudio shortcut "Chris's Buds4" --start-menu         Start Menu > Bluetooth Audio Switch
btaudio shortcut "Chris's Buds4" --desktop --start-menu
btaudio shortcut "Chris's Buds4" --toggle             A single "Toggle Chris's Buds4" shortcut
```

The shortcuts run `btaudio.exe` minimised and use the Windows Bluetooth icon. They point to wherever `btaudio.exe` was when you created them, so put the exe somewhere permanent first (for example `%LOCALAPPDATA%\Programs\btaudio\`).

A Start Menu shortcut can also be pinned to Start or the taskbar. You can give it a keyboard shortcut in its Properties dialog.

### Exit codes

| Code | Meaning |
| ---- | ------- |
| 0 | Success |
| 1 | Invalid command line |
| 2 | No paired audio device matches the name |
| 3 | The name matches more than one device |
| 4 | Windows rejected the connect/disconnect request |

`connect` and `disconnect` send the request to Windows and return straight away. The connection itself takes a few seconds, and the device must be switched on and in range (earbuds out of their case).

## How it works

Windows has no public "connect this Bluetooth device" API. `btaudio` uses the same mechanism as the **Connect** button in Settings:

1. List the audio endpoints with Core Audio (`IMMDeviceEnumerator`), including disconnected ones.
2. Follow each endpoint's topology to its kernel-streaming filter, and keep only the Bluetooth ones (`BTHENUM` / `BTHHFENUM`).
3. Match endpoints to paired devices by container ID. Names and connection state come from `Windows.Devices.Enumeration`.
4. Send `KSPROPERTY_ONESHOT_RECONNECT` or `KSPROPERTY_ONESHOT_DISCONNECT` (from `KSPROPSETID_BtAudio`) through `IKsControl`.

Supports classic Bluetooth audio (A2DP / Hands-Free). Bluetooth LE Audio-only devices aren't supported.

## Install

Download `btaudio-<version>-win-x64.zip` (or `win-arm64`) from the [Releases](../../releases) page. Extract `btaudio.exe` somewhere permanent and, optionally, add that folder to your `PATH`. The exe is self-contained, so you don't need to install .NET.

Requires Windows 10 version 2004 (build 19041) or later.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build
dotnet test
dotnet run --project src/BluetoothAudioSwitch -- list

# Self-contained single-file exe
dotnet publish src/BluetoothAudioSwitch -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o artifacts/win-x64
```

### Visual Studio 2026

Open `BluetoothAudioSwitch.slnx`. Debug profiles (`list`, `connect`, `disconnect`, `toggle`, `help`) are in the run button drop-down. They're defined in `src/BluetoothAudioSwitch/Properties/launchSettings.json`; change the device name there.

### VS Code

Install the recommended **C# Dev Kit** extension. Then:

- **Ctrl+Shift+B**: build
- **Run and Debug** (F5): choose `btaudio list`, `connect`, `disconnect` or `toggle`. You'll be asked for the device name.
- **Tasks: Run Task**: `test` or `publish`

## CI/CD

GitHub Actions ([.github/workflows/ci.yml](.github/workflows/ci.yml)):

- **Every push or pull request to `main`:** builds and tests on Windows, then publishes `win-x64` and `win-arm64` single-file builds as workflow artifacts.
- **Tags matching `v*`:** also creates a GitHub Release with the zipped exes attached.

```powershell
git tag v1.0.0
git push origin v1.0.0
```

## Project layout

```text
src/BluetoothAudioSwitch/
  Program.cs                  Entry point
  Commands/                   System.CommandLine commands, shortcut creation, exit codes
  Bluetooth/                  Device discovery, name matching, connect/disconnect
  Interop/                    Core Audio / Kernel Streaming / Shell Link COM definitions
tests/BluetoothAudioSwitch.Tests/   xUnit tests (no Bluetooth hardware needed)
```

## License

[MIT](LICENSE)
