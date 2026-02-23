# GameGuard

A lightweight Windows system tray app that limits game launcher execution to configured time windows. Built as a self-control tool — set the hours you're allowed to play, and GameGuard enforces them.

## Features

- **Time-based schedule** — define allowed days and time ranges (e.g. weekdays 21:00–23:00)
- **Blocked app list** — add any executable via file picker; optionally pin to exact path
- **Grace period** — shows a balloon warning before closing the process (default 10s)
- **Structured logging** — append-only JSONL log of all events at `%AppData%\GameGuard\log.jsonl`
- **Start with Windows** — toggle from the tray menu; no admin rights required
- **No background service** — single tray process, kill it and it stops

## Screenshot

> Tray icon → right-click for the full menu

| Settings — Blocked Apps | Settings — Schedule |
|---|---|
| Add any `.exe` via file picker | Set allowed days and time windows |

## Download

Grab the latest self-contained build (no .NET install needed) from [Releases](../../releases/latest).

1. Download and extract `GameGuard-v1.0.0-win-x64.zip`
2. Run `GameGuard.exe`
3. A shield icon appears in your system tray
4. Right-click → **Open Settings** to configure

## Configuration

Settings are stored in `%AppData%\GameGuard\config.json` and can be edited manually while the app is closed.

```json
{
  "blockedApps": [
    {
      "id": "a1b2c3d4",
      "processName": "steam.exe",
      "path": "C:\\Program Files (x86)\\Steam\\steam.exe",
      "pathPinned": true
    }
  ],
  "schedule": [
    {
      "days": [1, 2, 3, 4, 5],
      "start": "21:00",
      "end": "23:00"
    }
  ],
  "graceSeconds": 10,
  "pollIntervalSeconds": 3,
  "toastCooldownSeconds": 300
}
```

`days`: `0` = Sunday, `1` = Monday … `6` = Saturday

## Tray Menu

| Item | Action |
|---|---|
| Open Settings | Configure blocked apps, schedule, and advanced options |
| Current Status | Balloon showing whether games are allowed right now |
| Open Log Folder | Opens `%AppData%\GameGuard\` in Explorer |
| Start with Windows | Toggles the HKCU startup registry entry |
| Exit | Stops enforcement and removes the tray icon |

## Log Events

Each line in `log.jsonl` is a JSON object:

```json
{"timestamp":"2026-02-24T12:00:00Z","event":"app_detected","process":"steam.exe","detail":"pid=1234"}
{"timestamp":"2026-02-24T12:00:13Z","event":"app_terminated","process":"steam.exe","detail":"pid=1234"}
{"timestamp":"2026-02-24T12:05:00Z","event":"config_changed","process":null,"detail":null}
```

| Event | Meaning |
|---|---|
| `app_detected` | Blocked process found running outside allowed window |
| `app_terminated` | Process was killed after grace period |
| `allowed_execution` | Blocked process detected but currently inside allowed window |
| `config_changed` | Settings were saved |
| `terminate_failed` | Kill attempt failed (logged with reason) |

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

## Security Notes

- No network calls — ever
- No shell command execution — process control uses .NET APIs only
- No kernel drivers, no process injection, no keylogging
- No anti-removal techniques — delete the exe and it's gone
- Startup entry is a plain `HKCU` registry value, visible in Task Manager → Startup apps
- Config and logs are stored in your own `%AppData%` folder

## Requirements

- Windows 10 or 11 (x64)
- .NET 8 runtime — or use the self-contained release build (no install needed)
