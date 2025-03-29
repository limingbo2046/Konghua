namespace ParrotMimicry.Services.SubtitleSources;

public interface ISubtitleSource
{
    string Name { get; }
    Task<List<SubtitleInfo>> SearchSubtitlesAsync(string fileName);
    Task<string> DownloadSubtitleAsync(string downloadUrl);
}