using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using WhisperSubtitleApp.Services;

namespace WhisperSubtitleApp;

public partial class MainPage : ContentPage
{
    private readonly WhisperService _whisperService;
    private string _selectedFolderPath = string.Empty;
    private ObservableCollection<AudioFile> _audioFiles = new();

    public MainPage(WhisperService whisperService)
    {
        _whisperService = whisperService;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AudioFilesList.ItemsSource = _audioFiles;
    }
    private void OnModelSelected(object sender, EventArgs e)
    {
        var selectedIndex = (sender as Picker)?.SelectedIndex;
        _whisperService.CurrentModel = (WhisperModelType)selectedIndex.GetValueOrDefault();
        
    }

    private async void OnSelectFolderClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a" } },
                        { DevicePlatform.macOS, new[] { "public.audio" } },
                        { DevicePlatform.iOS, new[] { "public.audio" } },
                        { DevicePlatform.Android, new[] { "audio/*" } }
                    })
            });

            if (result != null)
            {
                SelectedFolderLabel.Text = _selectedFolderPath = (new FileInfo(result.FullPath)).DirectoryName;
                await LoadAudioFilesAsync(_selectedFolderPath);
                ProcessButton.IsEnabled = _audioFiles.Any();
                
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"选择文件夹时出错：{ex.Message}", "确定");
        }
    }

    private async Task LoadAudioFilesAsync(string folderPath)
    {
        var audioExtensions = new[] { ".mp3", ".wav", ".m4a" };
        var files = Directory.GetFiles(folderPath)
            .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLower()));

        foreach (var file in files)
        {
            if (_audioFiles.Any(f => f.FilePath == file))
            {
                continue;
            }
            _audioFiles.Add(new AudioFile
            {
                FilePath = file,
                FileName = Path.GetFileName(file)
            });
        }
    }

    private async void OnSelectModelFileClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".bin" } },
                        { DevicePlatform.macOS, new[] { "bin"} },
                        { DevicePlatform.iOS, new[] { "public.data" } },
                        { DevicePlatform.Android, new[] { "application/octet-stream" } }
                    })
            });

            if (result != null)
            {
                // 验证文件大小
                var fileInfo = new FileInfo(result.FullPath);
                if (fileInfo.Length < 1024 * 1024) // 至少1MB
                {
                    await DisplayAlert("错误", "选择的文件不是有效的Whisper模型文件", "确定");
                    return;
                }

                // 更新UI和状态
                StatusLabel.Text = $"已选择模型文件：{result.FileName}";
                _whisperService.CustomModelFilePath = result.FullPath;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"选择模型文件时出错：{ex.Message}", "确定");
        }
    }


    private async void OnProcessClicked(object sender, EventArgs e)
    {
        if (!_audioFiles.Any())
        {
            await DisplayAlert("提示", "请先添加音频文件", "确定");
            return;
        }

        try
        {
            ProcessButton.IsEnabled = false;
            SelectFolderButton.IsEnabled = false;


            foreach (var audioFile in _audioFiles)
            {
                if (audioFile.Status == "已完成")
                {
                    continue;
                }

                try
                {
                    audioFile.Status = "处理中...";
                    StatusLabel.Text = $"正在处理：{audioFile.FileName}";
                    var subtitles = await _whisperService.ProcessAudioFileAsync(audioFile.FilePath);
                    audioFile.Status = "已完成";
                }
                catch (Exception ex)
                {
                    audioFile.Status = "处理失败";
                    await DisplayAlert("错误", $"处理音频文件 {audioFile.FileName} 时出错：{ex.Message}", "确定");
                }

            }

            StatusLabel.Text = "所有文件处理完成";

        }
        finally
        {
            ProcessButton.IsEnabled = true;
            SelectFolderButton.IsEnabled = true;
        }
    }



    private void OnClearClicked(object sender, EventArgs e)
    {
        _selectedFolderPath = "";
        _audioFiles.Clear();
        SelectedFolderLabel.Text = "未选择文件夹";
        
        ProcessButton.IsEnabled = false;
        
        StatusLabel.Text = "准备就绪";
    }
}

