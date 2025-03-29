using System.Net.Http.Headers;
using System.Text.Json;

namespace ParrotMimicry.Services.SubtitleSources;

public class OpenSubtitlesSource : ISubtitleSource
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.opensubtitles.com/api/v1";
    private string _apiKey;

    public string Name => "OpenSubtitles";

    public OpenSubtitlesSource()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _apiKey = Preferences.Default.Get("opensubtitles_api_key", string.Empty);
    }

    public void SetApiKey(string apiKey)
    {
        _apiKey = apiKey;
        Preferences.Default.Set("opensubtitles_api_key", apiKey);
    }

    public async Task<List<SubtitleInfo>> SearchSubtitlesAsync(string fileName)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("请先在设置中配置OpenSubtitles API Key");
        }

        var request = new HttpRequestMessage(HttpMethod.Get, $"/search?query={Uri.EscapeDataString(fileName)}&languages=en,zh-CN");
        request.Headers.Add("Api-Key", _apiKey);
        request.Headers.Add("User-Agent", "ParrotMimicry v1.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new InvalidOperationException("搜索字幕失败：服务器返回了HTML页面，可能是API密钥无效或服务暂时不可用");
            }

            try
            {
                var errorMessage = JsonSerializer.Deserialize<JsonElement>(content);
                var message = errorMessage.GetProperty("message").GetString();
                if (message?.Contains("cannot consume", StringComparison.OrdinalIgnoreCase) == true)
                {
                    throw new InvalidOperationException("API使用次数已达到限制，请稍后再试或升级您的OpenSubtitles账户");
                }
                throw new InvalidOperationException($"搜索字幕失败：{message}");
            }
            catch (JsonException)
            {
                throw new InvalidOperationException($"搜索字幕失败：{response.StatusCode} - {content}");
            }
        }

        var searchResult = JsonSerializer.Deserialize<SearchResponse>(content);
        if (searchResult?.Data == null || !searchResult.Data.Any())
        {
            throw new InvalidOperationException("未找到匹配的字幕");
        }

        return searchResult?.Data?.Select(item => new SubtitleInfo
        {
            Id = item.Id,
            FileName = item.Attributes.Release ?? item.Attributes.MovieName,
            Language = item.Attributes.LanguageName,
            DownloadUrl = item.Attributes.DownloadUrl
        }).ToList() ?? new List<SubtitleInfo>();
    }

    public async Task<string> DownloadSubtitleAsync(string downloadUrl)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("请先在设置中配置OpenSubtitles API Key");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/download");
        request.Headers.Add("Api-Key", _apiKey);
        request.Headers.Add("User-Agent", "ParrotMimicry v1.0");
        request.Headers.Add("Accept-Language", "en,zh-CN");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var content = new StringContent(JsonSerializer.Serialize(new { file_id = downloadUrl }), System.Text.Encoding.UTF8, "application/json");
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var downloadResponse = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        var link = downloadResponse.GetProperty("link").GetString();

        var downloadRequest = new HttpRequestMessage(HttpMethod.Get, link);
        var downloadResult = await _httpClient.SendAsync(downloadRequest);
        downloadResult.EnsureSuccessStatusCode();

        return await downloadResult.Content.ReadAsStringAsync();
    }
}

public class SearchResponse
{
    public List<SubtitleData> Data { get; set; }
}

public class SubtitleData
{
    public string Id { get; set; }
    public SubtitleAttributes Attributes { get; set; }
}

public class SubtitleAttributes
{
    public string Release { get; set; }
    public string MovieName { get; set; }
    public string LanguageName { get; set; }
    public string SubFormat { get; set; }
    public string DownloadUrl { get; set; }
}