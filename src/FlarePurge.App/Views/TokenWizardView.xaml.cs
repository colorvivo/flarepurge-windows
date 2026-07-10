using System;
using FlarePurge.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FlarePurge.App.Views;

public sealed partial class TokenWizardView : UserControl
{
    public TokenWizardViewModel ViewModel { get; }

    public event EventHandler? CancelRequested;

    public TokenWizardView(TokenWizardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public bool CanCancel
    {
        get => BackButton.Visibility == Visibility.Visible;
        set => BackButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private void OnBackClick(object sender, RoutedEventArgs e)
        => CancelRequested?.Invoke(this, EventArgs.Empty);

    // D1: PasswordBox.Password no es bindable TwoWay; se propaga al VM aquí.
    private void OnTokenChanged(object sender, RoutedEventArgs e)
        => ViewModel.Token = TokenBox.Password;
}
