using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StatisticsAnalysisTool.Models;

namespace StatisticsAnalysisTool.UserControls;

public partial class AvalonRoadControl : UserControl
{
    private List<AvalonMapModel> _allMaps = new();
    private string _mapsImageFolder;

    public AvalonRoadControl()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        LoadMaps();
        ApplyFilter();
    }

    private void LoadMaps()
    {
        try
        {
            // JSON dosyasını bul
            var exeDir = AppContext.BaseDirectory;
            var jsonPath = Path.Combine(exeDir, "Avalon", "avalon_maps.json");

            // Geliştirme ortamında src klasöründen bul
            if (!File.Exists(jsonPath))
            {
                var devPath = FindDevPath("Avalon", "avalon_maps.json");
                if (devPath != null) jsonPath = devPath;
            }

            if (!File.Exists(jsonPath)) return;

            var json = File.ReadAllText(jsonPath);
            _allMaps = JsonSerializer.Deserialize<List<AvalonMapModel>>(json) ?? new();

            // Resim klasörünü bul
            _mapsImageFolder = Path.Combine(Path.GetDirectoryName(jsonPath)!, "avalon_map_images", "maps");

            ToplamText.Text = _allMaps.Count.ToString();
        }
        catch (Exception ex)
        {
            ToplamText.Text = $"Hata: {ex.Message}";
        }
    }

    private static string FindDevPath(string folder, string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var tryPath = Path.Combine(dir, "src", "StatisticsAnalysisTool", folder, file);
            if (File.Exists(tryPath)) return tryPath;
            tryPath = Path.Combine(dir, folder, file);
            if (File.Exists(tryPath)) return tryPath;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    // ══════════════════════════════════════════════════════════════
    // Filter & Sort
    // ══════════════════════════════════════════════════════════════

    private void Arama_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void Sort_Changed(object sender, RoutedEventArgs e) => ApplyFilter();
    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (_allMaps.Count == 0) return;

        var query = AramaKutusu.Text?.Trim().ToLowerInvariant() ?? "";

        var filtered = _allMaps.AsEnumerable();

        // Tier filtre
        var allowedTiers = new HashSet<int>();
        if (FilterT4.IsChecked == true) allowedTiers.Add(4);
        if (FilterT6.IsChecked == true) allowedTiers.Add(6);
        if (FilterT8.IsChecked == true) allowedTiers.Add(8);
        filtered = filtered.Where(m => allowedTiers.Contains(m.Tier));

        // Gold filtre
        if (FilterGold.IsChecked == true)
            filtered = filtered.Where(m => m.HasGold);

        // Arama
        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(m =>
                m.Name.ToLowerInvariant().Contains(query) ||
                m.Name.Replace("-", " ").ToLowerInvariant().Contains(query));
        }

        // Sıralama
        var list = SortTier.IsChecked == true
            ? filtered.OrderByDescending(m => m.Tier).ThenBy(m => m.Name).ToList()
            : filtered.OrderBy(m => m.Name).ToList();

        FiltreliSayiText.Text = list.Count < _allMaps.Count ? $"({list.Count} gösteriliyor)" : "";

        RenderCards(list);
    }

    // ══════════════════════════════════════════════════════════════
    // Card Rendering
    // ══════════════════════════════════════════════════════════════

    private void RenderCards(List<AvalonMapModel> maps)
    {
        MapCardsPanel.Children.Clear();

        foreach (var map in maps)
        {
            var card = CreateCard(map);
            MapCardsPanel.Children.Add(card);
        }
    }

    private Border CreateCard(AvalonMapModel map)
    {
        // Kart arka planı
        Brush cardBg;
        Brush cardBorder;

        if (map.Tier == 8)
        {
            cardBg = new LinearGradientBrush(
                Color.FromArgb(0xFF, 0x14, 0x3D, 0x6B),
                Color.FromArgb(0xFF, 0x0A, 0x25, 0x45), 135);
            cardBorder = new SolidColorBrush(Color.FromRgb(0x00, 0x66, 0xCC));
        }
        else if (map.HasGold)
        {
            cardBg = new LinearGradientBrush(
                Color.FromArgb(0xFF, 0x3A, 0x32, 0x10),
                Color.FromArgb(0xFF, 0x50, 0x40, 0x14), 135);
            cardBorder = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xD7, 0x00));
        }
        else
        {
            cardBg = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x28));
            cardBorder = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        }

        var card = new Border
        {
            Background = cardBg,
            BorderBrush = cardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Width = 290,
            Margin = new Thickness(5),
            Padding = new Thickness(0),
            Cursor = Cursors.Hand,
            Tag = map
        };

        var stack = new StackPanel();

        // ── Header: Map Name + Tier Badge ──
        var header = new Grid { Margin = new Thickness(10, 8, 10, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameText = new TextBlock
        {
            Text = map.Name,
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(nameText, 0);
        header.Children.Add(nameText);

        var tierBg = map.Tier switch
        {
            8 => new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF)),
            6 => new SolidColorBrush(Color.FromArgb(0xCC, 0x50, 0x50, 0x64)),
            _ => new SolidColorBrush(Color.FromArgb(0xCC, 0x40, 0x40, 0x50))
        };

        var tierBadge = new Border
        {
            Background = tierBg,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center
        };
        tierBadge.Child = new TextBlock
        {
            Text = map.TierDisplay,
            Foreground = Brushes.White,
            FontSize = 11,
            FontWeight = FontWeights.Bold
        };
        Grid.SetColumn(tierBadge, 1);
        header.Children.Add(tierBadge);

        stack.Children.Add(header);

        // ── Map Image ──
        var imgBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x15)),
            Margin = new Thickness(8, 2, 8, 4),
            CornerRadius = new CornerRadius(4),
            Height = 140,
            Cursor = Cursors.Hand
        };

        var img = new Image
        {
            Stretch = Stretch.Uniform,
            MaxHeight = 136,
            HorizontalAlignment = HorizontalAlignment.Center,
            Tag = map
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        img.MouseLeftButtonDown += MapImage_Click;

        // Resmi yükle
        if (_mapsImageFolder != null)
        {
            var imgPath = Path.Combine(_mapsImageFolder, map.Image);
            if (File.Exists(imgPath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(imgPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 400;
                    bmp.EndInit();
                    bmp.Freeze();
                    img.Source = bmp;
                }
                catch { }
            }
        }

        imgBorder.Child = img;
        stack.Children.Add(imgBorder);

        // ── Icons Row ──
        var iconsWrap = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(8, 2, 8, 8)
        };

        foreach (var icon in map.GetIcons())
        {
            var iconBadge = CreateIconBadge(icon);
            iconsWrap.Children.Add(iconBadge);
        }

        stack.Children.Add(iconsWrap);

        card.Child = stack;
        return card;
    }

    private static Border CreateIconBadge(MapIcon icon)
    {
        var bgColor = icon.Type switch
        {
            "GREEN" => Color.FromRgb(0x10, 0x60, 0x2B),
            "BLUE" => Color.FromRgb(0x18, 0x5D, 0x8B),
            "GOLD" => Color.FromRgb(0x8B, 0x75, 0x00),
            "DUNGEON_SOLO" => Color.FromRgb(0x8B, 0x20, 0x20),
            "DUNGEON_GROUP" => Color.FromRgb(0xA0, 0x10, 0x10),
            "STONE" => Color.FromRgb(0x5A, 0x5A, 0x5A),
            "WOOD" => Color.FromRgb(0x5A, 0x3A, 0x1A),
            "ORE" => Color.FromRgb(0x6A, 0x4A, 0x2A),
            "FIBER" => Color.FromRgb(0x2A, 0x5A, 0x2A),
            "HIDE" => Color.FromRgb(0x5A, 0x4A, 0x3A),
            _ => Color.FromRgb(0x44, 0x44, 0x44)
        };

        var badge = new Border
        {
            Background = new SolidColorBrush(bgColor),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 3, 6, 3),
            Margin = new Thickness(2),
            ToolTip = $"{icon.Count}x {icon.DisplayName}"
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal };

        // MiniMapMarker görseli varsa ikon + isim göster
        var markerImage = GetMarkerImageName(icon.Type);
        if (markerImage != null)
        {
            try
            {
                var bmp = new BitmapImage(new Uri($"pack://application:,,,/Assets/MiniMapMarker/{markerImage}", UriKind.Absolute));
                bmp.Freeze();

                sp.Children.Add(new Image
                {
                    Source = bmp,
                    Width = 14,
                    Height = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 3, 0)
                });
            }
            catch { }
        }

        // Her badge'e isim ekle
        sp.Children.Add(new TextBlock
        {
            Text = icon.DisplayName,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            FontSize = 9,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 3, 0)
        });

        if (icon.Count > 1)
        {
            sp.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0xCC, 0x33, 0x33)),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(4, 1, 4, 1),
                Child = new TextBlock
                {
                    Text = icon.Count.ToString(),
                    Foreground = Brushes.White,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold
                }
            });
        }

        badge.Child = sp;
        return badge;
    }

    private static string GetMarkerImageName(string type) => type switch
    {
        "STONE" => "minimapmarker_stone.png",
        "WOOD" => "minimapmarker_wood.png",
        "ORE" => "minimapmarker_ore.png",
        "FIBER" => "minimapmarker_fiber.png",
        "HIDE" => "minimapmarker_hide.png",
        "DUNGEON_SOLO" => "solo_dungeon.png",
        "DUNGEON_GROUP" => "group_dungeon.png",
        _ => null
    };

    // ══════════════════════════════════════════════════════════════
    // Büyük Resim Önizleme
    // ══════════════════════════════════════════════════════════════

    private void MapImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Image img && img.Tag is AvalonMapModel map && img.Source != null)
        {
            // Tam çözünürlükte yükle
            var imgPath = Path.Combine(_mapsImageFolder, map.Image);
            if (File.Exists(imgPath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(imgPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    BuyukResimImage.Source = bmp;
                }
                catch { BuyukResimImage.Source = img.Source; }
            }
            else
            {
                BuyukResimImage.Source = img.Source;
            }

            BuyukResimBaslik.Text = map.Name;
            BuyukResimTierText.Text = map.TierDisplay;
            BuyukResimTierBorder.Background = map.Tier switch
            {
                8 => new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF)),
                6 => new SolidColorBrush(Color.FromArgb(0xCC, 0x50, 0x50, 0x64)),
                _ => new SolidColorBrush(Color.FromArgb(0xCC, 0x40, 0x40, 0x50))
            };

            BuyukResimPanel.Visibility = Visibility.Visible;
            e.Handled = true;
        }
    }

    private void BuyukResimKapat_Click(object sender, MouseButtonEventArgs e)
    {
        BuyukResimPanel.Visibility = Visibility.Collapsed;
    }
}
