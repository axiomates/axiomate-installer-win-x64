# Axiomate Windows x64 Installer

WPF + .NET 8 self-contained single-file installer for [Axiomate](https://axiomate.net) on Windows.
Wraps the Axiomate Windows agent dist together with bundled Git / Python installers and produces
**one EXE** the end user can double-click.

| | |
|---|---|
| Output | `artifacts/installer/axiomate-installer-<version>.exe` |
| Size | ~278 MB (axiomate dist 165 MB + Git 62 MB + Python 26 MB + WPF runtime + uninstaller) |
| Target OS | Windows 11 (build 22000+), x64 only |
| Privilege | self-elevates: starts as invoker, captures original user SID/profile, then relaunches with UAC |
| UI | 7-step WPF wizard, Chinese strings, light theme matching the Axiomate doc style |
| Repo state | local git repo on `main`; no remote |

---

## Table of contents

1. [Quick start](#quick-start)
2. [What the installer does](#what-the-installer-does)
3. [Wizard flow](#wizard-flow)
4. [Silent installer parameters](#silent-installer-parameters)
5. [Project layout](#project-layout)
6. [Versioning](#versioning)
7. [Build pipeline](#build-pipeline)
8. [Updating bundled Axiomate dist](#updating-bundled-axiomate-dist)
9. [Model templates](#model-templates)
10. [Path guard, PATH registration, pip mirror, workspace, uninstaller](#runtime-behaviors)
11. [Logging & error handling](#logging--error-handling)
12. [Smoke test plan](#smoke-test-plan)
13. [Known limitations](#known-limitations)
14. [Layout reference](#layout-reference)

---

## Quick start

```powershell
# 1. (one-time) ensure axiomate dist is at C:\public\workspace\axiomate\agent\dist
#    -- the build script copies it into Resources/dist/ and embeds it.

# 2. (one-time) ensure these two payloads sit under src/AxiomateInstaller/Resources/
#       Git-2.54.0-64-bit.exe
#       python-3.12.10-amd64.exe

# 3. build everything
pwsh build.ps1
#    -SkipDistSync   skip step 1 if the dist hasn't changed
#    -KeepArtifactsRaw  keep the unversioned axiomate-installer.exe alongside the renamed copy

# 4. final deliverable
ls artifacts/installer/axiomate-installer-1.0.0.exe   # single file, ~278 MB
```

The build runs `dotnet publish` for both the uninstaller (intermediate) and the main installer.
After the main publish the uninstaller is embedded into the main EXE and intermediates are moved
under `artifacts/_intermediate/`. `artifacts/installer/` ends up with **exactly one file**.

---

## What the installer does

End-user view, double-clicking the EXE on a clean Windows 11 box:

1. Starts as the invoking user, captures SID/profile, then shows UAC and relaunches elevated.
2. Welcome & environment-check pages run.
3. If Git or Python are missing / too old, the installer offers to install bundled ones silently.
4. Pick install dir (default `%USERPROFILE%\axiomate`). Path is validated against a blacklist.
5. Optional: workspace + desktop / start-menu shortcuts.
6. Optional: quick model config — pick DeepSeek model, paste API key.
7. Optional: enable bypass permission mode by writing `permissions.defaultMode = "bypassPermissions"` to `~/.axiomate/settings.json`.
8. Progress page runs the steps in order, streaming a log to the screen.
9. Finish page; one-click launch when workspace was chosen.

Outputs on the user's machine after a successful run:

- `<install-dir>\axiomate.exe` and 10 native dependencies, plus
  `installation-manifest.json` (used by the uninstaller).
- `<install-dir>\Uninstaller.exe`.
- HKLM `Path` updated, `WM_SETTINGCHANGE` broadcast → newly opened terminals see `axiomate`.
- Apps & features entry under `HKLM\...\Uninstall\Axiomate`.
- Optional: `~/.axiomate.json` overwritten with the chosen DeepSeek template
  (and `~/.axiomate/` wiped).
- Optional: workspace dir plus `Axiomate.lnk` on `C:\Users\Public\Desktop` and
  `C:\ProgramData\Microsoft\Windows\Start Menu\Programs`; shortcuts launch through Windows Terminal, not cmd.
- Optional: `<PythonTargetDir>\pip.ini` containing the chosen mirror.

---

## Wizard flow

Page index in `MainWindow._pages`:

| # | Page | Code-behind | Notes |
|---|---|---|---|
| 0 | Welcome | `Pages/WelcomePage.xaml(.cs)` | Shows installer + axiomate version, brief feature list. Back disabled. |
| 1 | Env check | `Pages/EnvCheckPage.xaml(.cs)` | OS / arch / Git / Python detection; pip-mirror block appears only if Python install is checked. Blocks Next on Win10 / arm64. |
| 2 | Install path | `Pages/InstallPathPage.xaml(.cs)` | `DirGuard` validation; folder picker; force-empty confirmation when target is non-empty. |
| 3 | Options | `Pages/OptionsPage.xaml(.cs)` | Optional quick model config, bypass permission mode, workspace+shortcuts. PATH registration is mandatory and not exposed. |
| 4 | Model config | `Pages/ModelConfigPage.xaml(.cs)` | Site = DeepSeek (only option in v1), model = `deepseek-v4-pro` / `deepseek-v4-flash`, masked API-key input with "show" toggle. **Skipped automatically** when option not picked (`OptionsPage.NextIndex` jumps +2). |
| 5 | Progress | `Pages/ProgressPage.xaml(.cs)` | Drives `InstallEngine`. Streams `Logger` lines into a ScrollViewer; updates `ProgressBar`. On failure: turns red and pops a dialog with the log path + "open in Notepad" choice. |
| 6 | Finish | `Pages/FinishPage.xaml(.cs)` | Summary + optional "launch Axiomate" button (only when workspace launcher exists). |

`Pages/WizardPage.cs` is a thin abstract `Page` subclass with virtual hooks
(`OnEnter` / `OnLeave` / `Validate` / `NextIndex` / `PrevIndex` / `AllowBack` / `AllowNext` / `AllowCancel`).
`MainWindow` holds the canonical `InstallOptions`, `Logger`, `VersionInfo`, and the page array.

---

## Silent installer parameters

### Git for Windows 2.54.0  (`Resources/Git-2.54.0-64-bit.exe`)

```
/VERYSILENT /NORESTART /SP- /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NOICONS
/COMPONENTS="icons,ext\reg\shellhere,assoc,assoc_sh,gitlfs,windowsterminal,scalar"
/o:PathOption=CmdTools          # PATH includes git AND Unix tools (so AI's linux command output works)
/o:SSHOption=OpenSSH
/o:CURLOption=WinSSL            # WinSSL / SChannel — friendlier under enterprise proxies
/o:CRLFOption=CRLFCommitAsIs    # don't munge line endings
/o:BashTerminalOption=ConHost
/o:DefaultBranchOption=main
/o:CredentialManager=Enabled
/o:EditorOption=Nano            # nano is bundled with Git for Windows; no external download
/o:EnableSymlinks=Disabled
/o:EnableFSMonitor=Disabled
```

`gitlfs` and `scalar` come bundled inside the installer; flagging them in `COMPONENTS` does **not**
trigger any network download. Exit code `0` → success, `3010` → "restart required" (also treated as
success). Anything else surfaces as `InstallStepException`.

### Python 3.12.10  (`Resources/python-3.12.10-amd64.exe`)

```
/quiet
InstallAllUsers=1                   # per-machine; matches admin-required installer
TargetDir="<InstallOptions.PythonInstallDir>"   # default C:\Program Files\Python312
PrependPath=1
Include_launcher=1
AssociateFiles=1
Include_pip=1
Include_tcltk=1
Include_test=0                      # save ~30 MB
Include_doc=0
Include_dev=0
Include_debug=0
Shortcuts=1
SimpleInstall=1
SimpleInstallDescription="Installing Python 3.12.10"
```

Exit codes handled in `InstallerRunner.RunPythonAsync`: `0` success; `1602` user cancel (friendly
error); `1603` fatal (friendly error pointing to log).

---

## Project layout

```
axiomate-installer-win-x64/
├── AxiomateInstaller.sln
├── version.json                 ← single source of truth for installer version
├── build.ps1                    ← sync dist, build uninstaller + installer, version-stamp + rename
├── axiomate-logo.png            ← 256x256 source logo
├── .gitignore
└── src/
    ├── AxiomateInstaller/                   ← main wizard EXE
    │   ├── AxiomateInstaller.csproj
    │   ├── app.manifest                     ← asInvoker + Win10/11 GUID + DPI/long-path
    │   ├── App.xaml(.cs)                    ← global exception handlers, theme styles
    │   ├── MainWindow.xaml(.cs)             ← chrome (header, footer, frame), nav buttons
    │   ├── Pages/
    │   │   ├── WizardPage.cs                ← abstract base
    │   │   ├── WelcomePage.xaml(.cs)
    │   │   ├── EnvCheckPage.xaml(.cs)
    │   │   ├── InstallPathPage.xaml(.cs)
    │   │   ├── OptionsPage.xaml(.cs)
    │   │   ├── ModelConfigPage.xaml(.cs)
    │   │   ├── ProgressPage.xaml(.cs)
    │   │   └── FinishPage.xaml(.cs)
    │   ├── Services/
    │   │   ├── Logger.cs                    ← per-run %TEMP%\axiomate-installer\install-<ver>-<ts>.log
    │   │   ├── VersionInfo.cs               ← reads InformationalVersion + version.json fallback
    │   │   ├── InstallOptions.cs            ← all wizard state + enums (PipMirror, ModelChoice)
    │   │   ├── EnvironmentChecker.cs        ← OS / arch / git --version / py-3.12 / python --version
    │   │   ├── DirGuard.cs                  ← path blacklist; non-empty detection
    │   │   ├── PayloadExtractor.cs          ← unpack embedded resources to %TEMP%\axiomate-installer-<guid>\
    │   │   ├── InstallerRunner.cs           ← Git / Python silent runs + exit-code policy
    │   │   ├── AxiomateDeployer.cs          ← force-clean target, copy dist, write installation-manifest.json
    │   │   ├── PathRegistrar.cs             ← HKLM Path + WM_SETTINGCHANGE
    │   │   ├── PipMirrorWriter.cs           ← <PythonTargetDir>\pip.ini
    │   │   ├── WorkspaceCreator.cs          ← creates the resolved workspace dir via C#
    │   │   ├── AxiomateLauncher.cs          ← Windows Terminal launch helper, no cmd script
    │   │   ├── ShortcutManager.cs           ← IShellLinkW COM .lnk creation
    │   │   ├── ConfigWriter.cs              ← deletes ~/.axiomate/, writes ~/.axiomate.json from template
    │   │   ├── UninstallRegistrar.cs        ← stages Uninstaller.exe + HKLM\...\Uninstall\Axiomate
    │   │   └── InstallEngine.cs             ← orchestrates the steps and reports IProgress<InstallProgress>
    │   ├── Templates/
    │   │   ├── deepseek-v4-pro.json         ← embedded; {{API_KEY}} placeholder
    │   │   └── deepseek-v4-flash.json       ← embedded; {{API_KEY}} placeholder
    │   └── Resources/
    │       ├── icon.ico                     ← multi-size (16/32/48/64/128/256) generated from logo
    │       ├── axiomate-logo.png            ← shown in header banner
    │       ├── Git-2.54.0-64-bit.exe        ← embedded as <EmbeddedResource>
    │       ├── python-3.12.10-amd64.exe     ← embedded as <EmbeddedResource>
    │       ├── Uninstaller.exe              ← built by build.ps1 then embedded
    │       └── dist/                        ← synced by build.ps1, embedded as <EmbeddedResource Include="dist\**\*"/>
    └── AxiomateUninstaller/                 ← intermediate; embedded into the installer
        ├── AxiomateUninstaller.csproj
        ├── app.manifest                     ← admin
        └── App.cs                           ← single-file: read registry InstallLocation, prompt,
                                                clean PATH, delete shortcuts, drop registry, then
                                                spawn a copied helper EXE that deletes via C# Directory.Delete
```

`bin/`, `obj/`, `artifacts/`, `Resources/dist/` and `Resources/Uninstaller.exe` are gitignored.
The two installer EXEs (Git, Python) are tracked in git per project decision (~88 MB total).

---

## Versioning

Single source: `version.json` at the repo root.

```json
{
  "installerVersion": "1.0.0",
  "axiomateVersion": "auto",
  "axiomateBuildNumber": 24,
  "bundledGitVersion": "2.54.0",
  "bundledPythonVersion": "3.12.10"
}
```

`axiomateVersion: "auto"` tells `build.ps1` to read the first three numeric segments from
`dist\axiomate.exe` `FileVersion` (for example `0.6.12`) and append `axiomateBuildNumber` as the
fourth segment (for example `0.6.12.23`). Commit hashes or other suffixes from upstream file metadata
are intentionally stripped. Keep `axiomateBuildNumber` equal to the bundled package build number.
You can also pin a literal value like `"0.6.12.23"`.

`build.ps1` injects:

| MSBuild prop | Value |
|---|---|
| `Version` | `<installerVersion>` |
| `FileVersion` | `<installerVersion>.0` |
| `InformationalVersion` | `<installerVersion>+axiomate.<axiomateVersion>` |

These appear in the EXE's Win32 version resource:

```
FileVersion     : 1.0.0.0
ProductVersion  : 1.0.0+axiomate.0.6.12.23
ProductName     : Axiomate Installer
CompanyName     : Axiomate
```

UI / log usage:

- Window title: `Axiomate Installer 1.0.0`.
- Header badge: `v1.0.0  ·  Axiomate 0.6.12.23`.
- About dialog: 4 versions (installer / axiomate / bundled Git / bundled Python).
- Apps & features `DisplayVersion` = `axiomateVersion`; `Comments` = `Installer <installerVersion>`.
- Log file: `%TEMP%\axiomate-installer\install-<installerVersion>-<yyyyMMdd-HHmmss>.log`.
- Final EXE filename: `axiomate-installer-<installerVersion>.exe`.

---

## Build pipeline

`build.ps1` (PowerShell 7+):

| Step | What |
|---|---|
| 1 | Read `version.json`. |
| 2 | Sync `C:\public\workspace\axiomate\agent\dist\*` → `src/AxiomateInstaller/Resources/dist/` (skip with `-SkipDistSync`). |
| 3 | If `axiomateVersion == "auto"`, resolve `major.minor.patch.<axiomateBuildNumber>` from `dist\axiomate.exe`, stripping hash suffixes. |
| 4 | Verify `Git-2.54.0-64-bit.exe` and `python-3.12.10-amd64.exe` exist under `Resources/`. |
| 5 | `dotnet publish` AxiomateUninstaller → `artifacts/_intermediate/uninstaller/`, copy resulting EXE to `Resources/Uninstaller.exe`. |
| 6 | `dotnet publish` AxiomateInstaller → `artifacts/installer/`. |
| 7 | Rename `axiomate-installer.exe` → `axiomate-installer-<version>.exe`. Strip `.pdb` and the copied `version.json` so the folder ends up with **one file only**. |

Both projects are configured for single-file self-contained `win-x64`:

```xml
<TargetFramework>net8.0-windows</TargetFramework>
<UseWPF>true</UseWPF>                                   <!-- main installer only -->
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeAllContentInSingleFile>true</IncludeAllContentInSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

Embedded resources in `AxiomateInstaller.csproj`:

```xml
<EmbeddedResource Include="Resources\Git-2.54.0-64-bit.exe"/>
<EmbeddedResource Include="Resources\python-3.12.10-amd64.exe"/>
<EmbeddedResource Include="Resources\Uninstaller.exe" Condition="Exists('Resources\Uninstaller.exe')"/>
<EmbeddedResource Include="Resources\dist\**\*" Condition="Exists('Resources\dist')"/>
<EmbeddedResource Include="Templates\*.json"/>
<Resource Include="Resources\axiomate-logo.png"/>
```

---

## Updating bundled Axiomate dist

The installer embeds a copy of the Axiomate Windows x64 dist. When upstream Axiomate produces a new
Windows build, the normal update flow is:

```powershell
# 1. Build / obtain the new upstream dist here:
#    C:\public\workspace\axiomate\agent\dist

# 2. Rebuild the installer and let build.ps1 sync that dist into embedded resources.
pwsh build.ps1

# 3. Smoke-test the generated EXE.
.\artifacts\installer\axiomate-installer-1.0.0.exe
```

What changes automatically:

- `build.ps1` removes the old `src/AxiomateInstaller/Resources/dist/` directory and copies the new
  `C:\public\workspace\axiomate\agent\dist\*` into it.
- The copied dist is embedded by `<EmbeddedResource Include="Resources\dist\**\*" />` during publish.
- If `version.json` keeps `"axiomateVersion": "auto"`, the displayed bundled Axiomate version is
  `major.minor.patch.<axiomateBuildNumber>` from the new `dist\axiomate.exe`; commit/hash suffixes are stripped.
  Update `axiomateBuildNumber` when producing a new package build.
- The installer output remains `artifacts/installer/axiomate-installer-<installerVersion>.exe`.

What to edit manually:

- **Only Axiomate dist changed:** usually edit nothing. Run `pwsh build.ps1`, test, then commit the
  updated generated installer artifact if you are tracking release artifacts elsewhere.
- **Installer UI / install logic changed:** bump `installerVersion` in `version.json`, rebuild, test,
  and commit the source change plus `version.json`.
- **Need to pin a marketing/product version:** replace `"axiomateVersion": "auto"` with a literal value
  such as `"0.6.12.23"`; otherwise leave it as `auto`.
- **Git / Python payload changed:** replace the EXE under `src/AxiomateInstaller/Resources/`, update
  `bundledGitVersion` / `bundledPythonVersion` in `version.json`, and update the silent parameters only
  if the new installer requires different flags.

Use `-SkipDistSync` only when rebuilding after installer-code changes and the bundled Axiomate dist has
not changed:

```powershell
pwsh build.ps1 -SkipDistSync
```

Do **not** use `-SkipDistSync` when the upstream Axiomate dist was updated, or the final installer will
still embed the previous dist.

---

## Model templates

The installer ships two templates that map 1:1 with the upstream user's
`~/.axiomate.json` shape (real values, not toy defaults):

`Templates/deepseek-v4-pro.json`

```json
{
  "models": {
    "deepseek-v4-pro": {
      "model": "deepseek-v4-pro",
      "name": "DeepSeek V4 Pro",
      "protocol": "openai-chat",
      "contextWindow": 1000000,
      "maxOutputTokens": 384000,
      "supportsImages": false,
      "thinking": { "enabled": true, "effort": "high" },
      "baseUrl": "https://api.deepseek.com",
      "apiKey": "{{API_KEY}}",
      "modelTemplate": "openai-chat-deepseek-v4p"
    }
  },
  "model": { "current": "deepseek-v4-pro" }
}
```

`Templates/deepseek-v4-flash.json` is identical except `model`/`name`/`current` use the
flash id and `thinking.enabled` is `false`.

The wizard never asks for `baseUrl` or `protocol` — those are baked. User picks site (DeepSeek
official), model (pro / flash), and pastes the key. `ConfigWriter` JSON-escapes the key safely
(`\"`, `\\`, control chars) before substituting `{{API_KEY}}`, then writes UTF-8 (no BOM).

**Cleanup behavior, opt-in only**: when the user checks "quick model config", `ConfigWriter`
recursively deletes `~/.axiomate/` first, then writes the template to `~/.axiomate.json`. When the
user does **not** check that box, neither file is touched.

There are no routes / fallbacks / auxiliary blocks — Axiomate fills those in on first launch.

### Bypass permission option (`Services/SettingsWriter.cs`)

The install options page also has an opt-in checkbox for bypass permission mode. It is off by default.
When checked, the installer runs this step **after** quick model config, so if quick model config deletes
`~/.axiomate/`, the settings file is recreated afterwards.

`SettingsWriter` reads the target user's `~/.axiomate/settings.json` as JSON, preserves existing fields,
and updates/adds only:

```json
{
  "permissions": {
    "defaultMode": "bypassPermissions"
  }
}
```

If `settings.json` does not exist, it is created. If parsing fails, the invalid file is backed up as
`settings.json.invalid-<timestamp>.bak`, then the minimal permissions JSON above is written.

---

## Runtime behaviors

### User profile resolution and path guard (`Services/UserProfileResolver.cs`, `Services/DirGuard.cs`)

The installer starts unelevated, captures the original user's SID/profile, then relaunches itself elevated
with those values as command-line arguments. User-scoped paths must never be inferred from the elevated
process profile. `UserProfileResolver` uses the captured target user first, then falls back to active-session
resolution, and is used by model config, per-user Git/Python probing, workspace validation, and install path guards.
Path comparisons use final-path canonicalization where possible to reduce junction/symlink/subst alias risk.

`DirGuard.Evaluate(string)` returns `(Ok, Reason)`. Rejects:

- empty / non-rooted / paths with invalid chars;
- a drive root (`C:\`);
- exact matches of: `UserProfile`, `Desktop`, `Documents`, `Downloads`, `AppData\Local`,
  `AppData\Roaming`, system temp;
- the `~/.axiomate` config dir **and any of its subdirectories**;
- the protected system roots **and any of their subdirectories**: `Windows`, `System32`,
  `SysWOW64`, `Program Files`, `Program Files (x86)`, `ProgramData`.

Because the protected roots are child-blocked, `C:\Program Files\Axiomate` is **not** a valid target —
the default install dir is `%USERPROFILE%\axiomate`. `DirectoryHasContent` checks if the chosen dir
is non-empty so the wizard can confirm the wipe.

### PATH registration (`Services/PathRegistrar.cs`)

Opens `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment`,
reads `Path` with `RegistryValueOptions.DoNotExpandEnvironmentNames` (so `%SystemRoot%` stays
literal), splits on `;`, dedupes, appends the install dir, writes back as `RegistryValueKind.ExpandString`.
Then broadcasts:

```c
SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, "Environment",
                   SMTO_ABORTIFHUNG, 5000, &result);
```

Newly opened consoles pick the change up; already-running shells need to be reopened.
The uninstaller does the inverse via `RemoveFromPath`.

### pip mirror (`Services/PipMirrorWriter.cs`)

Visible **only when** "install bundled Python" is checked. The block has:

- `[x] 配置 pip 镜像源 (推荐)` (default checked)
- radio: `(•) 清华大学 TUNA` `( ) 阿里云`

Default-on + Tsinghua = best CN-network experience; user can flip to Aliyun or uncheck the box
entirely (then PyPI official is used). Output goes to `<PythonTargetDir>\pip.ini`:

```ini
[global]
index-url = https://pypi.tuna.tsinghua.edu.cn/simple
trusted-host = pypi.tuna.tsinghua.edu.cn
```

This is pip's **site-level** config on Windows: applies to that interpreter for every user on the
machine. User-level `%APPDATA%\pip\pip.ini` still wins, so individuals can override.

When the user already has a sufficiently new Python, the mirror block never appears — we never
touch their existing pip configuration.

### Workspace + shortcuts (`Services/WorkspaceCreator.cs`, `Services/ShortcutManager.cs`)

When the workspace option is checked, the wizard and install engine reject any workspace path that is
the same as the install directory or where either directory contains the other. This is mandatory because
the install directory is wiped during deployment while workspace is user data.

1. Default workspace path is `%USERPROFILE%\axiomate-workspace`; it is resolved with the captured
   target user profile, not the elevated installer account.
2. Create the resolved workspace directory directly from C#.
3. Create two `.lnk` files (per-machine paths since this is admin-installed):

   - `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Axiomate.lnk`
   - `C:\Users\Public\Desktop\Axiomate.lnk`

   Both target Windows Terminal (`wt.exe`) with arguments equivalent to:
   `wt -d "<workspace>" "<install-dir>\axiomate.exe"`. The shortcut working directory is the workspace,
   so Axiomate starts there without receiving the workspace path as a prompt argument. The icon is pulled
   from `<install-dir>\axiomate.exe,0`. No `.cmd` launcher is generated.

`.lnk` creation goes through the `IShellLinkW` + `IPersistFile` COM interfaces directly, no
`WshShell` dependency.

### Uninstaller (`AxiomateUninstaller/App.cs`)

Companion EXE (~70 MB self-contained). Embedded into the main installer as
`Resources/Uninstaller.exe`, then dropped at `<install-dir>\Uninstaller.exe` during the
"register uninstaller" step.

Apps & features registry values written by `UninstallRegistrar`:

| Value | Content |
|---|---|
| DisplayName | `Axiomate` |
| DisplayVersion | `<axiomateVersion>` |
| Publisher | `Axiomate` |
| URLInfoAbout | `https://axiomate.net` |
| Comments | `Installer <installerVersion>` |
| InstallLocation | `<install-dir>` |
| UninstallString | `"<install-dir>\Uninstaller.exe"` |
| QuietUninstallString | `"<install-dir>\Uninstaller.exe" /quiet` |
| DisplayIcon | `<install-dir>\axiomate.exe,0` |
| EstimatedSize | dist total bytes / 1024 (DWORD KB) |
| NoModify, NoRepair | 1 |

Uninstall flow:

1. `requireAdministrator` manifest → UAC.
2. Resolve install dir from registry `InstallLocation` (fallback: own EXE folder).
3. Confirmation dialog (skipped with `/quiet`): "your `~/.axiomate.json` and `~/.axiomate/` will be
   preserved".
4. Remove install dir from HKLM PATH; broadcast `WM_SETTINGCHANGE`.
5. Delete shortcuts at the per-machine desktop and start menu paths.
6. `DeleteSubKeyTree` on the Uninstall registry entry.
7. Self-delete: copy the uninstaller EXE to `%TEMP%\axiomate-installer\...exe`, start it with
   `ProcessStartInfo.ArgumentList`, wait for the parent process to exit, validate the install dir again,
   then delete via C# `Directory.Delete`. No `cmd.exe`, shell metacharacters, or command-line path quoting.

User data preserved on purpose: `~/.axiomate.json`, `~/.axiomate/`, the workspace dir.

---

## Logging & error handling

`Services/Logger.cs`:

- Each install run gets one log: `%TEMP%\axiomate-installer\install-<installerVersion>-<ts>.log`
  (UTF-8, no BOM, autoflush).
- `Info` / `Warn` / `Error(msg, ex)` helpers, all stamped `HH:mm:ss.fff`.
- `OnLine` event drives the live ScrollViewer on `ProgressPage`.
- `Snapshot()` gives a copy for diagnostics.

`App.xaml.cs` wires three top-level handlers:

- `Application.DispatcherUnhandledException` (UI thread)
- `AppDomain.UnhandledException`
- `TaskScheduler.UnobservedTaskException`

All of them route to a friendly `MessageBox`; nothing throws to a black .NET error window.

`InstallEngine` runs every step in a try/finally that always cleans up the temp extraction dir.
Any `InstallStepException` (or any other exception) trips `ProgressPage.ShowFailure(...)`:
title turns red, the bar turns red, and the user sees a dialog with the log path and an
"open in Notepad" button.

Already-completed steps (Git installed, Python installed) are **not rolled back** — they're useful
on their own and Windows users expect this. The error message tells you that.

---

## Smoke test plan

Run on a clean Windows 11 VM and on a working dev box:

1. **Cold machine, no Git, no Python**: full path through wizard. Verify after install:
   - new shell → `git --version` shows 2.54, `python --version` shows 3.12.10, `axiomate --help` runs.
   - `~/.axiomate.json` is the chosen template, `apiKey` matches the input.
   - Desktop shortcut exists, double-click opens TUI in workspace dir.
   - Apps & features lists Axiomate.
2. **Already has new Git + Python**: env-check page auto-skips both install boxes. Mirror block
   is hidden; existing pip config is untouched.
3. **Only Python missing, leave defaults**: `<TargetDir>\pip.ini` exists with the Tsinghua URL;
   `pip install requests -v` proves traffic goes there.
4. **Only Python missing, uncheck mirror box**: no `pip.ini` is created.
5. **Win 10 box**: env-check page blocks Next.
6. **arm64 / x86 box**: env-check page blocks Next.
7. **Path guard**: try `C:\Windows`, `C:\`, `%USERPROFILE%`. All rejected with a friendly reason.
8. **Re-install**: target dir non-empty → confirmation dialog → wipes & overwrites cleanly.
9. **Skip quick model config**: `~/.axiomate.json` and `~/.axiomate/` remain unchanged.
10. **Skip workspace**: no shortcuts, no workspace dir; `axiomate` still works in any new shell.
11. **Forced failure**: temporarily corrupt the bundled Git EXE → progress page goes red, dialog
    points at the log, install dir / PATH never get half-written.
12. **Uninstall**: from Apps & features. Folder gone, PATH clean, shortcuts gone, registry entry
    gone; `~/.axiomate.json` and `~/.axiomate/` preserved.

---

## Known limitations

- **Unsigned**. Windows SmartScreen will warn first-run users until the EXE is code-signed. v2.
- **One model site** (DeepSeek). The template / UI infrastructure is generic; adding OpenAI /
  Anthropic / SiliconFlow is just more `Templates/*.json` plus a combo-box entry.
- **No auto-update** path — installer is a fixed offline bundle.
- **Per-machine only**. No HKCU / `%LocalAppData%\Programs` mode.
- **Win11 + x64 only**. The OS GUID in `app.manifest` covers Win10/11 but `EnvironmentChecker`
  enforces build 22000+. Windows 11 still reports kernel version `10.0`, so the UI labels builds
  `>= 22000` as Windows 11 instead of relying on the major/minor version text. arm64 and x86 are
  blocked deliberately.
- **WPF runtime size**. The single-file EXE is ~278 MB. Most of that is the bundled axiomate dist
  (165 MB) + Git (62 MB) + Python (26 MB); WPF + .NET runtime account for the rest.
- **Uninstaller is .NET 8 self-contained** (~70 MB) — same single-file approach as the installer
  so it has zero machine prerequisites at uninstall time.
- The bundled Git/Python installers themselves still launch their own elevation flows; we run them
  silently from an already-elevated process, so users typically see no extra UAC.

---

## Layout reference

After a successful install, the user's machine looks like:

```
C:\Users\<user>\axiomate\               ← <install-dir> (default %USERPROFILE%\axiomate)
  axiomate.exe                       ← entry point
  agent-browser.exe
  rg.exe
  rtk.exe
  *.node, *.dll                      ← ~10 native deps
  Uninstaller.exe
  installation-manifest.json

C:\Program Files\Python312\          ← only when bundled Python ran
  pip.ini                            ← only when mirror checkbox stayed checked
  python.exe, python312.dll, ...

C:\Users\Public\Desktop\Axiomate.lnk
C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Axiomate.lnk

%USERPROFILE%\axiomate-workspace\     ← when workspace option chosen; no launcher script
%USERPROFILE%\.axiomate.json         ← when quick model config chosen
%USERPROFILE%\.axiomate\             ← created by axiomate on first run

HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment\Path
  ... ;C:\Users\<user>\axiomate

HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Axiomate
  DisplayName = Axiomate
  DisplayVersion = 0.6.12.23
  ...

%TEMP%\axiomate-installer\
  install-1.0.0-20260617-194812.log
```
