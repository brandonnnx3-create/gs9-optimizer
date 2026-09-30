# G.S.9 — Connection Optimizer

Native Windows desktop app (C# · .NET 10 · WPF) that puts a professional interface on top of the G.S.9 optimization scripts. It:

- detects the active network adapter and shows real data: type, adapter, link speed, status, IPv4, gateway, DNS, interface GUID;
- runs each script on its own or in sequence (ACTIVATE ALL);
- checks whether `Network_Tweaks` is active using the existing verifier script;
- gates access with a revocable online allowlist keyed to each PC's hardware ID;
- shows what is running, what finished and the exit codes, without showing or saving what the scripts print.

**The optimization logic lives only in the scripts in `scripts/`.** The app runs them exactly as they are and never rewrites them. In a build they are encrypted and embedded in the executable, so the distributed app has no visible `Scripts` folder.

## Build and run

Building requires Windows 10/11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet run --project src/ConnectionOptimizer
```

**Distributable build (no .NET needed on the target PC):**

```powershell
dotnet publish src/ConnectionOptimizer -p:PublishProfile=win-x64
```

`publish/ConnectionOptimizer/` then contains a single `ConnectionOptimizer.exe` (about 65 MB, with the .NET runtime inside) and the `Scripts\` folder. Keep them together. The first launch unpacks a few native files to `%TEMP%\.net\`, so it is a little slower than later launches.

## Private access (online allowlist, revocable)

The app opens only on PCs whose **hardware ID is on a signed allowlist** the owner hosts online. At startup the app downloads the list, checks the signature and its own hardware ID, and shows a lock screen otherwise. Because it checks online, the owner can **revoke** a PC at any time by removing it from the list.

- **Hardware ID** = hash of the motherboard UUID + motherboard serial (SMBIOS) + the Windows `MachineGuid`, shown as `XXXX-XXXX-XXXX-XXXX`. It changes if Windows is reinstalled or the motherboard is replaced.
- **Allowlist** = a signed file listing each authorized hardware ID, a name, and an optional expiry, signed with an ECDSA P-256 private key. The app has only the public key, so it can verify the list but not forge it. Editing the list breaks its signature.
- **Hosting**: the list lives at a URL the owner controls (e.g. a GitHub gist "raw" link), baked into the build at `Services/Licensing/AccessConfig.cs` (`AllowlistUrl`). It is baked in on purpose — if it could be changed on the user's PC, a revoked user could point it at an old copy and dodge the revocation.
- **Offline grace**: after a successful check the result is cached; the app keeps working offline for `GracePeriod` (3 days) before it must reconnect. So a brief outage does not lock people out, and a revocation takes effect within 3 days even if the PC is kept offline (immediately if it is online).

**Getting a PC's hardware ID** before authorizing it: send `GS9-HWID.exe` (`tools/HwidDetector`, ~11 MB, no .NET, no admin). It shows the ID and copies it to the clipboard.

```powershell
dotnet publish tools/HwidDetector -c Release -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:EnableCompressionInSingleFile=true -o publish/hwid
```

**Managing access** (owner only; needs the private key). These update `allowlist.json` (kept by the owner) and write `allowlist.signed`, which the owner uploads to the gist:

```powershell
dotnet run --project tools/LicenseTool -- allow add    key.pem XXXX-XXXX-XXXX-XXXX "PC name" [YYYY-MM-DD]
dotnet run --project tools/LicenseTool -- allow remove key.pem XXXX-XXXX-XXXX-XXXX     # revokes that PC
dotnet run --project tools/LicenseTool -- allow list   key.pem
```

**First-time setup**: `keygen` a key pair, paste the public key into `LicensePublicKey.cs`, set `AllowlistUrl` in `AccessConfig.cs` to the gist raw URL, build the allowlist with the owner's own hardware ID, upload `allowlist.signed`, then build and distribute the app.

**Limits (unchanged and important):**
- Revocation works only on this build (1.4.0+). Copies of older builds (which had no online check) cannot be revoked.
- It does not stop someone who decompiles the .NET app and removes the check. Blocking the URL only helps them for the grace period, after which the app locks.
- Never commit the private key: `*.pem`, `allowlist.json` and `allowlist.signed` are git-ignored.

## Project structure

```
scripts/                       The G.S.9 scripts, byte-for-byte as provided (.gitattributes keeps them untouched)
docs/DESIGN_SYSTEM.md          Visual identity rules: tokens, type, states, components
src/ConnectionOptimizer/
  Models/                      OptimizationDefinition + OptimizationCatalog (which script, name, category, warnings)
  Services/                    ScriptRunner, ScriptVerifier, ScriptWindows, NetworkInfoService, Elevation (no UI code)
  Services/Licensing/          Hardware ID, license format and check
  Services/Scripts/            Encrypted script archive: pack format and in-memory store
  ViewModels/                  MainViewModel (flows), OptimizationViewModel (one tool), Connection, Activity
  Views/                       MainWindow, LockWindow, OptimizationCell, StatusBadge, ConfirmDialog
  Themes/                      Tokens.xaml, Typography.xaml, Controls.xaml (the design system in XAML)
tools/LicenseTool/             Owner's command-line tool: create keys and issue licenses
tools/HwidDetector/            GS9-HWID.exe: shows a PC's hardware ID, to send before getting a license
tools/ScriptPacker/            Build step: encrypts scripts/ into the blob embedded in the app
build/script-key.txt           Secret AES key for the script blob (git-ignored, auto-created on build)
```

Architecture: MVVM with no external packages. The layers are UI → view models (state and flows) → services → `cmd.exe` → script.

## How scripts are run

- The scripts are stored **AES-256-GCM encrypted inside the executable** (`Services/Scripts`), not as files. To run one, its exact bytes are written to a temporary file under `%TEMP%\GS9-<random>\`, run through `cmd.exe /d /c` in a hidden console, and the file and folder are deleted immediately after — in a `finally`, so also on error or timeout. Its output is **not read**. The UI stays responsive because everything is `async`.
- **Only one script runs at a time.** Buttons are disabled while something runs, and the runner refuses a second run.
- Every script ends with `PAUSE`. The app closes the script's input, so `PAUSE` returns immediately instead of waiting for a key. No script is modified to achieve this.
- Result: **exit code 0 → APPLIED · unverified; any other code → ERROR.** A batch script's exit code comes from its last command, so "APPLIED" means the script ran to the end, not that every tweak took effect. Only Registry Tweaks has a verifier, so it is the only tool that can show **ACTIVE / PARTIAL / INACTIVE**.
- **Windows opened by a script** (for example the Disk Cleanup settings from `AntiInputLag.bat`) are detected while it runs. The tile switches to **◆ WINDOW OPEN**, the window is brought to the front once, and a SHOW WINDOW button brings it back. This replaces a script that looked frozen while waiting for a click.
- The verifier runs at start-up, after every script (scripts overlap: Minecraft PvP writes some of the same values), and on **CHECK STATUS**.
- **No logs.** Nothing the scripts print is shown or saved. The ACTIVITY panel lives in memory and disappears when the app closes. The verifier's output is read only to get its result, then discarded. The only file the app writes is the license. On start-up it deletes the `Logs` folder that versions up to 1.1 created.

## Administrator rights

The app starts **without** elevation (`asInvoker`). Reading the connection and checking the status do not need it. When you run a script, the app explains why it needs administrator rights and restarts itself through UAC, then continues the action you chose. That means one UAC prompt per session, and none if you only look.

## Replacing or updating a script

Because the scripts are embedded and encrypted, changing one means **rebuilding**: replace the file in `scripts/` (keep the same name) and build. The build step (`tools/ScriptPacker`) re-encrypts them into the blob. There is no longer a `Scripts\` folder to drop a file into next to the .exe. Names, descriptions and warnings are in `Models/OptimizationCatalog.cs`.

## Protecting the scripts (what this does and does not do)

The scripts do not appear as files and are not readable by opening the archive, the install folder, or the `.exe` with a text/strings viewer. This stops casual copying.

It is **not** unbreakable, and it cannot be. The app has to decrypt the scripts to run them, so the key travels inside the executable and the decrypted bytes exist briefly as a temp file and, while running, as a visible `cmd.exe`/`reg`/`netsh`/`powershell` command line (Task Manager → Details → Command line, or Process Monitor). Someone who decompiles the .NET app can recover the key and the scripts. This raises the effort; it does not make access impossible. The AES key is in `build/script-key.txt`, git-ignored and auto-created on first build; keep a copy if you want reproducible builds.

## Known issues in the scripts (not fixed: the scripts are only changed with the owner's approval)

| Script | Issue | Effect |
|---|---|---|
| `AdvancedWindowsCleanup.bat` | `"%TEMP%*"` and `"C:\Windows\Temp*"` are missing a `\` before `*`. Combined with `del /s`, they match **any file whose name starts with "Temp"** (for example `Template…`) anywhere under `%LOCALAPPDATA%` and `C:\Windows`. The `for /d … rd /s /q` lines then remove the whole `Temp` folders. | Can delete unrelated files. The intended pattern is `"%TEMP%\*"`. |
| `AdvancedWindowsCleanup.bat` | `Recent*` and `Profiles*` have the same missing `\`. | Recent items and the Firefox cache are not cleaned. |
| `AdvancedWindowsCleanup.bat` | The last command is `Clear-RecycleBin`, which likely returns an error when the Recycle Bin is already empty. | The app may show ERROR (exit 1) even though the cleanup ran. |
| `NetworkLatencyOptimizer.bat` | `Set-NetIPInterface -InterfaceAlias '*' -InterfaceMetric 1` gives **every** interface the same metric. | Does not prioritize the active interface; with Wi-Fi + Ethernet or a VPN, Windows' choice of route becomes a tie. |
| `NetworkLatencyOptimizer.bat` | Adapter properties are set by `DisplayName` (driver- and language-dependent) with `SilentlyContinue`. | On adapters with other names the changes are silently skipped; exit code is still 0. |
| `AntiInputLag.bat` | `cleanmgr /sageset:1` is the one-time *configuration* of Disk Cleanup, but it runs on every execution. | Every run stops until you click OK in that window. The app now shows it (WINDOW OPEN), but only changing the script to `/sagerun:1` alone removes the stop. |
