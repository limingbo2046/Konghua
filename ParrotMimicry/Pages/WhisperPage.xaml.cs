using CommunityToolkit.Maui.Storage;
using ParrotMimicry.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ParrotMimicry.Pages;

public partial class WhisperPage : ContentPage
{
    private string _wavsPath;//单独一个音频文件的路径
    private ObservableCollection<AudioFile> _audioFiles = new ObservableCollection<AudioFile>();
    private WhisperService _whisperService;
    
    public WhisperPage(IServiceProvider serviceProvider, string wavsPath = "")
    {
        InitializeComponent();
        _wavsPath = wavsPath;
        AudioFilesList.ItemsSource = _audioFiles;
        _whisperService = serviceProvider.GetRequiredService<WhisperService>();
        if (!string.IsNullOrWhiteSpace(_wavsPath))
        {
            _audioFiles.Add(new AudioFile
            {
                FilePath = _wavsPath,
                FileName = Path.GetFileName(_wavsPath)
            });
            ProcessButton.IsEnabled = _audioFiles.Any();
        }
    }

    private void OnModelSelected(object sender, EventArgs e)
    {
        if (_whisperService == null)
        {
            return;
        }
        var selectedIndex = (sender as Picker)?.SelectedIndex;
        _whisperService.CurrentModel = (WhisperModelType)selectedIndex.GetValueOrDefault();
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
    private void LoadAudioFilesAsync(string folderPath)
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

    private async void OnSelectFolderClicked(object sender, EventArgs e)
    {

        string RootFolder = string.Empty;
        try
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var result = await FilePicker.PickAsync();
                if (result != null)
                {
                    RootFolder = Path.GetDirectoryName(result.FullPath);
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
                }
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("错误", $"选择文件夹时发生错误：{ex.Message}", "确定");
        }




       

            if (!string.IsNullOrWhiteSpace(RootFolder))
            {
                SelectedFolderLabel.Text = RootFolder;
                LoadAudioFilesAsync(RootFolder);
                ProcessButton.IsEnabled = _audioFiles.Any();

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

    public class AudioFile : INotifyPropertyChanged
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }

        private string? _Status { get; set; } = "待处理";
        public string? Status
        {
            get => _Status;
            set
            {
                if (_Status != value)
                {
                    _Status = value;
                    OnPropertyChanged(nameof(Status)); // 通知 UI 更新
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}