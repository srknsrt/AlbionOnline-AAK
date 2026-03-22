using AutoUpdaterDotNET;
using Serilog;
using StatisticsAnalysisTool.Common.UserSettings;
using StatisticsAnalysisTool.Properties;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using StatisticsAnalysisTool.Diagnostics;
using Application = System.Windows.Application;

namespace StatisticsAnalysisTool.Common;

public static class AutoUpdateController
{
    public static async Task AutoUpdateAsync(bool reportErrors = false)
    {
        var updateDirPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AlbionAAK", "Updates");
        var executablePath = Path.Combine(Environment.CurrentDirectory, "StatisticsAnalysisTool.exe");
        string currentUpdateUrl = string.Empty;

        RemoveUpdateFiles(updateDirPath);

        try
        {
            // Kullanici tarafindan ayarlanmis URL'yi once dene, yoksa varsayilani kullan
            var userDefinedUrl = SettingsController.CurrentSettings.UpdateXmlUrl;
            var stableUrl = !string.IsNullOrWhiteSpace(userDefinedUrl)
                ? userDefinedUrl
                : Settings.Default.AutoUpdateConfigUrl;
            var preReleaseUrl = Settings.Default.AutoUpdatePreReleaseConfigUrl;

            var checkUrl = SettingsController.CurrentSettings.IsSuggestPreReleaseUpdatesActive
                ? preReleaseUrl
                : stableUrl;

            var isUrlAccessibleResult = await HttpClientUtils.IsUrlAccessible(checkUrl);

            if (isUrlAccessibleResult is { IsAccessible: true, IsProxyActive: true })
            {
                AutoUpdater.Proxy = new WebProxy(SettingsController.CurrentSettings.ProxyUrlWithPort);
                currentUpdateUrl = checkUrl;
            }
            else if (isUrlAccessibleResult is { IsAccessible: true, IsProxyActive: false })
            {
                currentUpdateUrl = checkUrl;
            }

            if (string.IsNullOrEmpty(currentUpdateUrl))
            {
                return;
            }

            // Arkaplanda indirme icin Synchronous=false
            AutoUpdater.Synchronous = false;
            AutoUpdater.ApplicationExitEvent -= AutoUpdaterApplicationExit;

            DirectoryController.CreateDirectoryWhenNotExists(updateDirPath);

            AutoUpdater.DownloadPath = updateDirPath;
            AutoUpdater.ExecutablePath = executablePath;
            AutoUpdater.RunUpdateAsAdmin = false;
            AutoUpdater.ReportErrors = reportErrors;
            AutoUpdater.ShowSkipButton = true;
            AutoUpdater.TopMost = false;

            AutoUpdater.Start(currentUpdateUrl);

            AutoUpdater.ApplicationExitEvent += AutoUpdaterApplicationExit;
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

    private static void AutoUpdaterApplicationExit()
    {
        AutoUpdater.ApplicationExitEvent -= AutoUpdaterApplicationExit;
        Application.Current.Shutdown();
    }

    public static void RemoveUpdateFiles(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            foreach (var filePath in Directory.GetFiles(path, "AlbionAAK-*-x64.zip"))
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