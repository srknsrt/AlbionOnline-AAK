using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace StatisticsAnalysisTool.Models;

public class BuildModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "PvP";

    [JsonPropertyName("created_by")]
    public string CreatedBy { get; set; } = "";

    [JsonPropertyName("image_data")]
    public string ImageData { get; set; } = "";

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("likes")]
    public int Likes { get; set; }

    [JsonIgnore]
    public BitmapImage BuildImage
    {
        get
        {
            try
            {
                if (string.IsNullOrEmpty(ImageData)) return null;
                var bytes = Convert.FromBase64String(ImageData);
                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = new MemoryStream(bytes);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                return null;
            }
        }
    }

    [JsonIgnore]
    public string CreatedAtFormatted
    {
        get
        {
            if (DateTime.TryParse(CreatedAt, out var dt))
                return dt.ToString("dd.MM.yyyy HH:mm");
            return CreatedAt;
        }
    }
}
