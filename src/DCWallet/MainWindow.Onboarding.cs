using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;

namespace DCWallet;
public sealed partial class MainWindow
{
    private void ShowAccess()
    {
        Reset("", ""); SetPageWidth(460); page.Margin = new Thickness(0, 40, 0, 16); page.Spacing = 12;
        page.Children.Add(Logo(54)); var title = Text("DC WALLET", 26); title.FontWeight = FontWeight.SemiBold; title.HorizontalAlignment = HorizontalAlignment.Center; page.Children.Add(title);
        var intro = Text("Ключи хранятся только на этом устройстве и зашифрованы вашим паролем. Кошелек работает в сети Decimal.", 14, Muted); intro.TextAlignment = TextAlignment.Center; page.Children.Add(intro);
        if (File.Exists(VaultPath))
        {
            BeginCard(); var password = Field("Пароль кошелька", secret: true); EndCard();
            var open = Button("Открыть кошелек", async () =>
            {
                var current = epoch; var unlockPassword = password.Text ?? "";
                var phrase = await Task.Run(() => Vault.OpenCredentialAsync(VaultPath, unlockPassword)); unlockPassword = "";
                if (current != epoch) return;
                var isKey=phrase.Kind=="PrivateKey";var active = await Task.Run(() => CreateCredentialSigner(phrase)); phrase = new("Mnemonic","");
                if (current != epoch) { active.Dispose(); return; }
                privateKeyProfile=isKey;if(isKey)accountIndex=0;signer = active; watchAddress = null; password.Text = ""; lastInput = DateTimeOffset.UtcNow; UpdateAccountLabel(); ShowAssets();
            }, true); open.HorizontalAlignment = HorizontalAlignment.Stretch;
            page.Children.Add(Text("Забыли пароль? Кошелек можно восстановить из фразы в отдельном профиле. Текущий файл не будет перезаписан.", 12, Muted));
        }
        else
        {
            WelcomeAction("Создать кошелек", "Новая фраза восстановления из 12 или 24 слов", "create", ShowCreate, true);
            page.Children.Add(Text("Уже есть кошелек", 12, Muted));
            WelcomeAction("Фраза восстановления", "12 или 24 слова из существующего кошелька", "restore", ShowImport);
            WelcomeAction("Приватный ключ", "Импорт ключа в зашифрованный профиль", "restore", ShowPrivateKeyImport);
            WelcomeAction("Зашифрованный файл", "Восстановление резервной копии DC WALLET", "export", () => _ = Run(RestoreBackup));
            WelcomeAction("Только просмотр", "Баланс и история адреса без возможности подписи", "assets", ShowWatch);
        }
        Button("Профили кошелька",()=>{ShowProfiles();return Task.CompletedTask;});
        var foot = Text("Сеть Decimal · " + (network.ChainId == 75 ? "Mainnet" : "Testnet") + "\nDC WALLET " + AppInfo.Version, 12, Muted); foot.TextAlignment = TextAlignment.Center; foot.Margin = new Thickness(0, 12, 0, 0); page.Children.Add(foot);
        var settings = Button("Настройки подключения", () => { ShowSettings(); return Task.CompletedTask; }); settings.Classes.Add("nav"); settings.HorizontalAlignment = HorizontalAlignment.Center;
    }
    private void WelcomeAction(string title, string description, string icon, Action action, bool primary = false)
    {
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*"), Margin = new Thickness(4) };
        var visual = DcIcon.Create(icon); visual.VerticalAlignment = VerticalAlignment.Center; content.Children.Add(visual);
        if (primary && visual is Viewbox box && box.Child is Canvas canvas) foreach (var path in canvas.Children.OfType<Avalonia.Controls.Shapes.Path>()) path.Stroke = UiPalette.PrimaryInk;
        var copy = new StackPanel { Spacing = 1 }; var heading = Text(title, 14, primary ? UiPalette.PrimaryInk : null); heading.FontWeight = FontWeight.SemiBold; copy.Children.Add(heading); copy.Children.Add(Text(description, 12, primary ? UiPalette.PrimaryInk : Muted)); Grid.SetColumn(copy, 1); content.Children.Add(copy);
        var button = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(14, 10), HorizontalContentAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(10) };
        if (primary) button.Classes.Add("primary"); button.Click += (_, _) => { if (!busy) action(); }; page.Children.Add(button);
    }
    private void ShowCreate() => ShowPhraseStep(DecimalWallet.GenerateMnemonic(12));
    private void StepHeader(int active, Action back)
    {
        Reset("", ""); SetPageWidth(600); page.Margin = new Thickness(0, 70, 0, 24); Back(back);
        var steps = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        var names = new[] { "1. Фраза", "2. Проверка", "3. Пароль" };
        for (var i = 0; i < names.Length; i++)
        {
            var caption = Text(names[i], 13, i == active ? UiPalette.Ink : Muted); if (i == active) caption.FontWeight = FontWeight.SemiBold;
            var border = new Border { Child = caption, BorderBrush = i <= active ? Mint : UiPalette.Line, BorderThickness = new Thickness(0, 0, 0, 2), Padding = new Thickness(0, 0, 0, 8), Margin = new Thickness(0, 0, 6, 0) }; Grid.SetColumn(border, i); steps.Children.Add(border);
        }
        page.Children.Add(steps);
    }
    private void ShowPhraseStep(string phrase)
    {
        StepHeader(0, ShowAccess); var title = Text("Фраза восстановления", 24); title.FontWeight = FontWeight.SemiBold; page.Children.Add(title);
        page.Children.Add(Text("Эти слова - ключ ко всем средствам кошелька. Сохраните их по порядку: запишите или перенесите в надежный менеджер паролей.", 14, Muted));
        var segments = Segments(("12 слов", () => ShowPhraseStep(DecimalWallet.GenerateMnemonic(12))), ("24 слова", () => ShowPhraseStep(DecimalWallet.GenerateMnemonic(24))));
        ((Button)segments.Children[phrase.Split(' ').Length == 12 ? 0 : 1]).Background = UiPalette.Card;
        var grid = new UniformGrid { Columns = 3, Rows = phrase.Split(' ').Length / 3 };
        var words = phrase.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            var word = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }; word.Children.Add(Text((i + 1).ToString(), 12, Muted)); var value = Text(words[i]); value.FontFamily = new FontFamily("Consolas, monospace"); word.Children.Add(value);
            grid.Children.Add(new Border { Child = word, Margin = new Thickness(4), Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(7), BorderBrush = UiPalette.Line, BorderThickness = new Thickness(1), Background = UiPalette.Soft });
        }
        page.Children.Add(Card(grid, 12));
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var copy = MakeButton("Копировать фразу", async () => { if (Clipboard is not null) await Clipboard.SetTextAsync(phrase); status.Text = "Фраза скопирована. Очистите буфер после сохранения."; }); actions.Children.Add(copy);
        var next = MakeButton("Далее", () => { ShowVerificationStep(phrase); return Task.CompletedTask; }, true); Grid.SetColumn(next, 1); actions.Children.Add(next); page.Children.Add(actions);
    }
    private void ShowVerificationStep(string phrase)
    {
        StepHeader(1, () => ShowPhraseStep(phrase)); page.Children.Add(Text("Проверим сохраненную фразу", 24)); page.Children.Add(Text("Введите три слова из своей записи. Проверку можно пропустить.", 14, Muted));
        var words = phrase.Split(' '); var indices = new HashSet<int>(); while (indices.Count < 3) indices.Add(RandomNumberGenerator.GetInt32(words.Length));
        BeginCard(); var fields = indices.Order().Select(i => (Index: i, Field: Field("Слово № " + (i + 1)))).ToArray(); EndCard();
        Button("Пропустить проверку", () => { ShowPasswordStep(phrase); return Task.CompletedTask; });
        Button("Далее", () =>
        {
            if (fields.Any(f => !string.Equals(f.Field.Text?.Trim(), words[f.Index], StringComparison.Ordinal))) throw new ArgumentException("Слова не совпадают. Проверьте номера в своей записи.");
            ShowPasswordStep(phrase); return Task.CompletedTask;
        }, true);
    }
    private void ShowPasswordStep(string phrase)
    {
        StepHeader(2, () => ShowVerificationStep(phrase)); page.Children.Add(Text("Пароль кошелька", 24)); page.Children.Add(Text("Пароль шифрует ключи на этом устройстве. Забытый пароль восстановить нельзя, но кошелек всегда можно восстановить по фразе.", 14, Muted));
        BeginCard(); var password = Field("Пароль", secret: true); var repeat = Field("Пароль еще раз", secret: true); target.Children.Add(Text("Не короче 12 символов.", 12, Muted)); EndCard();
        Button("Создать кошелек", async () =>
        {
            if (password.Text != repeat.Text) throw new ArgumentException("Пароли не совпадают."); var pass = password.Text ?? "";
            await Task.Run(() => StoreCredentialAsync(new VaultCredential("Mnemonic",phrase),pass,"Новый кошелек")); pass = phrase = ""; password.Text = repeat.Text = ""; ShowAccess(); status.Text = "Кошелек создан. Введите пароль для входа.";
        }, true);
    }
    private void ShowImport()
    {
        Reset("", ""); SetPageWidth(560); Back(ShowAccess); page.Children.Add(Text("Восстановить по фразе", 24)); page.Children.Add(Text("Фраза BIP39 из вашего Decimal-кошелька. Восстанавливаются адреса по пути m/44'/60'/0'/0/index.", 14, Muted));
        BeginCard(); var phrase = Field("Фраза восстановления", multiline: true); phrase.Watermark = "word word word ...";
        Button("Вставить из буфера", async () => { if (Clipboard is not null) phrase.Text = await Clipboard.TryGetTextAsync(); });
        var name = Field("Название счета", preferences.AccountName); target.Children.Add(new Separator()); target.Children.Add(Text("Пароль защищает ключи на этом устройстве. Он потребуется при входе в кошелек.", 12, Muted));
        var password = Field("Пароль кошелька", secret: true); var repeat = Field("Пароль еще раз", secret: true); target.Children.Add(Text("Не короче 12 символов.", 12, Muted)); EndCard();
        Button("Восстановить", async () =>
        {
            if (password.Text != repeat.Text) throw new ArgumentException("Пароли не совпадают.");
            var seed = NormalizePhrase(phrase.Text ?? ""); var pass = password.Text ?? "";
            await Task.Run(() => StoreCredentialAsync(new VaultCredential("Mnemonic",seed),pass,name.Text??"Импорт")); seed = pass = ""; preferences.AccountName = name.Text?.Trim() is { Length: > 0 } n ? n : "Основной"; preferences.Save(dataDirectory);
            phrase.Text = password.Text = repeat.Text = ""; ShowAccess();
        }, true);
    }
    private static string NormalizePhrase(string input) => string.Join(' ', Regex.Matches(input.ToLowerInvariant(), "[a-z]+").Select(m => m.Value));
    private async Task RestoreBackup()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Резервная копия DC WALLET", AllowMultiple = false });
        if (files.Count == 0) { status.Text = "Отменено"; return; }
        var path = files[0].TryGetLocalPath() ?? throw new IOException("Требуется локальный файл."); Reset("Восстановить резервную копию", "Введите пароль, которым зашифрован выбранный файл."); Back(ShowAccess);
        BeginCard(); var password = Field("Пароль", secret: true); EndCard();
        Button("Восстановить", async () =>
        {
            var pass = password.Text ?? ""; var credential = await Task.Run(() => Vault.OpenCredentialAsync(path, pass)); await StoreCredentialAsync(credential,pass,"Восстановленный"); pass = ""; password.Text = ""; ShowAccess();
        }, true);
    }
    private void ShowWatch()
    {
        Reset("", ""); SetPageWidth(560); Back(ShowAccess); page.Children.Add(Text("Только просмотр", 24)); page.Children.Add(Text("Кошелек покажет баланс и историю адреса. Подписывать транзакции с него нельзя.", 14, Muted));
        BeginCard(); var address = Field("Адрес Decimal"); address.Watermark = "0x..."; var name = Field("Название счета", "Для просмотра"); EndCard();
        Button("Открыть для просмотра", () =>
        {
            var owner = address.Text?.Trim() ?? ""; _ = TransactionDraft.Del(owner, owner, "0"); accountIndex = 0; privateKeyProfile = false; watchAddress = owner; preferences.AccountName = name.Text?.Trim() ?? "Для просмотра"; UpdateAccountLabel(); ShowAssets(); return Task.CompletedTask;
        }, true);
    }
    private void UpdateAccountLabel()
    {
        account.Text = Address.Length >= 12 ? Address[..8] + "..." + Address[^6..] + (signer is null ? " · просмотр" : privateKeyProfile?" · ключ":" · фраза") : "Кошелек заблокирован";
        var selector = shell.GetLogicalDescendants().OfType<ComboBox>().FirstOrDefault(c => c.Name == "AccountSelector");
        if (selector is not null) { var derived = signer is not null && !privateKeyProfile; selector.IsEnabled=derived; selector.ItemsSource = Enumerable.Range(1, derived?10:1).Select(i => i == 1 ? preferences.AccountName : "Счет " + i).ToArray(); selector.SelectedIndex = derived ? accountIndex : 0; }
    }
}
