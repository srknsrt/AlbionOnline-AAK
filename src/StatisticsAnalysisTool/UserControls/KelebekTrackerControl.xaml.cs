using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StatisticsAnalysisTool.UserControls;

public partial class KelebekTrackerControl : UserControl
{
    private Process _statsProcess;
    private Process _mightProcess;

    private static readonly string TrackerDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Trackers");

    public KelebekTrackerControl()
    {
        InitializeComponent();
        KonsolYaz("[Kelebek Tracker hazır]\n");
        KonsolYaz($"Tracker klasörü: {TrackerDir}\n");
        KonsolYaz("Paketler bellekte birikir → 'Veritabanına Aktar' ile kaydet.\n\n");
    }

    // ── Oyuncu Stats Tracker ─────────────────────────────────────

    private void StatsBaslat_Click(object sender, RoutedEventArgs e)
    {
        var script = Path.Combine(TrackerDir, "albion_tracker.py");
        if (!File.Exists(script))
        {
            KonsolYaz($"[HATA] albion_tracker.py bulunamadı: {script}\n");
            return;
        }

        var pythonYolu = BulPython();
        KonsolYaz($"[Python yolu: {pythonYolu}]\n");
        KonsolYaz($"[Script: {script}]\n");

        _statsProcess = ProcessBaslat(pythonYolu, $"\"{script}\"", onCikis: () =>
        {
            Dispatcher.Invoke(() => StatsStatusGuncelle(false));
        });

        if (_statsProcess != null)
        {
            StatsStatusGuncelle(true);
            KonsolYaz("[Stats Tracker başlatıldı]\n");
        }
    }

    private async void StatsDurdur_Click(object sender, RoutedEventArgs e)
    {
        if (_statsProcess != null && !_statsProcess.HasExited)
        {
            try
            {
                _statsProcess.StandardInput.WriteLine("FLUSH");
                KonsolYaz("[Durdurulmadan once veritabanina aktariliyor...]\n");
                await Task.Delay(3000);
            }
            catch { }
        }
        ProcessDurdur(_statsProcess);
        _statsProcess = null;
        StatsStatusGuncelle(false);
        KonsolYaz("[Stats Tracker durduruldu]\n");
    }

    private void StatsFlush_Click(object sender, RoutedEventArgs e)
    {
        if (_statsProcess == null || _statsProcess.HasExited)
        {
            KonsolYaz("[HATA] Stats Tracker çalışmıyor.\n");
            return;
        }
        try
        {
            _statsProcess.StandardInput.WriteLine("FLUSH");
            KonsolYaz("[💾 Veritabanına aktarma komutu gönderildi...]\n");
        }
        catch (Exception ex)
        {
            KonsolYaz($"[HATA] Komut gönderilemedi: {ex.Message}\n");
        }
    }

    private void StatsStatusGuncelle(bool calisiyor)
    {
        StatsStatusDot.Fill = calisiyor ? Brushes.LimeGreen : new SolidColorBrush(Color.FromRgb(85, 85, 85));
        StatsBaslatBtn.IsEnabled = !calisiyor;
        StatsDurdurBtn.IsEnabled = calisiyor;
        StatsFlushBtn.IsEnabled = calisiyor;
    }

    // ── Might Tracker ────────────────────────────────────────────

    private void MightBaslat_Click(object sender, RoutedEventArgs e)
    {
        var script = Path.Combine(TrackerDir, "might_tracker.py");
        if (!File.Exists(script))
        {
            KonsolYaz($"[HATA] might_tracker.py bulunamadı: {script}\n");
            return;
        }

        var pythonYoluM = BulPython();
        KonsolYaz($"[Python yolu: {pythonYoluM}]\n");
        KonsolYaz($"[Script: {script}]\n");

        _mightProcess = ProcessBaslat(pythonYoluM, $"\"{script}\"", onCikis: () =>
        {
            Dispatcher.Invoke(() => MightStatusGuncelle(false));
        });

        if (_mightProcess != null)
        {
            MightStatusGuncelle(true);
            KonsolYaz("[Might Tracker başlatıldı]\n");
        }
    }

