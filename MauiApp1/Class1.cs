using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace MauiApp1
{
    public partial class Class1 : ObservableObject
    {

        [RelayCommand]
        private void myfun()
        {
            Debug.WriteLine("ddddddddddddddddddd");
        }
    }
}
