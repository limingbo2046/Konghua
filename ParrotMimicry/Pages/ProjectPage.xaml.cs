

using CommunityToolkit.Maui.Views;
using ParrotMimicry.PageModels;
using ParrotMimicry.Services;
using System.Threading.Tasks;

namespace ParrotMimicry.Pages;

public partial class ProjectPage : ContentPage
{
    public ProjectPage()
    {
        InitializeComponent();
        viewModel.AppearingCommand.Execute(this);
    }

    private void PlayVideo_Clicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is TreeItem treeItem)
        {
            var playVidePage = new PlayVideoPage(treeItem.FullPath);
            playVidePage.Disappearing += PlayVidePage_Disappearing;
            Navigation.PushModalAsync(playVidePage);
        }
    }

    private void PlayVidePage_Disappearing(object? sender, EventArgs e)
    {
        if (sender is PlayVideoPage playVideoPage)
        {
            playVideoPage.Disappearing -= PlayVidePage_Disappearing;
            this.viewModel.UpdateTreeItem(playVideoPage.VideoPath);
        }
    }

    private async void SearchSubtitle_Clicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is TreeItem item)
        {
            
            var searchPage = new SearchSubtitlePage(item.FullPath);
            await Navigation.PushModalAsync(searchPage);
        }
    }

    private async void OpenNote_Clicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is TreeItem item)
        {
            await Navigation.PushModalAsync(new NotePage(item.FullPath));
        }
    }

    private void Practice_Record_Clicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is TreeItem treeItem)
        {
            var practicePage = new PracticePage(treeItem.FullPath);
            
            Navigation.PushModalAsync(practicePage);

        }
    }
}