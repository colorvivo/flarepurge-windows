using System;
using FlarePurge.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace FlarePurge.App.Views;

public sealed partial class AboutView : UserControl
{
    public AboutViewModel ViewModel { get; }

    public event EventHandler? BackRequested;

    public AboutView(AboutViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public bool IsModalChild
    {
        get => HeaderOverline.Visibility == Visibility.Collapsed;
        set => HeaderOverline.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnEscape(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSupportEmailClick(object sender, RoutedEventArgs e)
        => Safe.Fire(XamlRoot, () => Launcher.LaunchUriAsync(new Uri($"mailto:{ViewModel.SupportEmail}")).AsTask());
}
