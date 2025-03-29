namespace MauiApp1;

public partial class NewPage1 : ContentPage
{
	public NewPage1(Class1 class1)
	{
		InitializeComponent();
		BindingContext = class1;
	}
}