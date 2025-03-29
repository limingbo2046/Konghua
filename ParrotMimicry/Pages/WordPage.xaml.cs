using ParrotMimicry.Services;

namespace ParrotMimicry.Pages;

public partial class WordPage : ContentPage
{
    
    private readonly DatabaseService _databaseService;
    private Models.Word _word;

    public WordPage(IServiceProvider serviceProvider, string word = "")
    {
        InitializeComponent();
        _databaseService = serviceProvider.GetRequiredService<DatabaseService>();
        LoadWord(word);
    }

    private async void LoadWord(string word)
    {
        _word = await _databaseService.GetWordByTextAsync(word);//去数据库查找单词
        if (_word == null)
        {
            // 如果数据库中没有找到单词，提醒用户需要更新单词库
            DisplayAlert("错误", $"在数据库中没找到该单词{word}需要重新整理字幕或编辑单词库！", "确定");
            return;
        }
        lbl_word.Text = _word.Text;
        lbl_phonetic.Text = $"音标：{_word.IPAPhonetic ?? ""} | KK音标：{_word.KKPhonetic ?? ""}";
        lbl_definition.Text = _word.Definition ?? "未找到释义";
    }

    private async void MarkLearned_Clicked(object sender, EventArgs e)
    {
        if (_word != null)
        {
            _word.Familiarity = 10;
            _word.LastReviewTime = DateTime.UtcNow;
            await _databaseService.SaveWordAsync(_word);
            await Navigation.PopModalAsync();
        }
    }


    private async void Button_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}