using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace DCWallet;

public sealed class UpdatePromptWindow : Window
{
    public UpdatePromptWindow(string currentVersion, string newVersion, Uri downloadUri, Action<Uri>? openDownload = null)
    {
        Title = "Обновление DC WALLET";
        Width = 470; SizeToContent = SizeToContent.Height; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiPalette.Canvas; Foreground = UiPalette.Ink;
        var content = new StackPanel { Margin = new Thickness(28), Spacing = 16 };
        TextBlock Label(string text, double size = 14, IBrush? color = null) => new()
        { Text = text, FontSize = size, Foreground = color ?? UiPalette.Ink, TextWrapping = TextWrapping.Wrap };
        content.Children.Add(Label("Доступна новая версия", 23));
        content.Children.Add(Label("Установлена: " + currentVersion));
        content.Children.Add(Label("Новая версия: " + newVersion));
        content.Children.Add(Label("Кнопка «Обновить» откроет скачивание в браузере. После загрузки закройте кошелек и установите новую версию.", color: UiPalette.Muted));
        var error = Label("", color: UiPalette.Error);
        error.IsVisible = false; content.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
        var later = new Button { Content = "Позже" };
        later.Click += (_, _) => Close();
        var update = new Button { Content = "Обновить" }; update.Classes.Add("primary");
        update.Click += (_, _) =>
        {
            try
            {
                (openDownload ?? OpenDownload)(downloadUri);
                Close();
            }
            catch (Exception)
            {
                error.Text = "Не удалось открыть браузер. Попробуйте еще раз или скачайте обновление со страницы DCWallet на GitHub.";
                error.IsVisible = true;
            }
        };
        buttons.Children.Add(later); buttons.Children.Add(update); content.Children.Add(buttons);
        Content = content;
        Styles.Add(new Style(s => s.OfType<Button>()) { Setters = {
            new Setter(Button.PaddingProperty, new Thickness(16, 9)), new Setter(Button.CornerRadiusProperty, new CornerRadius(6)),
            new Setter(Button.BackgroundProperty, UiPalette.Card), new Setter(Button.ForegroundProperty, UiPalette.Ink),
            new Setter(Button.BorderBrushProperty, UiPalette.Line) } });
        Styles.Add(new Style(s => s.OfType<Button>().Class("primary")) { Setters = {
            new Setter(Button.BackgroundProperty, UiPalette.Primary), new Setter(Button.ForegroundProperty, UiPalette.PrimaryInk) } });
        foreach (var state in new[] { (":pointerover", UiPalette.PrimaryHover), (":pressed", UiPalette.PrimaryPressed), (":disabled", UiPalette.Primary) })
            Styles.Add(new Style(s => s.OfType<Button>().Class("primary").Class(state.Item1).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
            { Setters = { new Setter(ContentPresenter.BackgroundProperty, state.Item2), new Setter(ContentPresenter.ForegroundProperty, UiPalette.PrimaryInk) } });
        KeyDown += (_, args) => { if (args.Key == Key.Escape) Close(); };
    }
    static void OpenDownload(Uri uri) => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
}
