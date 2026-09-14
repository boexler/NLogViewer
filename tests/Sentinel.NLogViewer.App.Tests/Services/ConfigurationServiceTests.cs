using System;
using System.IO;
using Sentinel.NLogViewer.App.Services;
using Xunit;

namespace Sentinel.NLogViewer.App.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ConfigurationService"/> portable vs. per-user configuration paths.
    /// </summary>
    public class ConfigurationServiceTests : IDisposable
    {
        private readonly string? _previousPortableEnv;

        public ConfigurationServiceTests()
        {
            _previousPortableEnv = Environment.GetEnvironmentVariable(ConfigurationService.PortableEnvironmentVariableName);
        }

        public void Dispose()
        {
            RestorePortableEnv(_previousPortableEnv);
        }

        private static void RestorePortableEnv(string? previous)
        {
            if (previous is null)
                Environment.SetEnvironmentVariable(ConfigurationService.PortableEnvironmentVariableName, null);
            else
                Environment.SetEnvironmentVariable(ConfigurationService.PortableEnvironmentVariableName, previous);
        }

        [Fact]
        public void ResolveConfigPath_WithMarkerFile_UsesBaseDirectory()
        {
            Environment.SetEnvironmentVariable(ConfigurationService.PortableEnvironmentVariableName, null);
            var dir = Path.Combine(Path.GetTempPath(), "nlv_cfg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, ConfigurationService.PortableMarkerFileName), string.Empty);
                var path = ConfigurationService.ResolveConfigPath(dir);
                Assert.Equal(Path.Combine(dir, "appsettings.json"), path);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [Fact]
        public void ResolveConfigPath_WithEnvironmentOverride_IgnoresMarkerAndUsesBaseDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "nlv_cfg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            Environment.SetEnvironmentVariable(ConfigurationService.PortableEnvironmentVariableName, "1");
            try
            {
                var path = ConfigurationService.ResolveConfigPath(dir);
                Assert.Equal(Path.Combine(dir, "appsettings.json"), path);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [Fact]
        public void ResolveConfigPath_WithoutPortable_UseLocalAppData()
        {
            Environment.SetEnvironmentVariable(ConfigurationService.PortableEnvironmentVariableName, null);
            var dir = Path.Combine(Path.GetTempPath(), "nlv_cfg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var path = ConfigurationService.ResolveConfigPath(dir);
                Assert.Contains("Sentinel.NLogViewer.App", path);
                Assert.EndsWith("appsettings.json", path);
                Assert.Contains(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinel.NLogViewer.App"),
                    path);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [Fact]
        public void SaveAndLoad_InPortableDirectory_RoundTrips()
        {
            Environment.SetEnvironmentVariable(ConfigurationService.PortableEnvironmentVariableName, null);
            var dir = Path.Combine(Path.GetTempPath(), "nlv_cfg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, ConfigurationService.PortableMarkerDotFileName), string.Empty);
            try
            {
                var service = new ConfigurationService(dir);
                var saved = new AppConfiguration { Language = "en", MaxLogEntriesPerTab = 42 };
                service.SaveConfiguration(saved);
                var loaded = service.LoadConfiguration();
                Assert.Equal("en", loaded.Language);
                Assert.Equal(42, loaded.MaxLogEntriesPerTab);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        private static void TryDeleteDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // best effort
            }
        }
    }
}
