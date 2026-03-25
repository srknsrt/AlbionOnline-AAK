using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StatisticsAnalysisTool.Models;

namespace StatisticsAnalysisTool.Common;

public static class BuildsController
{
    private const string SupabaseUrl = "https://fwaszogbepswybvtecrk.supabase.co";
    private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZ3YXN6b2diZXBzd3lidnRlY3JrIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc3MzE2ODQyMSwiZXhwIjoyMDg4NzQ0NDIxfQ.N0BZAf3i8S3j4PAnf7KrgeM7H6FID4AuRhQG3rqtikI";

    private static readonly HttpClient Client = new();

    private static HttpRequestMessage CreateRequest(HttpMethod method, string endpoint)
    {
        var request = new HttpRequestMessage(method, $"{SupabaseUrl}/rest/v1/{endpoint}");
        request.Headers.Add("apikey", SupabaseKey);
        request.Headers.Add("Authorization", $"Bearer {SupabaseKey}");
        return request;
    }

    public static async Task<List<BuildModel>> GetBuildsAsync(string category = null)
    {
        try
        {
            var endpoint = "builds?select=*&order=created_at.desc";
            if (!string.IsNullOrEmpty(category) && category != "Tümü")
                endpoint += $"&category=eq.{category}";

            var request = CreateRequest(HttpMethod.Get, endpoint);
            var response = await Client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<BuildModel>>(json) ?? new List<BuildModel>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Builds yükleme hatası: {ex.Message}");
            return new List<BuildModel>();
        }
    }

    public static async Task<bool> CreateBuildAsync(BuildModel build)
    {
        try
        {
            var request = CreateRequest(HttpMethod.Post, "builds");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    title = build.Title,
                    description = build.Description,
                    category = build.Category,
                    created_by = build.CreatedBy,
                    image_data = build.ImageData,
                    created_at = DateTime.UtcNow.ToString("o"),
                    likes = 0
                }),
                Encoding.UTF8, "application/json");

            var response = await Client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Build kayıt hatası: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> DeleteBuildAsync(int id)
    {
        try
        {
            var request = CreateRequest(HttpMethod.Delete, $"builds?id=eq.{id}");
            var response = await Client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Build silme hatası: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> LikeBuildAsync(int id, int currentLikes)
    {
        try
        {
            var request = CreateRequest(new HttpMethod("PATCH"), $"builds?id=eq.{id}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { likes = currentLikes + 1 }),
                Encoding.UTF8, "application/json");

            var response = await Client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
