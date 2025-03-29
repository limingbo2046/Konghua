using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ParrotMimicry.Services.SubtitleSources;

public class SubHDSource : ISubtitleSource
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://subhd.tv";

    public string Name => "SubHD";

    public SubHDSource()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ParrotMimicry v1.0");
    }

    public async Task<List<SubtitleInfo>> SearchSubtitlesAsync(string fileName)
    {
        try
        {
            var searchUrl = $"/search/{Uri.EscapeDataString(fileName)}";
            var response = await _httpClient.GetAsync(searchUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"搜索字幕失败：{response.StatusCode}");
            }

            // 使用正则表达式解析搜索结果页面
            var subtitlePattern = @"<div class=""d-flex flex-row"">\s*<div class=""flex-fill"">\s*<a href=""(/subtitles/[^""]*)"">([^<]*)</a>.*?<span class=""label"">([^<]*)</span>";
            var matches = Regex.Matches(content, subtitlePattern, RegexOptions.Singleline);

            var subtitles = new List<SubtitleInfo>();
            foreach (Match match in matches)
            {
                var subtitleInfo = new SubtitleInfo
                {
                    Id = match.Groups[1].Value,
                    FileName = match.Groups[2].Value.Trim(),
                    Language = match.Groups[3].Value.Trim(),
                    DownloadUrl = match.Groups[1].Value
                };
                subtitles.Add(subtitleInfo);
            }

            if (!subtitles.Any())
            {
                throw new InvalidOperationException("未找到匹配的字幕");
            }

            return subtitles;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"搜索字幕失败：{ex.Message}");
        }
    }

    public async Task<string> DownloadSubtitleAsync(string downloadUrl)
    {
        try
        {
            // 获取字幕详情页
            var detailResponse = await _httpClient.GetAsync(downloadUrl);
            var detailContent = await detailResponse.Content.ReadAsStringAsync();

            // 解析下载链接
            var downloadPattern = @"<a class=""btn btn-primary"" href=""([^""]*)"">下载字幕</a>";
            var match = Regex.Match(detailContent, downloadPattern);

            if (!match.Success)
            {
                throw new InvalidOperationException("无法获取字幕下载链接");
            }

            var directDownloadUrl = match.Groups[1].Value;
            if (!directDownloadUrl.StartsWith("http"))
            {
                directDownloadUrl = BaseUrl + directDownloadUrl;
            }

            // 下载字幕文件
            var downloadResponse = await _httpClient.GetAsync(directDownloadUrl);
            downloadResponse.EnsureSuccessStatusCode();

            return await downloadResponse.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"下载字幕失败：{ex.Message}");
        }
    }
}