#if DEBUG
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Sentinel.LogViewer.Helpers;

/// <summary>
/// Opens the test application from the debug menu. It is found in the folder <c>samples</c> of the repository, above the folder of the
/// build; the project reference of the debug build builds it first.
/// </summary>
internal static class TestApplicationLauncher
{
    public static void Open()
    {
        var path = Find();
        if (path == null)
        {
            MessageBox.Show("The test application is not built yet: samples/Sentinel.LogViewer.TestApp", "Test application");
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static string? Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var build = Path.Combine(directory.FullName, "samples", "Sentinel.LogViewer.TestApp", "bin");
            if (Directory.Exists(build))
            {
                return Directory.EnumerateFiles(build, "Sentinel.LogViewer.TestApp.exe", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
        }

        return null;
    }
}
#endif
