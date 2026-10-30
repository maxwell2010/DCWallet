using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

namespace DCWallet;
public sealed partial class MainWindow
{
    private Grid shell = null!;
    private Border sidebar = null!;
    private StackPanel target = null!;
    private WalletPreferences preferences = new();
    private readonly Dictionary<string, Button> navigation = new();
    private TextBlock connection = null!;
    private double pageWidth = 640;
    private TrayIcon? tray;
    private bool exiting;
    private Button settingsReturn = null!;
    private bool settingsPageActive;
    private void InitializeShell()
    {
        preferences = WalletPreferences.Load(dataDirectory); ApplyTheme();
        Title = "DC WALLET"; Width = 1100; Height = 720; MinWidth = 900; MinHeight = 680;
        Background = UiPalette.Canvas; Foreground = UiPalette.Ink; FontFamily = new FontFamily("Segoe UI, DejaVu Sans, sans-serif"); FontSize = 14;
        using (var source = AssetLoader.Open(new Uri("avares://DCWallet/Assets/icon.png"))) Icon = new WindowIcon(source);
        AddControlStyles();
        shell = new Grid { ColumnDefinitions = new ColumnDefinitions("236,*"), RowDefinitions = new RowDefinitions("*,Auto") };
        var sideGrid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        sidebar = new Border { Child = sideGrid, Background = UiPalette.Sidebar, BorderBrush = UiPalette.Line, BorderThickness = new Thickness(0, 0, 1, 0) };
        Grid.SetRowSpan(sidebar, 2); shell.Children.Add(sidebar);
        var heading = new StackPanel { Spacing = 8, Margin = new Thickness(18, 20, 18, 8) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        brand.Children.Add(Logo(32)); var brandTitle = Text("DC WALLET", 16); brandTitle.FontWeight = FontWeight.SemiBold; brandTitle.VerticalAlignment = VerticalAlignment.Center; brand.Children.Add(brandTitle); heading.Children.Add(brand);
        heading.Children.Add(Text("Счет", 12, Muted));
        var choose = new ComboBox { ItemsSource = Enumerable.Range(1, 10).Select(i => i == 1 ? preferences.AccountName : "Счет " + i).ToArray(), SelectedIndex = accountIndex, HorizontalAlignment = HorizontalAlignment.Stretch, Name = "AccountSelector" };
        choose.SelectionChanged += (_, _) => { if (choose.SelectedIndex >= 0 && accountIndex != choose.SelectedIndex) { accountIndex = choose.SelectedIndex; Lock(); } };
        heading.Children.Add(choose); account.FontSize = 11;
        var copyAddress = new Button { Name = "WalletAddressCopy", Content = account, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch };
        copyAddress.Click += async (_, _) => { if (signer is null && watchAddress is null) return; if (Clipboard is not null) { await Clipboard.SetTextAsync(Address); status.Text = "Адрес скопирован"; } };
        heading.Children.Add(copyAddress); sideGrid.Children.Add(heading);
        var menuScroll = new ScrollViewer { Content = nav }; Grid.SetRow(menuScroll, 1); sideGrid.Children.Add(menuScroll);
        AddNav("Обзор", "assets", ShowAssets); AddNav("Отправить", "send", ShowSend); AddNav("Обмен", "swap", ShowSwap);
        AddNav("Мост", "export", ShowBridge); AddNav("Стейкинг", "stake", ShowStaking);
        AddNav("Токены", "create", ShowTokens); AddNav("NFT", "assets", ShowNfts);
        AddNav("Чеки", "export", ShowChecks);
        AddNav("Общий счет", "shield", ShowSafe); AddNav("История", "history", ShowHistory);
        AddNav("Счета", "restore", ShowAccounts); AddNav("Настройки", "settings", ShowSettings);
        var foot = new StackPanel { Spacing = 10, Margin = new Thickness(12, 8, 12, 14) };
        connection = Text("Decimal RPC\nЕще не проверено", 12, Muted);
        foot.Children.Add(new Border { Child = connection, Background = UiPalette.Card, BorderBrush = UiPalette.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14) });
        var lockButton = MakeButton("Заблокировать", () => { Lock(); return Task.CompletedTask; }); lockButton.Classes.Add("nav"); lockButton.HorizontalAlignment = HorizontalAlignment.Stretch; foot.Children.Add(lockButton);
        Grid.SetRow(foot, 2); sideGrid.Children.Add(foot);
        var scroll = new ScrollViewer { Name = "MainScroll", Content = page, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        page.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumn(scroll, 1); shell.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 4, Margin = new Thickness(32, 8, 32, 16) };
        settingsReturn = MakeButton("Вернуться ко входу", () => { ReturnFromSettings(); return Task.CompletedTask; }); settingsReturn.IsVisible = false; footer.Children.Add(settingsReturn);
        status.Margin = new Thickness(0); status.FontSize = 12; footer.Children.Add(status);
        Grid.SetColumn(footer, 1); Grid.SetRow(footer, 1); shell.Children.Add(footer); Content = shell;
        SizeChanged += (_, _) => SizePage();
        AddHandler(PointerPressedEvent, (_, _) => lastInput = DateTimeOffset.UtcNow, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, (_, _) => lastInput = DateTimeOffset.UtcNow, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Escape && settingsPageActive && !busy) { ReturnFromSettings(); e.Handled = true; } });
        timer.Tick += (_, _) => { if (signer is not null && DateTimeOffset.UtcNow - lastInput > TimeSpan.FromMinutes(preferences.AutoLockMinutes)) Lock(); else if ((signer is not null || watchAddress is not null) && automaticLoading && !dataLoading && DateTimeOffset.UtcNow - dataReadAt > TimeSpan.FromMinutes(1)) ScheduleDataLoad(); };
        timer.Start();
        InitializeTray();
        Closing += (_, e) => { if (!exiting && preferences.CloseToTray && tray is not null) { e.Cancel = true; Hide(); } };
        PropertyChanged += (_, e) => { if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized && preferences.MinimizeToTray && tray is not null) Hide(); };
        if (PlatformSettings is not null) PlatformSettings.ColorValuesChanged += OnSystemColorsChanged;
        Closed += (_, _) => { CancelDataLoads(); signer?.Dispose(); timer.Stop(); tray?.Dispose(); if (PlatformSettings is not null) PlatformSettings.ColorValuesChanged -= OnSystemColorsChanged; };
    }
    private void InitializeTray()
    {
        if (Environment.GetEnvironmentVariable("DC_HEADLESS") == "1") return;
        try
        {
            tray = new TrayIcon { Icon = Icon, ToolTipText = "DC WALLET" };
            var menu = new NativeMenu(); var open = new NativeMenuItem("Открыть"); open.Click += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); }; menu.Add(open);
            var close = new NativeMenuItem("Заблокировать"); close.Click += (_, _) => Lock(); menu.Add(close);
            var exit = new NativeMenuItem("Выйти"); exit.Click += (_, _) => { exiting = true; Close(); }; menu.Add(exit); tray.Menu = menu;
            tray.Clicked += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); }; tray.IsVisible = true;
        }
        catch { tray?.Dispose(); tray = null; }
    }
    private void ApplyTheme(PlatformThemeVariant? systemTheme = null)
    {
        // Resolve the OS preference directly. ActualThemeVariant still refers to the
        // previous explicit selection during a light/dark -> system transition.
        var neon = preferences.Theme == "neon";
        var dark = neon || preferences.Theme == "dark" || preferences.Theme == "system" &&
            (systemTheme ?? PlatformSettings?.GetColorValues().ThemeVariant ?? PlatformThemeVariant.Light) == PlatformThemeVariant.Dark;
        var effective = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        RequestedThemeVariant = effective;
        if (Application.Current is not null) Application.Current.RequestedThemeVariant = effective;
        UiPalette.Set(dark, neon); Classes.Set("neon", neon);
    }
    private void OnSystemColorsChanged(object? sender, PlatformColorValues colors)
    {
        if (preferences.Theme != "system") return;
        if (Dispatcher.UIThread.CheckAccess()) ApplyTheme(colors.ThemeVariant);
        else Dispatcher.UIThread.Post(() => { if (preferences.Theme == "system") ApplyTheme(colors.ThemeVariant); });
    }
    private void ReturnFromSettings() { if (signer is not null || watchAddress is not null) ShowAssets(); else ShowAccess(); }
    private void AddControlStyles()
    {
        Styles.Add(new Style(s => s.OfType<Button>()) { Setters = { new Setter(Avalonia.Controls.Button.CornerRadiusProperty, new CornerRadius(6)), new Setter(Avalonia.Controls.Button.PaddingProperty, new Thickness(14, 6)), new Setter(Avalonia.Controls.Button.MinHeightProperty, 34d), new Setter(Avalonia.Controls.Button.BackgroundProperty, UiPalette.Card), new Setter(Avalonia.Controls.Button.BorderBrushProperty, UiPalette.Line), new Setter(Avalonia.Controls.Button.BorderThicknessProperty, new Thickness(1)), new Setter(Avalonia.Controls.Button.ForegroundProperty, UiPalette.Ink) } });
        Styles.Add(new Style(s => s.OfType<Button>().Class("primary")) { Setters = { new Setter(Avalonia.Controls.Button.BackgroundProperty, UiPalette.Primary), new Setter(Avalonia.Controls.Button.ForegroundProperty, UiPalette.PrimaryInk), new Setter(Avalonia.Controls.Button.BorderThicknessProperty, new Thickness(0)), new Setter(Avalonia.Controls.Button.FontWeightProperty, FontWeight.SemiBold) } });
        foreach (var state in new[] { (":pointerover", UiPalette.PrimaryHover), (":pressed", UiPalette.PrimaryPressed), (":disabled", UiPalette.Primary) })
        {
            Styles.Add(new Style(s => s.OfType<Button>().Class("primary").Class(state.Item1).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter")) { Setters = { new Setter(ContentPresenter.BackgroundProperty, state.Item2), new Setter(ContentPresenter.ForegroundProperty, UiPalette.PrimaryInk) } });
        }
        Styles.Add(new Style(s => s.OfType<Button>().Class("nav").Class("selected").Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter")) { Setters = { new Setter(ContentPresenter.BackgroundProperty, UiPalette.Selected), new Setter(ContentPresenter.ForegroundProperty, UiPalette.Ink) } });
        Styles.Add(new Style(s => s.OfType<Button>().Class("nav")) { Setters = { new Setter(Avalonia.Controls.Button.BackgroundProperty, Brushes.Transparent), new Setter(Avalonia.Controls.Button.BorderThicknessProperty, new Thickness(0)), new Setter(Avalonia.Controls.Button.ForegroundProperty, UiPalette.Muted), new Setter(Avalonia.Controls.Button.PaddingProperty, new Thickness(12, 7)) } });
        Styles.Add(new Style(s => s.OfType<Button>().Class("nav").Class("selected")) { Setters = { new Setter(Avalonia.Controls.Button.BackgroundProperty, UiPalette.Selected), new Setter(Avalonia.Controls.Button.ForegroundProperty, UiPalette.Ink) } });
        Styles.Add(new Style(s => s.OfType<Button>().Class("nav")) { Setters = { new Setter(Avalonia.Controls.Button.MinHeightProperty, 32d), new Setter(Avalonia.Controls.Button.PaddingProperty, new Thickness(12, 5)) } });
        Styles.Add(new Style(s => s.OfType<TextBox>()) { Setters = { new Setter(TextBox.BackgroundProperty, UiPalette.Card), new Setter(TextBox.BorderBrushProperty, UiPalette.Line), new Setter(TextBox.CornerRadiusProperty, new CornerRadius(5)), new Setter(TextBox.ForegroundProperty, UiPalette.Ink), new Setter(TextBox.PaddingProperty, new Thickness(10, 6)), new Setter(TextBox.MinHeightProperty, 34d) } });
        Styles.Add(new Style(s => s.OfType<ComboBox>()) { Setters = { new Setter(ComboBox.BackgroundProperty, UiPalette.Card), new Setter(ComboBox.BorderBrushProperty, UiPalette.Line), new Setter(ComboBox.CornerRadiusProperty, new CornerRadius(5)), new Setter(ComboBox.MinHeightProperty, 34d) } });
        Styles.Add(new Style(s => s.OfType<CheckBox>()) { Setters = { new Setter(CheckBox.ForegroundProperty, UiPalette.Ink) } });
        Styles.Add(new Style(s => s.OfType<Window>().Class("neon").Descendant().OfType<Border>().Class("wallet-card")) { Setters = { new Setter(Border.BoxShadowProperty, new BoxShadows(new BoxShadow { Blur = 22, Color = Color.Parse("#2029DCFF") })) } });
    }
    private void AddNav(string label, string icon, Action action)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 }; row.Children.Add(DcIcon.Create(icon)); row.Children.Add(Text(label, 14, Muted));
        var button = new Button { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch }; button.Classes.Add("nav");
        button.Click += (_, _) => { if (!busy) { SelectNav(label); Invalidate(); action(); } }; navigation[label] = button; nav.Children.Add(button);
    }
    private void SelectNav(string label)
    {
        foreach (var item in navigation) item.Value.Classes.Set("selected", item.Key == label);
    }
    private static TextBlock Text(string value, double size = 14, IBrush? brush = null) => new() { Text = value, FontSize = size, FontWeight = size >= 19 ? FontWeight.SemiBold : FontWeight.Normal, Foreground = brush ?? UiPalette.Ink, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.5 };
    private static Image Logo(double size) => new() { Source = new Bitmap(AssetLoader.Open(new Uri("avares://DCWallet/Assets/icon.png"))), Width = size, Height = size };
    private TextBox Field(string label, string? value = null, bool secret = false, bool multiline = false)
    {
        var wrapper = new StackPanel { Spacing = 5 }; wrapper.Children.Add(Text(label));
        var field = new TextBox { Text = value, PasswordChar = secret ? '●' : '\0', AcceptsReturn = multiline, MinHeight = multiline ? 88 : 34, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap };
        if (secret)
        {
            var reveal = new Button { Content = "Показать", FontSize = 11, MinHeight = 24, Padding = new Thickness(7, 0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Muted };
            reveal.Click += (_, _) => { field.RevealPassword = !field.RevealPassword; reveal.Content = field.RevealPassword ? "Скрыть" : "Показать"; };
            field.InnerRightContent = reveal;
        }
        field.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Invalidate(); }; wrapper.Children.Add(field); target.Children.Add(wrapper); return field;
    }
    private Button MakeButton(string label, Func<Task> action, bool primary = false)
    {
        var button = new Button { Content = label, HorizontalAlignment = primary ? HorizontalAlignment.Right : HorizontalAlignment.Left };
        if (primary) button.Classes.Add("primary"); button.Click += async (_, _) => await Run(action); return button;
    }
    private Button Button(string label, Func<Task> action, bool primary = false) { var b = MakeButton(label, action, primary); target.Children.Add(b); return b; }
    private StackPanel BeginCard()
    {
        var content = new StackPanel { Spacing = 16 }; target.Children.Add(Card(content)); target = content; return content;
    }
    private static Border Card(Control child, double padding = 20)
    {
        var card = new Border { Child = child, Padding = new Thickness(padding), Background = UiPalette.Card, BorderBrush = UiPalette.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10) };
        card.Classes.Add("wallet-card"); return card;
    }
    private void EndCard() => target = page;
    private void Reset(string title, string subtitle, bool clearPending = true)
    {
        if (clearPending) Invalidate(); page.Children.Clear(); target = page; status.Text = ""; page.Spacing = 18; page.Margin = new Thickness(0, 32, 0, 24);
        settingsPageActive = false; settingsReturn.IsVisible = false;
        dataChanged = null;
        historyChanged = null;
        journalChanged = null;
        var showSide = signer is not null || watchAddress is not null;
        sidebar.IsVisible = showSide; shell.ColumnDefinitions[0].Width = new GridLength(showSide ? 236 : 0); SetPageWidth(showSide ? 680 : 600);
        if (!string.IsNullOrEmpty(title)) { var heading = Text(title, 24); heading.FontWeight = FontWeight.SemiBold; page.Children.Add(heading); }
        if (!string.IsNullOrEmpty(subtitle)) page.Children.Add(Text(subtitle, 14, Muted));
    }
    private void SetPageWidth(double width) { pageWidth = width; SizePage(); }
    private void SizePage() => page.Width = Math.Max(240, Math.Min(pageWidth, (Bounds.Width > 0 ? Bounds.Width : Width) - (sidebar.IsVisible ? 236 : 0) - 64));
    private void Back(Action action) { var b = Button("← Назад", () => { action(); return Task.CompletedTask; }); b.Classes.Add("nav"); b.Foreground = Mint; }
    private StackPanel Segments(params (string Label, Action Action)[] items)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        foreach (var item in items) { var b = MakeButton(item.Label, () => { item.Action(); return Task.CompletedTask; }); b.BorderThickness = new Thickness(0); b.Background = Brushes.Transparent; panel.Children.Add(b); }
        target.Children.Add(new Border { Child = panel, Background = UiPalette.Soft, BorderBrush = UiPalette.Line, BorderThickness = new Thickness(1), Padding = new Thickness(3), CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left }); return panel;
    }
    private void SetConnection(bool connected) => connection.Text = connected ? "Decimal RPC\nПодключено · " + network.ChainId : "Decimal RPC\nНет соединения";
}
