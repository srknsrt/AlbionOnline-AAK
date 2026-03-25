using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Models;

namespace StatisticsAnalysisTool.UserControls;

public partial class BuildsControl : UserControl
{
    private string _selectedImageBase64;

    public BuildsControl()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        try { await BuildleriYukle(); }
        catch { DurumText.Text = "Buildler yüklenemedi."; }
    }

    private async System.Threading.Tasks.Task BuildleriYukle(string kategori = null)
    {
        try
        {
            DurumText.Text = "Buildler yükleniyor...";
            var builds = await BuildsController.GetBuildsAsync(kategori);
            BuildListesi.ItemsSource = builds;
            DurumText.Text = $"{builds.Count} build bulundu";
        }
        catch (Exception ex)
        {
            DurumText.Text = $"Hata: {ex.Message}";
        }
    }

    private async void Yenile_Click(object sender, RoutedEventArgs e)
    {
        var secili = (KategoriCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        await BuildleriYukle(secili);
    }

    private async void Kategori_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var secili = (KategoriCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        await BuildleriYukle(secili);
    }

    private void YeniBuild_Click(object sender, RoutedEventArgs e)
    {
        BuildFormu.Visibility = Visibility.Visible;
    }

    private void FormIptal_Click(object sender, RoutedEventArgs e)
    {
        BuildFormu.Visibility = Visibility.Collapsed;
        FormuTemizle();
    }

    // ── Resim Seçme ──────────────────────────────────────────────

    private void ResimSec_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Build Resmi Seç",
            Filter = "Resim Dosyaları|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Tüm Dosyalar|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var bytes = File.ReadAllBytes(dialog.FileName);

            // Resmi küçült (max 800px genişlik, JPEG %80 kalite)
            var resized = ResizeImage(bytes, 800);
            _selectedImageBase64 = Convert.ToBase64String(resized);

            // Önizleme göster
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = new MemoryStream(resized);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();

            ResimOnizleme.Source = img;
            ResimOnizlemeBorder.Visibility = Visibility.Visible;
            ResimDurum.Text = $"Resim seçildi ({resized.Length / 1024} KB)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Resim yüklenemedi: {ex.Message}", "Hata");
        }
    }

    private static byte[] ResizeImage(byte[] imageBytes, int maxWidth)
    {
        using var ms = new MemoryStream(imageBytes);
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        double scale = 1.0;
        if (frame.PixelWidth > maxWidth)
            scale = (double)maxWidth / frame.PixelWidth;

        var transformed = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));

        var encoder = new JpegBitmapEncoder { QualityLevel = 80 };
        encoder.Frames.Add(BitmapFrame.Create(transformed));

        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    // ── Build Kaydet ────────────────────────────────────────────

    private async void FormKaydet_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(TxtBuildAdi.Text))
            {
                MessageBox.Show("Build adı boş olamaz!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_selectedImageBase64))
            {
                MessageBox.Show("Lütfen bir build resmi seçin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var build = new BuildModel
            {
                Title = TxtBuildAdi.Text.Trim(),
                Description = TxtAciklama.Text.Trim(),
                Category = (FormKategori.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PvP",
                CreatedBy = TxtOlusturan.Text.Trim(),
                ImageData = _selectedImageBase64
            };

            DurumText.Text = "Build kaydediliyor...";
            var sonuc = await BuildsController.CreateBuildAsync(build);

            if (sonuc)
            {
                BuildFormu.Visibility = Visibility.Collapsed;
                FormuTemizle();
                await BuildleriYukle();
                DurumText.Text = "Build başarıyla paylaşıldı!";
            }
            else
            {
                DurumText.Text = "Build kaydedilemedi!";
                MessageBox.Show("Build kaydedilemedi. İnternet bağlantısını kontrol edin.", "Hata");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex}", "Build Kayıt Hatası");
        }
    }

    private async void Like_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BuildModel build)
        {
            await BuildsController.LikeBuildAsync(build.Id, build.Likes);
            var secili = (KategoriCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            await BuildleriYukle(secili);
        }
    }

    // ── Büyük Resim Önizleme ────────────────────────────────────

    private void BuildImage_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Image img && img.Tag is BuildModel build && build.BuildImage != null)
        {
            BuyukResimImage.Source = build.BuildImage;
            BuyukResimBaslik.Text = build.Title;
            BuyukResimKategoriText.Text = build.Category;
            BuyukResimPanel.Visibility = Visibility.Visible;
            e.Handled = true;
        }
    }

    private void BuyukResimKapat_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BuyukResimPanel.Visibility = Visibility.Collapsed;
    }

    private void FormuTemizle()
    {
        TxtBuildAdi.Text = "";
        TxtAciklama.Text = "";
        TxtOlusturan.Text = "";
        _selectedImageBase64 = null;
        ResimOnizleme.Source = null;
        ResimOnizlemeBorder.Visibility = Visibility.Collapsed;
        ResimDurum.Text = "Henüz resim seçilmedi";
    }
}
