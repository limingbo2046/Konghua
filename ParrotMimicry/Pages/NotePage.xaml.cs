using Microsoft.Maui.Controls;
using System.IO;

namespace ParrotMimicry.Pages;

public partial class NotePage : ContentPage
{
    private string _noteFilePath;

    public NotePage(string videoFilePath)
    {
        InitializeComponent();
        _noteFilePath = Path.ChangeExtension(videoFilePath, ".txt");
        LoadNoteContent();
    }

    private void LoadNoteContent()
    {
        if (File.Exists(_noteFilePath))
        {
            NoteEditor.Text = File.ReadAllText(_noteFilePath);
        }
    }

    private async void SaveNote_Clicked(object sender, EventArgs e)
    {
        try
        {
            File.WriteAllText(_noteFilePath, NoteEditor.Text);
            await DisplayAlert("成功", "笔记已保存", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"保存笔记时出错：{ex.Message}", "确定");
        }
    }

    private async void Back_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}