using CommunityToolkit.Maui.Storage;
using ParrotMimicry.Models;
using ParrotMimicry.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ParrotMimicry.Pages;

public partial class Video2WavPage : ContentPage
{
    private ObservableCollection<VideoFile> _videoFiles = new ObservableCollection<VideoFile>();
    private VideoToAudioService _videoToAudioService;
    private CancellationTokenSource _cancellationTokenSource;
    private string _selectedFolderPath;
    private string _outputDirectoryPath;

    public Video2WavPage(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        VideoFilesList.ItemsSource = _videoFiles;
        _videoToAudioService = serviceProvider.GetRequiredService<VideoToAudioService>();
    }

    private async void OnSelectFilesClicked(object sender, EventArgs e)
    {
        try
        {
            var options = new PickOptions
            {
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".mp4", ".avi", ".mkv", ".mov", ".flv" } },
                        { DevicePlatform.macOS, new[] { "mp4", "avi", "mkv", "mov", "flv" } },
                        { DevicePlatform.iOS, new[] { "public.mpeg-4", "public.avi", "public.movie" } },
                        { DevicePlatform.Android, new[] { "video/*" } }
                    }),
                PickerTitle = "选择视频文件"
            };

            var results = await FilePicker.PickMultipleAsync(options);
            if (results != null && results.Any())
            {
                foreach (var result in results)
                {
                    if (!_videoFiles.Any(f => f.FilePath == result.FullPath))
                    {
                        _videoFiles.Add(new VideoFile
                        {
                            FilePath = result.FullPath,
                            FileName = result.FileName
                        });
                    }
                }

                _selectedFolderPath = Path.GetDirectoryName(results.First().FullPath);
                SelectedPathLabel.Text = $"已选择 {_videoFiles.Count} 个文件";
                ConvertButton.IsEnabled = _videoFiles.Any();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"选择文件时出错：{ex.Message}", "确定");
        }
    }

    private async void OnSelectFolderClicked(object sender, EventArgs e)
    {
        try
        {
            string folderPath = string.Empty;

            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                // 在Android上，通过选择一个文件来获取文件夹路径
                var result = await FilePicker.PickAsync();
                if (result != null)
                {
                    folderPath = Path.GetDirectoryName(result.FullPath);
                }
            }
            else
            {
                // 在其他平台上使用文件夹选择器
                var folder = await FolderPicker.Default.PickAsync();
                if (folder.Folder != null)
                {
                    folderPath = folder.Folder.Path;
                    if (DeviceInfo.Platform == DevicePlatform.WinUI)
                    {
                        // 在Windows平台上，确保路径格式正确
                        folderPath = Path.GetFullPath(folderPath);
                    }
                }
            }

            if (!string.IsNullOrEmpty(folderPath))
            {
                _selectedFolderPath = folderPath;
                SelectedPathLabel.Text = folderPath;
                LoadVideoFilesFromFolder(folderPath);
                ConvertButton.IsEnabled = _videoFiles.Any();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"选择文件夹时出错：{ex.Message}", "确定");
        }
    }

    private void LoadVideoFilesFromFolder(string folderPath)
    {
        var videoExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".flv" };
        var files = Directory.GetFiles(folderPath)
            .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()));

        foreach (var file in files)
        {
            if (_videoFiles.Any(f => f.FilePath == file))
            {
                continue;
            }

            _videoFiles.Add(new VideoFile
            {
                FilePath = file,
                FileName = Path.GetFileName(file)
            });
        }
    }

    private async void OnBrowseOutputClicked(object sender, EventArgs e)
    {
        try
        {
            var folder = await FolderPicker.Default.PickAsync();
            if (folder.Folder != null)
            {
                _outputDirectoryPath = folder.Folder.Path;
                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    // 在Windows平台上，确保路径格式正确
                    _outputDirectoryPath = Path.GetFullPath(_outputDirectoryPath);
                }
                OutputDirectoryEntry.Text = _outputDirectoryPath;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"选择输出目录时出错：{ex.Message}", "确定");
        }
    }

    private void OnRemoveFileClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is VideoFile videoFile)
        {
            _videoFiles.Remove(videoFile);
            ConvertButton.IsEnabled = _videoFiles.Any();
            SelectedPathLabel.Text = $"已选择 {_videoFiles.Count} 个文件";
        }
    }

    private async void OnConvertClicked(object sender, EventArgs e)
    {
        if (!_videoFiles.Any())
        {
            await DisplayAlert("提示", "请先添加视频文件", "确定");
            return;
        }

        try
        {
            // 禁用按钮，防止重复操作
            ConvertButton.IsEnabled = false;
            SelectFilesButton.IsEnabled = false;
            SelectFolderButton.IsEnabled = false;
            BrowseOutputButton.IsEnabled = false;
            CancelButton.IsEnabled = true;

            // 创建取消令牌
            _cancellationTokenSource = new CancellationTokenSource();

            // 获取输出目录
            string outputDirectory = string.IsNullOrEmpty(OutputDirectoryEntry.Text) ? null : OutputDirectoryEntry.Text;

            // 创建进度报告
            var progress = new Progress<double>(p =>
            {
                TotalProgressBar.Progress = p;
                StatusLabel.Text = $"转换进度：{p:P0}";
            });

            // 开始批量转换
            StatusLabel.Text = "开始转换...";
            var results = await _videoToAudioService.BatchConvertVideoToAudioAsync(
                _videoFiles.ToList(),
                outputDirectory,
                progress,
                _cancellationTokenSource.Token);

            // 转换完成
            StatusLabel.Text = $"转换完成，共 {results.Count} 个文件";
            await DisplayAlert("完成", $"成功转换 {results.Count} 个视频文件为音频文件", "确定");
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "转换已取消";
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"转换过程中出错：{ex.Message}", "确定");
            StatusLabel.Text = "转换失败";
        }
        finally
        {
            // 恢复按钮状态
            ConvertButton.IsEnabled = _videoFiles.Any();
            SelectFilesButton.IsEnabled = true;
            SelectFolderButton.IsEnabled = true;
            BrowseOutputButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            TotalProgressBar.Progress = 0;

            // 释放取消令牌
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        CancelButton.IsEnabled = false;
        StatusLabel.Text = "正在取消...";
    }
}