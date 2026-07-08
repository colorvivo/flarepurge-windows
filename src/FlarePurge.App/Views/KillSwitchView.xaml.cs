using Microsoft.UI.Xaml.Controls;

namespace FlarePurge.App.Views;

public sealed partial class KillSwitchView : UserControl
{
    public KillSwitchView(string? message)
    {
        InitializeComponent();
        MessageText.Text = string.IsNullOrWhiteSpace(message)
            ? "Vuelve a abrir la app más tarde. Publicaremos más información en flarepurge.com si es necesario."
            : message!;
    }
}
