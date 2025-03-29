using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using ParrotMimicry.Utilities;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace ParrotMimicry.Pages
{
    public partial class PracticePage : ContentPage
    {
        private string _videoPath = string.Empty;
        public string VideoPath
        {
            get => _videoPath;
            set => _videoPath = value;
        }
        public PracticePage(string videoPath)
        {
            InitializeComponent();
            _videoPath = videoPath;
            subtitlesList.ItemsSource = Subtitles;
            LoadVideo(videoPath);
        }

        private ObservableCollection<SubtitleItem> Subtitles = new();//���ڴ����Ļ
        private void LoadVideo(string videoPath)
        {
            mediaElement.Source = videoPath;
            var subtitleFile = videoPath.Replace(".mp4", ".srt");//Ĭ����Ļ�ļ�����Ƶ�ļ�ͬ������׺Ϊ.srt
            LoadSubtitles(subtitleFile);
            mediaElement.Play();
        }

        private void LoadSubtitles(string filePath)
        {
            Subtitles.Clear();//�����Ļ�б�
            string srtContent = File.ReadAllText(filePath);
            _selectedSubtitleIndex = -1;//����ѡ�е���Ļ����
            using (StringReader reader = new StringReader(srtContent))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    // ������Ļ����
                    if (!int.TryParse(line, out int index)) continue;

                    // ����ʱ���
                    string? timeLine = reader.ReadLine();
                    if (timeLine == null) continue;

                    string[] times = timeLine.Split(" --> ");
                    if (times.Length != 2) continue;

                    TimeSpan start = TimeSpan.ParseExact(times[0].Replace(',', '.'), @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
                    TimeSpan end = TimeSpan.ParseExact(times[1].Replace(',', '.'), @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

                    // ������Ļ���ݣ������Ƕ��У�
                    string text = "";
                    while (!string.IsNullOrWhiteSpace(line = reader.ReadLine()))
                    {
                        if (line == null) break;
                        text += " " + line;
                    }
                    if (_selectedSubtitleIndex == -1)//Ĭ��ѡ�е�һ����Ļ
                    {
                        _selectedSubtitleIndex = index;
                    }
                    Subtitles.Add(new SubtitleItem
                    {
                        Index = index,
                        Start = start,
                        End = end,
                        Text = text.Trim()
                    });
                }
            }
            if (_selectedSubtitleIndex - 1 >= 0)
            {
                Subtitles[_selectedSubtitleIndex - 1].IsActive = true;
                ScrollToSubtitle(_selectedSubtitleIndex - 1);
            }
        }

        private int _selectedSubtitleIndex = -1;//ѡ�е���Ļ����
        private void MediaElement_PositionChanged(object? sender, MediaPositionChangedEventArgs e)
        {
            var currentTime = e.Position;
            if (_selectedSubtitleIndex != -1)
            {
                var sub = Subtitles[_selectedSubtitleIndex];
                if (currentTime > sub.End)
                {
                    sub.IsActive = false;
                    _selectedSubtitleIndex = FindNextSubtitleIndex(currentTime);
                    if (_selectedSubtitleIndex != -1)
                    {
                        sub = Subtitles[_selectedSubtitleIndex];
                        if (currentTime >= sub.Start)
                        {
                            sub.IsActive = true;
                            ScrollToSubtitle(_selectedSubtitleIndex);
                        }
                    }
                }
            }
            else
            {
                _selectedSubtitleIndex = FindNextSubtitleIndex(currentTime);
                if (_selectedSubtitleIndex != -1)
                {
                    var sub = Subtitles[_selectedSubtitleIndex];
                    if (currentTime >= sub.Start)
                    {
                        sub.IsActive = true;
                        ScrollToSubtitle(_selectedSubtitleIndex);
                    }
                }
            }
        }

        // ���ֲ���
        private int FindNextSubtitleIndex(TimeSpan currentTime)
        {
            int left = 0;
            int right = Subtitles.Count - 1;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                var sub = Subtitles[mid];

                if (currentTime < sub.Start)
                {
                    right = mid - 1;
                }
                else if (currentTime > sub.End)
                {
                    left = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            return -1; // δ�ҵ�ƥ�����Ļ
        }

        private bool isUserScrolling = false;
        private void ScrollToSubtitle(int index)
        {
            if (isUserScrolling)
            {
                return;
            }
            subtitlesList.ScrollTo(index, position: ScrollToPosition.Center, animate: true);
        }

        private async void PlayButton_Clicked(object sender, EventArgs e)
        {
            var path = await CommunityToolkit.Maui.Storage.FolderPicker.Default.PickAsync();
            Debug.WriteLine(path);
        }

        private void PlayPostion_Clicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is SubtitleItem subtitle)
            {
                mediaElement.SeekTo(subtitle.Start);
                ScrollToSubtitle(subtitle.Index);
            }
        }

        private CancellationTokenSource? scrollCts; // ����ȡ��֮ǰ�ļ������
        private async void subtitlesList_Scrolled(object sender, ItemsViewScrolledEventArgs e)
        {
            // ȡ����һ��δ��ɵ��ӳ�����
            scrollCts?.Cancel();
            scrollCts = new CancellationTokenSource();
            isUserScrolling = true;

            try
            {
                await Task.Delay(3000, scrollCts.Token);
                isUserScrolling = false;
                Debug.WriteLine("�û���ֹͣ�������������ִ�й����߼�");
            }
            catch (TaskCanceledException)
            {
                // ����ȡ����˵���û�����������ʲô������
            }
        }

        private void btnReturn_Clicked(object sender, EventArgs e)
        {
            Navigation.PopModalAsync();
        }

        private async void Word_Clicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is WordItem word)
            {
                Debug.WriteLine(word.Text);
                mediaElement.Pause();
                var services = Application.Current.Handler.MauiContext.Services;
                var wordPage = new WordPage(services, word.Text);
                wordPage.Disappearing += WordPage_Disappearing;
                await Navigation.PushModalAsync(wordPage);
            }
        }

        private void WordPage_Disappearing(object sender, EventArgs e)
        {
            if (sender is WordPage wordPage)
            {
                wordPage.Disappearing -= WordPage_Disappearing;
                mediaElement.Play();
            }
        }
    }

    public class SubtitleItem : ObservableObject
    {
        public int Index { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }

        private string _text = string.Empty;
        public string Text
        {
            get => _text;
            set
            {
                SetProperty(ref _text, value);
                words.Clear();
                foreach (var w in value.ExtractWords().ToArray())
                {
                    Words.Add(new WordItem { Text = w });
                }
            }
        }

        private ObservableCollection<WordItem> words = new ObservableCollection<WordItem>();
        public ObservableCollection<WordItem> Words
        {
            get => words;
            set => SetProperty(ref words, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }
    }

    public class WordItem : ObservableObject
    {
        private string _text = string.Empty;
        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }
    }
}