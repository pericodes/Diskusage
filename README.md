# DiskUsage

A tiny, fast and portable disk-usage explorer for Windows. Browse your folders and instantly see how much space each one takes, with a pie chart of the biggest ones next to it.

Think of it as a graphical `du -h --max-depth=1` that you can navigate with the mouse and for Windows.

![DiskUsage screenshot](https://github.com/pericodes/Diskusage/blob/main/docs/Screenshot.png?raw=true)

## What is it and what is it for?

DiskUsage answers one simple question: **"what is eating my disk space?"**

You open a folder (for example `Downloads`) and it lists every subfolder with its total size, including everything inside it, plus a pie chart with the biggest items. Double-click a folder to dive in, go up to the parent, and the view is recalculated immediately.

**Features**

- Lists the sub-folders of the current folder with their total size and percentage.
- Pie chart (right side) with the largest folders/files, showing absolute size and percentage.
- Sort by name or by size, ascending or descending (click the column headers).
- Fast navigation: enter a folder, go up, go back, type a path or switch drive.
- Multi-threaded scanning using the native Win32 API; results appear and re-sort progressively while they are being calculated.
- Results are cached while you navigate, so going into an already-scanned subfolder is almost instant.
- Loading indicator (spinner while calculating, green dot when finished).
- Optional display of individual files (otherwise files are grouped in a single "[ N files ]" row).
- Open the current folder in Windows Explorer, show an item in Explorer, copy its path.
- Delete files/folders (to the Recycle Bin or permanently), with confirmation.
- Multi-language UI, automatically selected from the OS language.
- No dependencies, no installer, no Electron: a single ~45 KB executable.

## How to use it

Run `DiskUsage.exe`. Optionally pass a starting folder:

```
DiskUsage.exe D:\Data
```

If no folder is given, it opens the last folder you used, or your `Downloads` folder the first time.

### Interface

| Action | How |
| --- | --- |
| Enter a folder | Double-click / `Enter` on a folder row, or double-click its slice in the pie chart |
| Go to the parent folder | **Up** button, `Backspace` or `Alt+↑` |
| Go back | **Back** button or `Alt+←` |
| Type or paste a path | Click the path box (or `Ctrl+L` / `F4`), then `Enter` |
| Change drive | Drive drop-down |
| Recalculate (clear cache) | **Refresh** button or `F5` |
| Sort | Click the *Name*, *Size* or *%* column header; click again to reverse |
| Show individual files | *Show files* check box |
| Open current folder in Explorer | **Open in Explorer** button |
| Context menu | Right-click a row: Open, Show in Explorer, Copy path, Delete, Delete permanently |
| Delete to Recycle Bin | `Del` (with confirmation) |
| Delete permanently | `Shift+Del` (with confirmation) |
| Change language | Language drop-down (top right) |

Hover over a pie slice or legend entry to highlight it; click it to select the matching row.

### Supported languages

| Code | Language |
| --- | --- |
| `en` | English (default / fallback) |
| `es` | Español |
| `fr` | Français |
| `de` | Deutsch |
| `it` | Italiano |
| `pt` | Português |

The language is picked from your Windows UI language. If it is not available, English is used. You can change it at any time from the language drop-down; the choice is remembered.

### Good to know

- Sizes are the **logical size of the files** (what Explorer shows as *Size*), not the space allocated on disk.
- Symbolic links and junctions are not followed (this avoids loops and double counting).
- Cloud-only placeholder files (e.g. OneDrive "Files On-Demand" that are not downloaded) are not counted.
- Folders you have no permission to read count as 0.
- Cached sizes expire after 2 minutes; press `F5` to force a full recalculation.

## Installation / uninstallation

DiskUsage is **portable**: there is nothing to install.

**Install:** download (or [build](#building-from-source)) `DiskUsage.exe` and put it anywhere you like — a USB stick, your desktop, a tools folder. Double-click it. That's it.

**Uninstall:** delete `DiskUsage.exe`. If you ran it, also delete the `DiskUsage.ini` file created next to it (it stores your language, last folder, sort order and window size). It writes nothing to the registry and nothing outside its own folder.

### ⚠️ Security warning when running the `.exe` (this is normal)

The first time you run `DiskUsage.exe` you will probably see a Windows security warning such as **"Windows protected your PC"** (Microsoft Defender SmartScreen), or a similar message from your antivirus or browser when downloading it.

**This is expected.** The executable is **not digitally signed**: code-signing certificates cost money every year, and SmartScreen also flags any small, new or little-downloaded program until it builds up "reputation". It does **not** mean the program is malicious.

To run it anyway, click **More info → Run anyway**. If the file was downloaded and Windows blocks it, you can also right-click it → **Properties** → check **Unblock** → **OK**.

If you don't want to trust a pre-built binary, you don't have to: the whole program is a single, readable source file, so you can **review the code and [build it yourself](#building-from-source)** with the same command used in `build.bat`. Keep in mind that the executable you build will still be unsigned, so Windows or your antivirus may keep showing the warning — that is expected and not a sign of a problem.

**Requirements:** Windows with .NET Framework 4.x. This is already included in Windows 10 and 11, so no extra download is needed. The program runs from a read-only location too; it just won't be able to save its settings.

## Building from source

The whole program is a single C# file (`DiskUsage.cs`) written in **C# 5**, so it can be compiled with the compiler that already ships with Windows — no Visual Studio or SDK needed.

### Option 1: `build.bat` (recommended)

Double-click `build.bat`, or run it from a terminal. It produces `DiskUsage.exe` in the same folder.

### Option 2: manual command

From a Command Prompt in the repository folder:

```bat
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /optimize+ /target:winexe /codepage:65001 /out:DiskUsage.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Microsoft.VisualBasic.dll DiskUsage.cs
```

(Use `Framework` instead of `Framework64` on 32-bit Windows.)

### Option 3: cross-compile with Mono (Linux/macOS)

```bash
mcs -nologo -optimize+ -target:winexe -out:DiskUsage.exe \
    -r:System.Windows.Forms.dll -r:System.Drawing.dll \
    -r:Microsoft.VisualBasic.dll DiskUsage.cs
```

The resulting executable only runs on Windows (it uses Win32 APIs).

### Using Visual Studio / `dotnet`

You can also create a WinForms project targeting .NET Framework 4.x, add `DiskUsage.cs` and reference `Microsoft.VisualBasic`. The source file is saved as UTF-8 with BOM; keep it that way (or pass `/codepage:65001`) so the translated strings compile correctly.

## Contributing

Contributions are very welcome — bug reports, ideas, translations and pull requests.

1. **Open an issue** first for bigger changes so we can discuss the approach.
2. **Fork** the repository and create a branch (`git checkout -b feature/my-change`).
3. Make your change, build it with `build.bat` and test it on Windows.
4. **Open a pull request** describing what you changed and why.

**Guidelines**

- Keep it **small, fast and dependency-free**: no NuGet packages, no extra runtime, no installer.
- Keep the code compatible with **C# 5** (the compiler built into Windows): no string interpolation (`$""`), no `?.`, no `nameof`, no expression-bodied members, no `out var`, etc.
- Keep it single-file unless there's a strong reason to split it.
- Don't block the UI thread: heavy work goes to background tasks, UI updates go through `BeginInvoke`.
- Keep the file encoded as UTF-8 with BOM.

**Adding a new language**

All UI strings live in the `L` class in `DiskUsage.cs`:

1. Add the language code to `L.Codes` and its native name to `L.Names` (same position).
2. In the `T` dictionary, add one translation at the same position to **every** entry.
3. Build and check that it is selectable from the language drop-down and that long texts fit.

**Ideas for contributions**

- Size on disk (allocated clusters) as an alternative to logical size.
- Export of results (CSV).
- More languages.
- Keyboard shortcuts / accessibility improvements.

## License

See the `LICENSE` file in this repository.