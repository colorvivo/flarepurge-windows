using CommunityToolkit.Mvvm.ComponentModel;

namespace FlarePurge.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "Scaffold OK — Sprint 1 done. Sprint 2 starts here.";
}
