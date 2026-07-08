using System;
using System.Linq;
using FlarePurge.App.Localization;
using FlarePurge.App.ViewModels;
using FlarePurge.Core.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FlarePurge.App.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsViewModel ViewModel { get; }

    public event EventHandler? BackRequested;
    public event EventHandler? AddAccountRequested;
    public event EventHandler? AccountsChanged;

    public SettingsView(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += (_, _) => RefreshAccounts();
    }

    public bool IsModalChild
    {
        get => HeaderPanel.Visibility == Visibility.Collapsed;
        set => HeaderPanel.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnEscape(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshAccounts()
    {
        AccountsList.Children.Clear();
        var store = App.Services.GetRequiredService<IAccountStore>();
        var activeId = store.GetActiveAccountId();
        var accounts = store.LoadAccounts().OrderBy(a => a.Label, StringComparer.OrdinalIgnoreCase).ToArray();
        if (accounts.Length == 0)
        {
            AccountsList.Children.Add(new TextBlock
            {
                Text = L.S("empty_zones_title"),
                Style = (Style)Application.Current.Resources["FPCaptionStyle"],
                Margin = new Thickness(10),
            });
            return;
        }

        foreach (var account in accounts)
        {
            AccountsList.Children.Add(BuildAccountRow(account, activeId == account.Id));
        }
    }

    private Grid BuildAccountRow(StoredAccount account, bool isActive)
    {
        var grid = new Grid
        {
            Padding = new Thickness(10, 8, 10, 8),
            ColumnSpacing = 12,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var indicator = new Border
        {
            Width = 3,
            Height = 28,
            Background = ThemeBrush("FPAccentBrush"),
            CornerRadius = new CornerRadius(2),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = isActive ? Visibility.Visible : Visibility.Collapsed,
        };
        Grid.SetColumn(indicator, 0);

        var stack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = account.Label,
            Style = (Style)Application.Current.Resources["FPBodyLargeStyle"],
        });
        stack.Children.Add(new TextBlock
        {
            Text = account.TokenKeychainAccount,
            Style = (Style)Application.Current.Resources["FPMonoSmallStyle"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
        });
        Grid.SetColumn(stack, 1);

        var editBtn = new Button
        {
            Tag = account.Id,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new FontIcon { Glyph = "", FontSize = 14 }, // pencil
        };
        ToolTipService.SetToolTip(editBtn, L.S("editLabel_title"));
        AutomationProperties.SetName(editBtn, L.S("editLabel_title"));
        editBtn.Click += OnEditLabelClick;
        Grid.SetColumn(editBtn, 2);

        var signOutBtn = new Button
        {
            Content = L.S("accounts_remove"),
            Tag = account.Id,
            VerticalAlignment = VerticalAlignment.Center,
        };
        signOutBtn.Click += OnSignOutClick;
        Grid.SetColumn(signOutBtn, 3);

        grid.Children.Add(indicator);
        grid.Children.Add(stack);
        grid.Children.Add(editBtn);
        grid.Children.Add(signOutBtn);
        return grid;
    }

    private void OnEditLabelClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;

        var store = App.Services.GetRequiredService<IAccountStore>();
        var target = store.LoadAccounts().FirstOrDefault(a => a.Id == id);
        if (target is null) return;

        var input = new TextBox
        {
            Text = target.Label,
            PlaceholderText = L.S("accounts_labelPlaceholder"),
            MaxLength = 60,
            MinWidth = 260,
        };

        var saveBtn = new Button
        {
            Content = L.S("action_save"),
            Style = Application.Current.Resources["AccentButtonStyle"] as Style,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var flyout = new Flyout
        {
            Content = new StackPanel
            {
                Spacing = 8,
                MinWidth = 280,
                Children =
                {
                    new TextBlock
                    {
                        Text = L.S("editLabel_title"),
                        Style = (Style)Application.Current.Resources["FPCaptionStyle"],
                    },
                    input,
                    saveBtn,
                },
            },
        };

        void Commit()
        {
            var newLabel = input.Text.Trim();
            flyout.Hide();
            if (store.RenameAccount(id, newLabel))
            {
                RefreshAccounts();
                AccountsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        saveBtn.Click += (_, _) => Commit();
        input.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                Commit();
            }
        };

        input.Loaded += (_, _) => { input.SelectAll(); input.Focus(FocusState.Programmatic); };

        flyout.ShowAt(btn);
    }

    private Brush? ThemeBrush(string key)
    {
        var themeKey = ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        if (Application.Current.Resources.ThemeDictionaries.TryGetValue(themeKey, out var dict)
            && dict is ResourceDictionary rd
            && rd.TryGetValue(key, out var value))
        {
            return value as Brush;
        }
        return null;
    }

    private void OnSignOutClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;

        var store = App.Services.GetRequiredService<IAccountStore>();
        var keychain = App.Services.GetRequiredService<IKeychainProvider>();
        var accounts = store.LoadAccounts();
        var target = accounts.FirstOrDefault(a => a.Id == id);
        if (target is null) return;

        keychain.Delete(target.TokenKeychainAccount);
        App.Services.GetRequiredService<IZoneCacheStore>().Delete(id);
        var remaining = accounts.Where(a => a.Id != id).ToArray();
        store.SaveAccounts(remaining);

        if (string.Equals(id, store.GetActiveAccountId(), StringComparison.Ordinal))
        {
            store.SetActiveAccountId(remaining.Length > 0 ? remaining[0].Id : null);
        }

        RefreshAccounts();
        AccountsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnAddAccountClick(object sender, RoutedEventArgs e)
        => AddAccountRequested?.Invoke(this, EventArgs.Empty);
}
