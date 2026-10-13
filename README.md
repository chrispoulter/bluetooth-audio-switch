# Bluetooth Audio Switch

Connect/disconnect paired Bluetooth audio devices from the command line or a desktop shortcut.

## Usage

```text
btaudio list                         List paired Bluetooth audio devices
btaudio list --all                   ...including non-audio devices (mice, keyboards, ...)
btaudio connect "Chris's Buds4"      Connect a device
btaudio disconnect "Chris's Buds4"   Disconnect a device
btaudio shortcut "Chris's Buds4"     Create Connect/Disconnect shortcuts on the desktop
btaudio list --verbose               Any command with detailed diagnostics on stderr
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
```

The shortcuts run `btaudio.exe` minimised. Each one has a Bluetooth icon with a badge for its action: a green tick for Connect and a red cross for Disconnect. The icons are saved to `%LOCALAPPDATA%\Bluetooth Audio Switch\Icons\`. The shortcuts point to wherever `btaudio.exe` was when you created them, so put the exe somewhere permanent first (for example `%LOCALAPPDATA%\Programs\btaudio\`).

A Start Menu shortcut can also be pinned to Start or the taskbar. You can give it a keyboard shortcut in its Properties dialog.

### Exit codes

| Code | Meaning                                         |
| ---- | ----------------------------------------------- |
| 0    | Success                                         |
| 1    | Invalid command line                            |
| 2    | No paired audio device matches the name         |
| 3    | The name matches more than one device           |
| 4    | Windows rejected the connect/disconnect request |

`connect` and `disconnect` send the request to Windows and return straight away. The connection itself takes a few seconds, and the device must be switched on and in range (earbuds out of their case).

### Logs

Each run is logged to `%LOCALAPPDATA%\Bluetooth Audio Switch\Logs\btaudio-<date>.log`. There's one file per day, and files older than 7 days are deleted. If a shortcut seems to do nothing, check the latest log: the shortcuts run minimised, so their error messages are only recorded there.

`--verbose` (`-v`) works with every command. It adds detail to the log (every audio endpoint, its kernel-streaming filter and container ID, and the HRESULT from each connect/disconnect request) and also prints it to stderr. stdout isn't affected, so `btaudio list -v > devices.txt` still saves only the table.

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
dotnet publish src/BluetoothAudioSwitch -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
# Output: src/BluetoothAudioSwitch/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/btaudio.exe
```

### Visual Studio 2026

Open `BluetoothAudioSwitch.slnx`. Debug profiles (`list`, `connect`, `disconnect`, `help`) are in the run button drop-down. They're defined in `src/BluetoothAudioSwitch/Properties/launchSettings.json`; change the device name there.

### VS Code

Install the recommended **C# Dev Kit** extension. Then:

- **Ctrl+Shift+B**: build
- **Run and Debug** (F5): choose `btaudio list`, `connect` or `disconnect`. You'll be asked for the device name.
- **Tasks: Run Task**: `test` or `publish`

### Formatting

C# and project files are formatted with [CSharpier](https://csharpier.com). It's installed as a local tool, and settings are in `.csharpierrc.json`. CI fails if anything isn't formatted.

```powershell
dotnet tool restore           # once, after cloning
dotnet csharpier format .     # format everything
dotnet csharpier check .      # what CI runs
```

For editor support, install the **CSharpier** extension. In VS Code it's recommended for this workspace and set as the C# formatter, so **Format Document** (Shift+Alt+F) uses it. In Visual Studio 2026, install it from the Marketplace and run **Reformat with CSharpier**.

## CI/CD

GitHub Actions ([.github/workflows/ci.yml](.github/workflows/ci.yml)):

- **Every push to any branch:** builds, tests and checks formatting on Windows. Pushes that only change Markdown, `.vscode/` or `.gitignore` files are skipped.
- **Tags matching `v*.*.*`:** also publishes `win-x64` and `win-arm64` single-file builds and creates a GitHub Release with the zipped exes attached. Path filters don't apply to tags, so a tag always releases.
- **Manual run** (*Run workflow* in the Actions tab): builds and publishes the zips as workflow artifacts, without a release.

```powershell
git tag v1.0.0
git push origin v1.0.0
```

## Project layout

```text
src/BluetoothAudioSwitch/
  Program.cs                  Entry point
  Logging.cs                  Serilog setup (rolling log file, --verbose)
  Commands/                   System.CommandLine commands, shortcut creation, exit codes
  Bluetooth/                  Device discovery, name matching, connect/disconnect
  Interop/                    Core Audio / Kernel Streaming / Shell Link COM definitions
tests/BluetoothAudioSwitch.Tests/   xUnit tests (no Bluetooth hardware needed)
```

## License

[MIT](LICENSE)
