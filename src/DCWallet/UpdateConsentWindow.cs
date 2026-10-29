using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DCWallet;

public sealed class UpdateConsentWindow : Window
{
    public UpdateConsentWindow()
    {
        Title = "Проверка обновлений";
        Width = 520; SizeToContent = SizeToContent.Height; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiPalette.Canvas; Foreground = UiPalette.Ink;
        var panel = new StackPanel { Margin = new Thickness(28), Spacing = 16 };
        void Label(string text, double size = 14) => panel.Children.Add(new TextBlock { Text = text, FontSize = size, Foreground = UiPalette.Ink, TextWrapping = TextWrapping.Wrap });
        Label("Проверять новые версии?", 23);
        Label("При запуске можно проверять версию на GitHub. GitHub получит IP-адрес и обычные данные HTTPS-запроса. Адреса кошелька, балансы, seed-фразы и ключи в запрос не входят.");
        Label("Без вашего согласия проверка не выполняется. Выбор можно изменить в настройках. RPC и каталоги активов используются при работе с выбранным кошельком независимо от проверки обновлений.");
        var privacy = new Expander { Header = "Конфиденциальность и удаление", Content = new TextBlock { Text = "Ключи хранятся локально в зашифрованном файле. Выбранный RPC и каталоги получают публичные адреса и параметры запросов активов, истории и симуляций. Отправка подписанной транзакции требует подтверждения. Удаление переносимого приложения не удаляет хранилище кошелька; удаляйте резервные копии и данные только после сохранения доступа к средствам.", Foreground = UiPalette.Ink, TextWrapping = TextWrapping.Wrap } };
        panel.Children.Add(privacy);
        var policy = new Button { Content = "Политика конфиденциальности на GitHub", Background = UiPalette.Card, Foreground = UiPalette.Ink };
        policy.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/maxwell2010/DCWallet/blob/main/docs/PRIVACY.md") { UseShellExecute = true }); }
            catch { Label("Не удалось открыть браузер. Текст основных правил показан выше."); }
        };
        panel.Children.Add(policy);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
        foreach (var option in new[] { ("Не проверять", false), ("Проверять", true) })
        {
            var button = new Button { Content = option.Item1, Padding = new Thickness(14, 9), Background = UiPalette.Card, Foreground = UiPalette.Ink, BorderBrush = UiPalette.Line };
            button.Click += (_, _) => Close(option.Item2); buttons.Children.Add(button);
        }
        panel.Children.Add(buttons); Content = panel;
    }
}
