using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParrotMimicry.Utilities;
using System.Diagnostics;

namespace ParrotMimicry.PageModels;

public partial class ProjectPageModel : ObservableObject
{
    [RelayCommand]
    private async Task Appearing()
    {
        await LoadProjectData();
    }

    private string _rootFolder = string.Empty;
    private List<TreeItem> _rootItems = [];

    public string RootFolder
    {
        get => _rootFolder;
        set => SetProperty(ref _rootFolder, value);
    }

    /// <summary>
    /// 根目录下的所有课
    /// </summary>
    public List<TreeItem> RootItems
    {
        get => _rootItems;
        set => SetProperty(ref _rootItems, value);
    }

    public ProjectPageModel()
    {
        LoadSettings();
    }

    private async Task<TreeItem> FindLastCheckedItemAsync(string rootPath)
    {
        return await Task.Run(() =>
        {
            TreeItem lastCheckedItem = null;
            var rootDir = new DirectoryInfo(rootPath);
            DirectoryInfo lastCourseDir = null;
            DirectoryInfo lastChapterDir = null;
            bool hasCheckedItem = false;

            // 遍历所有二级目录
            foreach (var courseDir in rootDir.GetDirectories())
            {
                // 检查是否有子目录
                var chapterDirs = courseDir.GetDirectories().Where(p => Directory.GetFiles(p.FullName).Count() > 0);
                foreach (var chapterDir in chapterDirs)
                {
                    // 检查视频文件
                    var videoFiles = chapterDir.GetFiles("*.mp4", SearchOption.TopDirectoryOnly);
                    foreach (var videoFile in videoFiles)
                    {
                        if (File.Exists(videoFile.FullName.Replace(".mp4", ".txt")))
                        {
                            hasCheckedItem = true;
                            lastCourseDir = courseDir;
                            lastChapterDir = chapterDir;
                        }
                    }
                }
            }

            // 如果找到了有笔记的视频，构建路径信息
            if (hasCheckedItem && lastCourseDir != null && lastChapterDir != null)
            {
                lastCheckedItem = new TreeItem
                {
                    Name = lastCourseDir.Name,
                    FullPath = lastCourseDir.FullName,
                    IsDirectory = true,
                    Icon = "课",
                    IsExpanded = true,
                    Children = new List<TreeItem>
                    {
                        new TreeItem
                        {
                            Name = lastChapterDir.Name,
                            FullPath = lastChapterDir.FullName,
                            IsDirectory = true,
                            Icon = "章",
                            IsExpanded = true
                        }
                    }
                };
            }

            return lastCheckedItem;
        });
    }

    private async Task ExpandLastCheckedItemAsync()
    {
        var lastCheckedItem = await FindLastCheckedItemAsync(RootFolder);
        if (lastCheckedItem != null)//找到有笔记的视频
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var course_item = RootItems.FirstOrDefault(c=>c.FullPath==lastCheckedItem.FullPath);//课程
                if (course_item != null)
                {
                    ToggleExpand(course_item);
                    var chapter_item = course_item.Children.FirstOrDefault(chapter => chapter.FullPath == lastCheckedItem.Children.FirstOrDefault()?.FullPath);//章
                    if (chapter_item != null)
                    {
                        ToggleExpand(chapter_item);
                    }
                }
            });
        }
    }

    private async Task LoadProjectData()
    {
        if (string.IsNullOrEmpty(RootFolder) || !Directory.Exists(RootFolder))
        {
            await Shell.Current.DisplayAlert("错误", "请先在设置中选择根文件夹", "确定");
            return;
        }

        try
        {
            var rootDir = new DirectoryInfo(RootFolder);
            RootItems = rootDir.GetDirectories().Where(p => Directory.GetDirectories(p.FullName).Count() > 0)
                .Select(dir => new TreeItem
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true,
                    Icon = "课"
                })
                .ToList();

            // 找到最后一个有笔记的项目并展开
           await ExpandLastCheckedItemAsync();
            
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("错误", $"加载文件夹时发生错误：{ex.Message}", "确定");
        }
    }

    [RelayCommand]
    private void ToggleExpand(TreeItem item)
    {
        if (!item.IsDirectory) return;

        item.IsExpanded = !item.IsExpanded;

        if (item.IsExpanded && (item.Children == null || !item.Children.Any()))
        {
            try
            {
                var directory = new DirectoryInfo(item.FullPath);
                var children = new List<TreeItem>();

                // 添加子文件夹
                children.AddRange(directory.GetDirectories().Where(p => Directory.GetFiles(p.FullName).Count() > 0)
                    .Select(dir => new TreeItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsDirectory = true,
                        Icon = "章"
                    }));

                // 只在第二层添加文件
                if (directory.FullName.Count(c => c == Path.DirectorySeparatorChar) ==
                    RootFolder.Count(c => c == Path.DirectorySeparatorChar) + 2)
                {
                    children.AddRange(directory.GetFiles("*.mp4", SearchOption.TopDirectoryOnly)
                        .Select(file => new TreeItem
                        {
                            Name = file.Name,
                            FileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name),
                            FullPath = file.FullName,
                            IsDirectory = false,
                            Icon = File.Exists(file.FullName.Replace(".mp4", ".srt")) ? "幕" : "",//有字幕显示绿色文件图标
                            IsChecked = File.Exists(file.FullName.Replace(".mp4", ".txt"))//有笔记则勾选
                        }));
                }

                item.Children = children;
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Shell.Current.DisplayAlert("错误", $"加载文件夹内容时发生错误：{ex.Message}", "确定");
                });
            }
        }
    }
    private void LoadSettings()
    {
        RootFolder = Preferences.Default.Get("root_folder", string.Empty);
    }

    public void UpdateTreeItem(string videoPath)
    {
        // 检查笔记文件是否存在
        var noteExists = File.Exists(videoPath.Replace(".mp4", ".txt"));

        // 遍历所有课程
        foreach (var courseItem in RootItems)
        {
            if (!courseItem.IsExpanded)
            {
                ToggleExpand(courseItem);
            }

            // 遍历章节
            foreach (var chapterItem in courseItem.Children)
            {
                if (!chapterItem.IsExpanded)
                {
                    ToggleExpand(chapterItem);
                }

                // 查找视频文件
                var videoItem = chapterItem.Children.FirstOrDefault(v => v.FullPath == videoPath);
                if (videoItem != null)
                {
                    videoItem.IsChecked = noteExists;
                    return;
                }
            }
        }
    }
}


public partial class TreeItem : ObservableObject
{

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _fileNameWithoutExtension = string.Empty;
    public string FileNameWithoutExtension
    {
        get => _fileNameWithoutExtension;
        set => SetProperty(ref _fileNameWithoutExtension, value);
    }

    private string _fullPath = string.Empty;
    public string FullPath
    {
        get => _fullPath;
        set => SetProperty(ref _fullPath, value);
    }

    private bool _isDirectory;
    public bool IsDirectory
    {
        get => _isDirectory;
        set => SetProperty(ref _isDirectory, value);
    }

    private string _icon = string.Empty;
    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    private List<TreeItem> _children = new();
    public List<TreeItem> Children
    {
        get => _children;
        set
        {
            var sorted = value
               .OrderBy(x => x.IsDirectory ? 0 : 1) // 文件夹在前
               .ThenBy(x => x.Name, new NaturalStringComparer()) // 自然排序
               .ToList();
            SetProperty(ref _children, sorted);
        }
    }
}


public class ProgressInfo
{
    public int ProcessedCount { get; set; }
    public int TotalFiles { get; set; }
}

