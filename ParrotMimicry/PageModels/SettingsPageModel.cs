using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParrotMimicry.Services;
using System.Threading.Tasks;

namespace ParrotMimicry.PageModels;

public partial class SettingsPageModel : ObservableObject
{
    private string _rootFolder = string.Empty;
    private string _openSubtitlesApiKey = string.Empty;
    private DatabaseService _databaseService;
    private SubtitleParserService _subtitleParserService;

    public string RootFolder
    {
        get => _rootFolder;
        set => SetProperty(ref _rootFolder, value);
    }

    public string OpenSubtitlesApiKey
    {
        get => _openSubtitlesApiKey;
        set => SetProperty(ref _openSubtitlesApiKey, value);
    }

    private double _subtitleProgress;
    public double SubtitleProgress
    {
        get => _subtitleProgress;
        set => SetProperty(ref _subtitleProgress, value);
    }

    private string _subtitleMessage = string.Empty;
    public string SubtitleMessage
    {
        get => _subtitleMessage;
        set => SetProperty(ref _subtitleMessage, value);
    }
    
    public SettingsPageModel(DatabaseService databaseService, SubtitleParserService subtitleParserService)
    {
        _databaseService = databaseService;
        _subtitleParserService = subtitleParserService;
        _subtitleParserService.BatchParseSubtitles_ProgressChanged += _subtitleParserService_BatchParseSubtitles_ProgressChanged;
        LoadSettings();
    }

    private void _subtitleParserService_BatchParseSubtitles_ProgressChanged(int value, int totals)
    {
        SubtitleProgress = (double)value / totals;
        SubtitleMessage = $"正在解析字幕文件...{(int)(SubtitleProgress * 100)}%";
    }

    [RelayCommand]
    private async Task SelectRootFolder()
    {
        try
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var result = await FilePicker.PickAsync();
                if (result != null)
                {
                    var path = Path.GetDirectoryName(result.FullPath);
                    if (!string.IsNullOrEmpty(path))
                    {
                        path = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(path))
                        {
                            RootFolder = path;
                            SaveSettings();
                        }
                    }
                }
            }
            else
            {
                var folder = await FolderPicker.Default.PickAsync();
                if (folder.Folder != null)
                {
                    var folderPath = folder.Folder.Path;
                    if (DeviceInfo.Platform == DevicePlatform.WinUI)
                    {
                        // 在Windows平台上，需要确保路径格式正确
                        folderPath = Path.GetFullPath(folderPath);
                    }
                    RootFolder = folderPath;
                    SaveSettings();
                }
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("错误", $"选择文件夹时发生错误：{ex.Message}", "确定");
        }
    }

    private void LoadSettings()
    {
        RootFolder = Preferences.Default.Get("root_folder", string.Empty);
        OpenSubtitlesApiKey = Preferences.Default.Get("opensubtitles_api_key", string.Empty);
    }

    private void SaveSettings()
    {
        Preferences.Default.Set("root_folder", RootFolder);
        Preferences.Default.Set("opensubtitles_api_key", OpenSubtitlesApiKey);
    }

    [RelayCommand]
    private void SaveApiKey()
    {
        SaveSettings();
    }

    [RelayCommand]
    private async Task InitializeDatabase()
    {
        bool confirm = await Shell.Current.DisplayAlert("警告", "初始化数据库将清空所有数据，是否继续？", "确定", "取消");
        if (confirm)
        {
            await _databaseService.InitializeAsync();
        }
    }

    [RelayCommand]
    private async Task ExtractSubtitles()
    {
        if (string.IsNullOrWhiteSpace(RootFolder))
        {
            await Shell.Current.DisplayAlert("错误", "请先设置根文件夹", "确定");
            return;
        }
        else
        {
            var subtitleFiles = Directory.GetFiles(RootFolder, "*.srt", SearchOption.AllDirectories).ToList();
            await _subtitleParserService.BatchParseSubtitlesAsync(subtitleFiles);
        }
    }
}