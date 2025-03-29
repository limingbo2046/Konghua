using System.Collections.ObjectModel;
using ParrotMimicry.Services;
using ParrotMimicry.Services.SubtitleSources;

namespace ParrotMimicry.Pages;

public partial class SearchSubtitlePage : ContentPage
{
    private string _videoFilePath;
    private readonly SearchSubtitlesService _subtitlesService;
    private ObservableCollection<SubtitleInfo> _subtitles;
    private ISubtitleSource _selectedSource;

    public SearchSubtitlePage(string videoFilePath)
    {
        InitializeComponent();
        _videoFilePath = videoFilePath;
        _subtitlesService = new SearchSubtitlesService();
        _subtitles = new ObservableCollection<SubtitleInfo>();
        SubtitlesListView.ItemsSource = _subtitles;
        VideoFileNameEntry.Text = Path.GetFileNameWithoutExtension(videoFilePath);

        // 初始化字幕源选择器
        var sources = _subtitlesService.GetAvailableSources();
        SubtitleSourcePicker.ItemsSource = sources;
        SubtitleSourcePicker.ItemDisplayBinding = new Binding("Name");
        if (sources.Any())
        {
            SubtitleSourcePicker.SelectedItem = sources[0];
            _selectedSource = sources[0];
        }
        SubtitleSourcePicker.SelectedIndexChanged += OnSubtitleSourceChanged;
    }

    private async void SearchSubtitle_Clicked(object sender, EventArgs e)
    {
        try
        {
            LoadingIndicator.IsRunning = true;
            StatusLabel.Text = "正在搜索字幕...";
            _subtitles.Clear();
            SubtitlesListView.IsVisible = false;

            if (_selectedSource == null)
            {
                throw new InvalidOperationException("请选择字幕源");
            }

            var results = await _subtitlesService.SearchSubtitlesAsync(VideoFileNameEntry.Text, _selectedSource);
            foreach (var subtitle in results)
            {
                _subtitles.Add(subtitle);
            }

            SubtitlesListView.IsVisible = true;
            StatusLabel.Text = $"找到 {results.Count} 个字幕文件";
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", ex.Message, "确定");
            StatusLabel.Text = "搜索字幕失败";
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
        }
    }

    private async void OnSubtitleTapped(object sender, EventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is SubtitleInfo subtitle)
        {
            try
            {
                LoadingIndicator.IsRunning = true;
                StatusLabel.Text = "正在下载字幕...";

                var content = await _subtitlesService.DownloadSubtitleAsync(subtitle.DownloadUrl, _selectedSource);
                var srtFile = Path.ChangeExtension(_videoFilePath, ".srt");
                await File.WriteAllTextAsync(srtFile, content);

                StatusLabel.Text = "字幕下载完成";
                await DisplayAlert("成功", "字幕已下载并保存", "确定");
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", ex.Message, "确定");
                StatusLabel.Text = "下载字幕失败";
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
            }
        }
    }

    private async void Back_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private void OnSubtitleSourceChanged(object sender, EventArgs e)
    {
        _selectedSource = SubtitleSourcePicker.SelectedItem as ISubtitleSource;
    }
}