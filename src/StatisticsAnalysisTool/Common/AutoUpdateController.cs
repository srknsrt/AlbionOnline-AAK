using Serilog;
using StatisticsAnalysisTool.Common.UserSettings;
using StatisticsAnalysisTool.Properties;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using StatisticsAnalysisTool.Diagnostics;
using Application = System.Windows.Application;

namespace StatisticsAnalysisTool.Common;

public static class AutoUpdateController
{
    private static readonly HttpClient HttpClient = new();

    public static async Task AutoUpdateAsync(bool reportErrors = false)
    {
        var updateDirPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AlbionAAK", "Updates");

        RemoveUpdateFiles(updateDirPath);

        try
        {
            var userDefinedUrl = SettingsController.CurrentSettings.UpdateXmlUrl;
            var stableUrl = !string.IsNullOrWhiteSpace(userDefinedUrl)
                ? userDefinedUrl
                : Settings.Default.AutoUpdateConfigUrl;
            var preReleaseUrl = Settings.Default.AutoUpdatePreReleaseConfigUrl;

            var checkUrl = SettingsController.CurrentSettings.IsSuggestPreReleaseUpdatesActive
                ? preReleaseUrl
                : stableUrl;

            var isUrlAccessibleResult = await HttpClientUtils.IsUrlAccessible(checkUrl);
            if (isUrlAccessibleResult is not { IsAccessible: true })
                return;

            // XML'den güncelleme bilgisini oku
            var xmlContent = await HttpClient.GetStringAsync(checkUrl);
            var doc = XDocument.Parse(xmlContent);
            var item = doc.Root;
            if (item == null) return;

            var remoteVersionStr = item.Element("version")?.Value;
            var downloadUrl = item.Element("url")?.Value;
            var isMandatory = item.Element("mandatory")?.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            if (string.IsNullOrEmpty(remoteVersionStr) || string.IsNullOrEmpty(downloadUrl))
                return;

            var remoteVersion = new Version(remoteVersionStr);
            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version("0.0.0.0");

            if (remoteVersion <= currentVersion)
            {
                Log.Information("No update available. Current: {current}, Remote: {remote}", currentVersion, remoteVersion);
                return;
            }

            Log.Information("Update available: {remote} (current: {current})", remoteVersion, currentVersion);

            // WPF MessageBox ile kullanıcıya sor
            var result = System.Windows.MessageBox.Show(
                $"Yeni sürüm mevcut: v{remoteVersion}\nŞu anki sürüm: v{currentVersion}\n\nGüncellemek ister misiniz?",
                "Güncelleme Mevcut",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                DirectoryController.CreateDirectoryWhenNotExists(updateDirPath);

                var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                var localFilePath = Path.Combine(updateDirPath, fileName);

                // İndirme
                Log.Information("Downloading update from: {url}", downloadUrl);
                using var response = await HttpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                await using (var fs = new FileStream(localFilePath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }

                Log.Information("Update downloaded to: {path}", localFilePath);

                // Setup.exe'yi çalıştır ve uygulamayı kapat
                Process.Start(new ProcessStartInfo
                {
                    FileName = localFilePath,
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
            }
        }
        catch (HttpRequestException e)
        {
            DebugConsole.WriteError(MethodBase.GetCurrentMethod()?.DeclaringType, e);
            Log.Warning(e, "{message}", MethodBase.GetCurrentMethod()?.DeclaringType);
        }
        catch (Exception e)
        {
            DebugConsole.WriteError(MethodBase.GetCurrentMethod()?.DeclaringType, e);
            Log.Error(e, "{message}", MethodBase.GetCurrentMethod()?.DeclaringType);
        }
    }

    public static void RemoveUpdateFiles(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            foreach (var filePath in Directory.GetFiles(path, "AlbionAAK-*"))
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or PathTooLongException)
        {
            DebugConsole.WriteError(MethodBase.GetCurrentMethod()?.DeclaringType, ex);
            Log.Warning(ex, "{message}", MethodBase.GetCurrentMethod()?.DeclaringType);
        }
        catch (Exception e)
        {
            DebugConsole.WriteError(MethodBase.GetCurrentMethod()?.DeclaringType, e);
            Log.Error(e, "{message}", MethodBase.GetCurrentMethod()?.DeclaringType);
        }
    }
}
