using ParrotMimicry.Services.SubtitleSources;

namespace ParrotMimicry.Services;

public class SearchSubtitlesService
{
    private readonly List<ISubtitleSource> _subtitleSources;
    private readonly OpenSubtitlesSource _openSubtitlesSource;

    public SearchSubtitlesService()
    {
        _openSubtitlesSource = new OpenSubtitlesSource();
        _subtitleSources = new List<ISubtitleSource>
        {
            _openSubtitlesSource,
            new SubHDSource()
        };
    }

    public void SetApiKey(string apiKey)
    {
        _openSubtitlesSource.SetApiKey(apiKey);
    }

    public List<ISubtitleSource> GetAvailableSources()
    {
        return _subtitleSources;
    }

    public async Task<List<SubtitleInfo>> SearchSubtitlesAsync(string fileName, ISubtitleSource source)
    {
        try
        {
            return await source.SearchSubtitlesAsync(fileName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"从{source.Name}搜索字幕失败：{ex.Message}");
        }
    }

    public async Task<string> DownloadSubtitleAsync(string downloadUrl, ISubtitleSource source)
    {
        try
        {
            return await source.DownloadSubtitleAsync(downloadUrl);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"从{source.Name}下载字幕失败：{ex.Message}");
        }
    }
}

public class SubtitleInfo
{
    public string Id { get; set; }
    public string FileName { get; set; }
    public string Language { get; set; }
    public string DownloadUrl { get; set; }
}