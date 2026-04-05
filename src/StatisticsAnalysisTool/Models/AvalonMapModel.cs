using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StatisticsAnalysisTool.Models;

public class AvalonMapModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("tier")]
    public int Tier { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; } = "";

    [JsonPropertyName("chests")]
    public string Chests { get; set; } = "";

    [JsonPropertyName("dungeons")]
    public string Dungeons { get; set; } = "";

    [JsonPropertyName("resources")]
    public string Resources { get; set; } = "";

    public string TierDisplay => $"T{Tier}";

    public bool HasGold => Chests.Contains("GOLD");

    /// <summary>
    /// Chests, Dungeons, Resources alanlarını parse edip ikon listesi döndürür
    /// </summary>
    public List<MapIcon> GetIcons()
    {
        var icons = new List<MapIcon>();
        ParseField(Chests, icons);
        ParseField(Dungeons, icons);
        ParseField(Resources, icons);
        return icons;
    }

    private static void ParseField(string field, List<MapIcon> icons)
    {
        if (string.IsNullOrEmpty(field)) return;

        var parts = field.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Format: "1x GREEN small" veya "2x DUNGEON_SOLO small"
            var tokens = trimmed.Split(' ');
            if (tokens.Length < 2) continue;

            var countStr = tokens[0].Replace("x", "");
            int.TryParse(countStr, out var count);
            if (count == 0) count = 1;

            var type = tokens[1];
            var size = tokens.Length >= 3 ? tokens[2] : "small";

            // Aynı tip varsa sayıyı topla
            var existing = icons.Find(i => i.Type == type);
            if (existing != null)
                existing.Count += count;
            else
                icons.Add(new MapIcon { Type = type, Count = count, Size = size });
        }
    }
}

public class MapIcon
{
    public string Type { get; set; } = "";
    public int Count { get; set; }
    public string Size { get; set; } = "small";

    public string DisplayName => Type switch
    {
        "GREEN" => "Green",
        "BLUE" => "Blue",
        "GOLD" => "Gold",
        "DUNGEON_SOLO" => "Solo Dungeon",
        "DUNGEON_GROUP" => "Group Dungeon",
        "STONE" => "Stone",
        "WOOD" => "Wood",
        "ORE" => "Ore",
        "FIBER" => "Fiber",
        "HIDE" => "Hide",
        _ => Type
    };

    public string IconChar => Type switch
    {
        "GREEN" => "\uD83D\uDFE9",   // 🟩
        "BLUE" => "\uD83D\uDFE6",    // 🟦
        "GOLD" => "\uD83D\uDFE8",    // 🟨
        "DUNGEON_SOLO" => "\u2694",   // ⚔
        "DUNGEON_GROUP" => "\uD83D\uDC79", // 👹
        "STONE" => "\u26F0",          // ⛰
        "WOOD" => "\uD83C\uDF32",    // 🌲
        "ORE" => "\u26CF",           // ⛏
        "FIBER" => "\uD83C\uDF3F",   // 🌿
        "HIDE" => "\uD83E\uDE76",    // 🦴 (yakın)
        _ => "\u2753"                 // ❓
    };
}
