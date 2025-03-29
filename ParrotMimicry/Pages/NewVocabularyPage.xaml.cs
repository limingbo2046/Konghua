using ParrotMimicry.Models;
using ParrotMimicry.Services;

namespace ParrotMimicry.Pages;

public partial class NewVocabularyPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private List<Word> _words;
    private int _currentIndex;
    private Word _currentWord;
    private List<Subtitle> _currentSubtitles;
    

    public NewVocabularyPage(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        LoadWordsAsync();
    }

    private async void LoadWordsAsync()
    {
        // 获取熟悉度大于0的单词，按熟悉度倒序排列
        _words = (await _databaseService.GetWordsAsync())
            .Where(w => w.Familiarity > 0)
            .OrderByDescending(w => w.Familiarity)
            .ToList();

        if (_words.Any())
        {
            _currentIndex = 0;
            await ShowCurrentWordAsync();
        }
        else
        {
            await DisplayAlert("提示", "没有需要复习的单词", "确定");
            await Navigation.PopAsync();
        }
    }

    private async Task ShowCurrentWordAsync()
    {
        _currentWord = _words[_currentIndex];
        _currentSubtitles = await _databaseService.GetSubtitlesByWordIdAsync(_currentWord.Id);

        if (_currentSubtitles.Any())
        {
            // 随机选择一条字幕
            var subtitle = _currentSubtitles[new Random().Next(_currentSubtitles.Count)];
            var text = subtitle.Text;
            
            // 查找目标单词的位置
            int wordIndex = text.IndexOf(_currentWord.Text, StringComparison.OrdinalIgnoreCase);
            if (wordIndex >= 0)
            {
                // 分割字幕文本
                string prefix = text.Substring(0, wordIndex);
                string suffix = text.Substring(wordIndex + _currentWord.Text.Length);

                // 设置各个部分的文本
                SubtitlePrefix.Text = prefix;
                SubtitleEntry.Text = string.Empty;
                SubtitleSuffix.Text = suffix;
                

                // 重置答案区域
                AnswerSection.IsVisible = false;
                ShowAnswerButton.IsVisible = true;
            }
        }
    }



    private void OnShowAnswerClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(SubtitleEntry.Text))
        {
            SubtitleEntry.TextColor = Colors.Black;
            return;
        }

        if (SubtitleEntry.Text.Trim().Equals(_currentWord.Text, StringComparison.OrdinalIgnoreCase))
        {
            SubtitleEntry.TextColor = Colors.Green;
        }
        else
        {
            SubtitleEntry.TextColor = Colors.Red;
        }

        WordLabel.Text = _currentWord.Text;
        PhoneticLabel.Text = _currentWord.KKPhonetic ?? _currentWord.IPAPhonetic;
        DefinitionLabel.Text = _currentWord.Definition;

        // 最多显示5条字幕
        var subtitles = _currentSubtitles.Take(5).Select(s => s.Text).ToList();
        SubtitlesCollection.ItemsSource = subtitles;

        AnswerSection.IsVisible = true;
        ShowAnswerButton.IsVisible = false;
    }

    private async void OnMoreFamiliarClicked(object sender, EventArgs e)
    {
        if (_currentWord != null)
        {
            _currentWord.Familiarity = Math.Max(0, _currentWord.Familiarity - 1);
            _currentWord.LastReviewTime = DateTime.UtcNow;
            await _databaseService.SaveWordAsync(_currentWord);
            await ShowNextWordAsync();
        }
    }

    private async void OnKeepAsIsClicked(object sender, EventArgs e)
    {
        if (_currentWord != null)
        {
            _currentWord.LastReviewTime = DateTime.UtcNow;
            await _databaseService.SaveWordAsync(_currentWord);
            await ShowNextWordAsync();
        }
    }

    private async Task ShowNextWordAsync()
    {
        _currentIndex++;
        if (_currentIndex < _words.Count)
        {
            await ShowCurrentWordAsync();
        }
        else
        {
            await DisplayAlert("提示", "所有单词已复习完成", "确定");
            await Navigation.PopAsync();
        }
    }
}