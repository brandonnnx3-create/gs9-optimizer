# G.S.9 — Connection Optimizer

Native Windows desktop app (C# · .NET 10 · WPF) that puts a professional interface on top of the G.S.9 optimization scripts. It:

- detects the active network adapter and shows real data: type, adapter, link speed, status, IPv4, gateway, DNS, interface GUID;
- runs each script on its own or in sequence (ACTIVATE ALL);
- checks whether `Network_Tweaks` is active using the existing verifier script;
- shows what is running, what finished, the exit codes, and the full output of every run.

**The optimization logic lives only in the scripts in `scripts/`.** The app runs them exactly as they are and never rewrites them.

## Build and run

Requires Windows 10/11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet run --project src/ConnectionOptimizer
```

Single-file build (needs the .NET 10 Desktop Runtime on the target PC):

```powershell
dotnet publish src/ConnectionOptimizer -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

`publish/` then contains `ConnectionOptimizer.exe` and a `Scripts\` folder. Keep them together.

## Project structure

```
scripts/                       The G.S.9 scripts, byte-for-byte as provided (.gitattributes keeps them untouched)
docs/DESIGN_SYSTEM.md          Visual identity rules: tokens, type, states, components
src/ConnectionOptimizer/
  Models/                      OptimizationDefinition + OptimizationCatalog (which script, name, category, warnings)
  Services/                    ScriptRunner, ScriptVerifier, NetworkInfoService, Elevation (no UI code)
  ViewModels/                  MainViewModel (flows), OptimizationViewModel (one tool), Connection, Activity
  Views/                       MainWindow, OptimizationCell, StatusBadge, ConfirmDialog, LogWindow
  Themes/                      Tokens.xaml, Typography.xaml, Controls.xaml (the design system in XAML)
```

Architecture: MVVM with no external packages. The layers are UI → view models (state and flows) → services → `cmd.exe` → script.

## How scripts are run

- Each script runs through `cmd.exe /d /c "Scripts\<file>"` with its output captured. The UI stays responsive because everything is `async`.
- **Only one script runs at a time.** Buttons are disabled while something runs, and the runner refuses a second run.
- Every script ends with `PAUSE`. The app closes the script's input, so `PAUSE` returns immediately instead of waiting for a key. No script is modified to achieve this.
- Result: **exit code 0 → APPLIED · unverified; any other code → ERROR.** A batch script's exit code comes from its last command, so "APPLIED" means the script ran to the end, not that every tweak took effect. Only Registry Tweaks has a verifier, so it is the only tool that can show **ACTIVE / PARTIAL / INACTIVE**.
- The verifier runs at start-up, after every script (scripts overlap: Minecraft PvP writes some of the same values), and on **CHECK STATUS**.
- Logs are written to `%LOCALAPPDATA%\GS9\ConnectionOptimizer\Logs\`: one file per run, plus a daily activity log.

## Administrator rights

The app starts **without** elevation (`asInvoker`). Reading the connection and checking the status do not need it. When you run a script, the app explains why it needs administrator rights and restarts itself through UAC, then continues the action you chose. That means one UAC prompt per session, and none if you only look.

## Replacing or updating a script

Replace the file in `Scripts\`, next to the executable (or in `scripts/` before building), **keeping the same name**. Nothing else needs to change. Names, descriptions and warnings are in `Models/OptimizationCatalog.cs`.

## Known issues in the scripts (not fixed: the scripts are only changed with the owner's approval)

| Script | Issue | Effect |
|---|---|---|
| `AdvancedWindowsCleanup.bat` | `"%TEMP%*"` and `"C:\Windows\Temp*"` are missing a `\` before `*`. Combined with `del /s`, they match **any file whose name starts with "Temp"** (for example `Template…`) anywhere under `%LOCALAPPDATA%` and `C:\Windows`. The `for /d … rd /s /q` lines then remove the whole `Temp` folders. | Can delete unrelated files. The intended pattern is `"%TEMP%\*"`. |
| `AdvancedWindowsCleanup.bat` | `Recent*` and `Profiles*` have the same missing `\`. | Recent items and the Firefox cache are not cleaned. |
| `AdvancedWindowsCleanup.bat` | The last command is `Clear-RecycleBin`, which likely returns an error when the Recycle Bin is already empty. | The app may show ERROR (exit 1) even though the cleanup ran. |
| `NetworkLatencyOptimizer.bat` | `Set-NetIPInterface -InterfaceAlias '*' -InterfaceMetric 1` gives **every** interface the same metric. | Does not prioritize the active interface; with Wi-Fi + Ethernet or a VPN, Windows' choice of route becomes a tie. |
| `NetworkLatencyOptimizer.bat` | Adapter properties are set by `DisplayName` (driver- and language-dependent) with `SilentlyContinue`. | On adapters with other names the changes are silently skipped; exit code is still 0. |
| `AntiInputLag.bat` | `cleanmgr /sageset:1` opens an interactive window and waits for OK. | The run pauses until you close that window. |