    private async void MightDurdur_Click(object sender, RoutedEventArgs e)
    {
        if (_mightProcess != null && !_mightProcess.HasExited)
        {
            try
            {
                _mightProcess.StandardInput.WriteLine("FLUSH");
                KonsolYaz("[Durdurulmadan once veritabanina aktariliyor...]\n");
                await Task.Delay(3000);
            }
            catch { }
        }
        ProcessDurdur(_mightProcess);
        _mightProcess = null;
        MightStatusGuncelle(false);
        KonsolYaz("[Might Tracker durduruldu]\n");
    }

    private void MightFlush_Click(object sender, RoutedEventArgs e)
    {
        if (_mightProcess == null || _mightProcess.HasExited)
        {
            KonsolYaz("[HATA] Might Tracker çalışmıyor.\n");
            return;
        }
        try
        {
            _mightProcess.StandardInput.WriteLine("FLUSH");
            KonsolYaz("[💾 Veritabanına aktarma komutu gönderildi...]\n");
        }
        catch (Exception ex)
        {
            KonsolYaz($"[HATA] Komut gönderilemedi: {ex.Message}\n");
        }
    }

    private void MightStatusGuncelle(bool calisiyor)
    {
        MightStatusDot.Fill = calisiyor ? Brushes.LimeGreen : new SolidColorBrush(Color.FromRgb(85, 85, 85));
        MightBaslatBtn.IsEnabled = !calisiyor;
        MightDurdurBtn.IsEnabled = calisiyor;
        MightFlushBtn.IsEnabled = calisiyor;
    }

    // ── Yardımcı Metodlar ────────────────────────────────────────

    private static string BulPython()
    {
        var adaylar = new[]
        {
            "python",
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Python\pythoncore-3.14-64\python.exe",
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Python\pythoncore-3.13-64\python.exe",
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Python\pythoncore-3.12-64\python.exe",
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Python\bin\python.exe",
            @"C:\Python313\python.exe",
            @"C:\Python312\python.exe",
            @"C:\Program Files\Python313\python.exe",
            @"C:\Program Files\Python312\python.exe",
        };
        foreach (var aday in adaylar)
        {
            if (aday == "python") continue;
            if (File.Exists(aday)) return aday;
        }
        // PATH'te ara
        try
        {
            var p = Process.Start(new ProcessStartInfo("where.exe", "python")
            {
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
            });
            var sonuc = p?.StandardOutput.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(sonuc) && File.Exists(sonuc)) return sonuc;
        }
        catch { }
        return "python";
    }

    private Process ProcessBaslat(string dosya, string argümanlar, Action onCikis)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = dosya,
                Arguments = argümanlar,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            p.OutputDataReceived += (s, ev) =>
            {
                if (ev.Data != null)
                    Dispatcher.Invoke(() => KonsolYaz(ev.Data + "\n"));
            };
            p.ErrorDataReceived += (s, ev) =>
            {
                if (ev.Data != null)
                    Dispatcher.Invoke(() => KonsolYaz("[ERR] " + ev.Data + "\n"));
            };
            p.Exited += (s, ev) => onCikis?.Invoke();

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            return p;
        }
        catch (Exception ex)
        {
            KonsolYaz($"[HATA] Başlatılamadı: {ex.Message}\n");
            return null;
        }
    }

    private static void ProcessDurdur(Process p)
    {
        try { p?.Kill(entireProcessTree: true); } catch { }
    }

    private void KonsolYaz(string metin)
    {
        KonsolCikti.AppendText(metin);
        KonsolScroll.ScrollToEnd();
    }

    private void Temizle_Click(object sender, RoutedEventArgs e)
    {
        KonsolCikti.Clear();
    }
}
