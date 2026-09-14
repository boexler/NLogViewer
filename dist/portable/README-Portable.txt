NLogViewer – portable distribution
==================================

These ZIP packages are portable: unpack anywhere (including a USB drive) and run
Sentinel.NLogViewer.App.exe from this folder.

Configuration (ports, language, etc.) is stored in appsettings.json in this same
folder as the executable — not in AppData. A marker file (NLogViewer.portable or
.portable) enables this layout; do not delete it if you want portable behavior.

Self-contained build
--------------------
Includes the .NET runtime. No separate .NET installation is required. Larger
download (~100–150 MB typical).

Framework-dependent build
-------------------------
Requires the .NET 8 (or compatible) desktop runtime for Windows (win-x64)
installed on the machine. Smaller download.

Advanced: you can force portable paths by setting environment variable
NLOGVIEWER_PORTABLE=1 (mainly for diagnostics).

Project home: https://github.com/boexler/NLogViewer
