using ParrotMimicry.PageModels;
using ParrotMimicry.Services;

namespace ParrotMimicry.Pages;

public partial class SettingPage : ContentPage
{
	public SettingPage(IServiceProvider serviceProvider)
	{
		InitializeComponent();
        BindingContext = new SettingsPageModel(serviceProvider.GetRequiredService<DatabaseService>(),
			serviceProvider.GetRequiredService<SubtitleParserService>());
    }
}


