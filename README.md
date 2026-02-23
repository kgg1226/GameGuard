# GameGuard

A lightweight Windows system tray app that blocks game launchers and game executables during user-defined time windows. Built as a self-control tool — define when gaming is blocked, and GameGuard enforces it with a 5-minute warning before closing the process.

## Features

- **Blocked time windows** — define days and time ranges when apps are blocked (e.g. weeknights 23:00–07:00)
- **Overnight window support** — windows that span midnight work correctly (e.g. 23:00–07:00)
- **Two app kinds** — register launchers (Steam, Epic, etc.) or individual game executables with sensible path-pinning defaults
- **5-minute grace period** — balloon warning shown on detection; process is only terminated after the full grace period elapses
- **Grace cancellation** — if the blocked window ends before 5 minutes are up, the timer is cancelled and the app is not killed
- **Structured audit log** — append-only JSONL at `%AppData%\GameGuard\log.jsonl`
- **Start with Windows** — toggle from the tray menu; no admin rights required
- **No background service** — single tray process, kill it and enforcement stops

## Download

Grab the latest self-contained build (no .NET install needed) from [Releases](../../releases/latest).

1. Download and extract `GameGuard-v2.0.0-win-x64.zip`
2. Run `GameGuard.exe`
3. A shield icon appears in your system tray
4. Right-click → **Open Settings** to configure

## Configuration

Settings are stored in `%AppData%\GameGuard\config.json` and can be edited manually while the app is closed. If the file is missing or unreadable, GameGuard starts with empty defaults and shows a balloon warning.

```json
{
  "blockedApps": [
    {
      "id": "a1b2c3d4",
      "displayName": "Steam",
      "kind": "launcher",
      "processName": "steam.exe",
      "path": "C:\\Program Files (x86)\\Steam\\steam.exe",
      "pathPinned": false
    },
    {
      "id": "e5f6g7h8",
      "displayName": "MyGame",
      "kind": "game",
      "processName": "mygame.exe",
      "path": "C:\\Games\\MyGame\\mygame.exe",
      "pathPinned": true
    }
  ],
  "blockedWindows": [
    {
      "days": [0, 1, 2, 3, 4, 5, 6],
      "start": "23:00",
      "end": "07:00"
    }
  ],
  "graceSeconds": 300,
  "pollIntervalSeconds": 3,
  "toastCooldownSeconds": 300
}
```

`days`: `0` = Sunday, `1` = Monday … `6` = Saturday

`blockedWindows` defines when apps are **blocked**. An empty list means no enforcement is active.

### Overnight windows

When `start > end`, the window spans midnight. For example `"start": "23:00", "end": "07:00"` on `days: [1]` (Monday):

| Time | Day | Blocked? |
|---|---|---|
| 23:30 | Monday | Yes — evening portion |
| 02:00 | Tuesday | Yes — morning portion (Monday's window carries over) |
| 08:00 | Tuesday | No |

### Path pinning

| `pathPinned` | Behaviour |
|---|---|
| `false` | Match by process name only. Default for **launchers** (paths may change after updates). |
| `true` | Only block if `MainModule.FileName` matches the registered path exactly. Default for **games** (prevents rename bypass). If the path cannot be verified (e.g. elevated process), termination is **skipped** and `verify_failed` is logged. |

## Tray Menu

| Item | Action |
|---|---|
| Open Settings | Configure blocked apps, blocked windows, and advanced options |
| Status | Balloon showing whether enforcement is active right now |
| Open Log Folder | Opens `%AppData%\GameGuard\` in Explorer |
| Start with Windows | Toggles the HKCU startup registry entry |
| Exit | Stops enforcement and removes the tray icon |

## Log Events

Each line in `log.jsonl` is a JSON object:

```json
{"timestamp":"2026-02-24T23:00:05Z","event":"blocked_detected","process":"steam.exe","detail":"pid=1234"}
{"timestamp":"2026-02-24T23:00:05Z","event":"grace_started","process":"steam.exe","detail":"pid=1234, plannedKillAt=2026-02-24T23:05:05Z"}
{"timestamp":"2026-02-24T23:05:06Z","event":"terminated_success","process":"steam.exe","detail":"pid=1234"}
```

| Event | Meaning |
|---|---|
| `blocked_detected` | Blocked process found running during a blocked window |
| `grace_started` | Grace timer started; includes `plannedKillAt` |
| `terminated_success` | Process was killed after grace period |
| `terminated_failed` | Kill attempt threw an unexpected exception |
| `terminate_skipped` | Kill skipped — process is elevated (access denied) |
| `verify_failed` | `pathPinned=true` but `MainModule.FileName` could not be read; termination skipped |
| `app_registered` | App added via Settings UI |
| `app_removed` | App removed via Settings UI |
| `blocked_window_added` | Blocked window added via Settings UI |
| `blocked_window_removed` | Blocked window removed via Settings UI |
| `config_changed` | Settings saved |
| `config_load_error` | `config.json` was unreadable or malformed; defaults applied |
| `monitor_error` | Unexpected error in poll loop (logged and swallowed to keep monitor running) |

## Building from Source

**Requirements:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Windows 10/11 x64

```bat
git clone https://github.com/kgg1226/GameGuard.git
cd GameGuard
dotnet build -c Release
dotnet run
```

**Publish a self-contained single-file exe:**

```bat
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## Migrating from v1

The config schema changed in v2. The `schedule` key (allowed windows) was replaced by `blockedWindows` (blocked windows) with inverted semantics. v1 configs will not load — delete `%AppData%\GameGuard\config.json` and reconfigure via the Settings UI.

## Security Notes

- No network calls — ever
- No shell command execution — process control uses .NET APIs only
- No kernel drivers, no process injection, no keylogging
- No anti-removal techniques — delete the exe and it's gone
- Startup entry is a plain `HKCU` registry value, visible in Task Manager → Startup apps
- Config and logs are stored in your own `%AppData%` folder
- Elevated processes cannot be terminated (access denied is logged, not retried)

## Requirements

- Windows 10 or 11 (x64)
- .NET 8 runtime — or use the self-contained release build (no install needed)
