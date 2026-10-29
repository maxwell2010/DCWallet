using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Transactions;

namespace DCWallet;
public sealed partial class MainWindow
{
    private void ShowAccounts()
    {
        if (!RequireWallet()) return;
        Reset("Счета", privateKeyProfile ? "Профиль приватного ключа содержит один адрес. Независимые кошельки доступны в профилях." : "Одна фраза может восстанавливать несколько адресов. Текущий адрес выбирается в меню слева."); SelectNav("Счета"); SetPageWidth(720);
        Button("Профили кошелька", () => { ShowProfiles(); return Task.CompletedTask; });
        BeginCard(); var name = Field("Название счета", preferences.AccountName);
        target.Children.Add(new SelectableTextBlock { Text = Address, Foreground = Mint, TextWrapping = TextWrapping.Wrap });
        Button("Переименовать", () =>
        {
            preferences.AccountName = name.Text?.Trim() is { Length: > 0 and <= 80 } value ? value : throw new ArgumentException("Введите название до 80 символов.");
            preferences.Save(dataDirectory); UpdateAccountLabel(); status.Text = "Название сохранено"; return Task.CompletedTask;
        });
        target.Children.Add(Text(privateKeyProfile ? "Адрес импортированного приватного ключа" : "Адрес № " + (accountIndex + 1) + " · m/44'/60'/0'/0/" + accountIndex, 12, Muted)); EndCard();
        page.Children.Add(Text("Адресная книга", 20)); page.Children.Add(Text("Сохраненные контакты можно выбрать при отправке.", 13, Muted));
        BeginCard(); var contactName = Field("Имя контакта"); var contactAddress = Field("Адрес Decimal"); contactAddress.Watermark = "0x...";
        Button("Сохранить в адресной книге", () =>
        {
            var owner = contactAddress.Text?.Trim() ?? ""; _ = TransactionDraft.Del(owner, owner, "0");
            if (string.IsNullOrWhiteSpace(contactName.Text) || contactName.Text.Length > 80) throw new ArgumentException("Введите имя до 80 символов.");
            preferences.Contacts[owner] = contactName.Text.Trim(); preferences.Save(dataDirectory); ShowAccounts(); status.Text = "Контакт сохранен"; return Task.CompletedTask;
        });
        foreach (var entry in preferences.Contacts)
        {
            target.Children.Add(Text(entry.Value + "\n" + entry.Key, 13));
            var owner = entry.Key; Button("Удалить «" + entry.Value + "»", () => { preferences.Contacts.Remove(owner); preferences.Save(dataDirectory); ShowAccounts(); return Task.CompletedTask; });
        }
        EndCard();
        if (signer is not null && !privateKeyProfile)
        {
            page.Children.Add(Text("Фраза восстановления", 20)); BeginCard(); target.Children.Add(Text("Для просмотра фразы введите пароль кошелька. Убедитесь, что экран не видят посторонние.", 13, Muted)); var password = Field("Пароль", secret: true);
            var secret = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, IsVisible = false }; target.Children.Add(secret);
            Button("Показать фразу", async () => { var pass = password.Text ?? ""; var phrase = await Task.Run(() => Vault.OpenAsync(VaultPath, pass)); password.Text = ""; pass = ""; secret.Text = phrase; phrase = ""; secret.IsVisible = true; }); EndCard();
        }
    }
    private void ShowSettings()
    {
        Reset("Настройки", ""); SelectNav("Настройки"); SetPageWidth(720);
        settingsPageActive = true; settingsReturn.Content = signer is not null || watchAddress is not null ? "Вернуться к кошельку" : "Вернуться ко входу"; settingsReturn.IsVisible = true;
        page.Children.Add(Text("Подключение к сети", 19)); page.Children.Add(Text("Выберите сеть и RPC. Ключи остаются на устройстве, транзакции подписываются локально.", 13, Muted));
        BeginCard(); var selected = new ComboBox { ItemsSource = new[] { "Decimal Mainnet · 75", "Decimal Testnet · 202020" }, SelectedIndex = network.ChainId == 75 ? 0 : 1, HorizontalAlignment = HorizontalAlignment.Stretch, Name = "NetworkSelector" }; target.Children.Add(selected);
        var endpoint = Field("Адрес RPC", network.RpcUrl.AbsoluteUri); selected.SelectionChanged += (_, _) => endpoint.Text = (selected.SelectedIndex == 0 ? DecimalNetwork.Mainnet() : DecimalNetwork.Testnet()).RpcUrl.AbsoluteUri;
        Button("Проверить и подключить", async () =>
        {
            var basis = selected.SelectedIndex == 0 ? DecimalNetwork.Mainnet() : DecimalNetwork.Testnet();
            _ = DecimalNetwork.Custom(basis.Name, basis.ChainId, endpoint.Text ?? ""); var candidate = basis with { RpcUrl = new Uri(endpoint.Text!) }; var nextClient = new DecimalClient(candidate);
            if ((await nextClient.Web3.Eth.ChainId.SendRequestAsync()).Value != candidate.ChainId) throw new InvalidOperationException("RPC подключен к другой сети.");
            network = candidate; client = nextClient; session.Clear(); session = new(client, JournalDirectory); SetConnection(true); Lock(); status.Text = "Подключение проверено. Откройте кошелек в выбранной сети.";
        }); EndCard();
        page.Children.Add(Text("Безопасность", 19)); BeginCard(); var minutes = Field("Автоблокировка, минут", preferences.AutoLockMinutes.ToString()); var ceiling = Field("Предел gas price, Gwei (пусто - без ограничения)", preferences.GasPriceCeilingGwei);
        Button("Сохранить", () =>
        {
            if (!int.TryParse(minutes.Text, out var value) || value is < 1 or > 120) throw new ArgumentException("Укажите от 1 до 120 минут.");
            if (!string.IsNullOrWhiteSpace(ceiling.Text) && Units.Parse(ceiling.Text, 9) <= 0) throw new ArgumentException("Предел gas price должен быть положительным.");
            preferences.AutoLockMinutes = value; preferences.GasPriceCeilingGwei = ceiling.Text?.Trim() ?? ""; preferences.Save(dataDirectory); status.Text = "Настройки сохранены"; return Task.CompletedTask;
        });
        if (File.Exists(VaultPath))
        {
            target.Children.Add(new Separator()); target.Children.Add(Text("Сменить пароль", 15)); var old = Field("Текущий пароль", secret: true); var next = Field("Новый пароль", secret: true); var again = Field("Новый пароль еще раз", secret: true);
            Button("Сменить пароль", async () =>
            {
                if (next.Text != again.Text) throw new ArgumentException("Новые пароли не совпадают.");
                var previous = old.Text ?? ""; var replacement = next.Text ?? "";
                await Task.Run(() => Vault.ChangePasswordAsync(VaultPath, previous, replacement)); previous = replacement = ""; old.Text = next.Text = again.Text = ""; Lock(); status.Text = "Пароль изменен. Откройте кошелек с новым паролем.";
            });
        }
        EndCard(); page.Children.Add(Text("Вид", 19)); page.Children.Add(Text("Тема", 14));
        var themes = Segments(("Как в системе", () => ChangeTheme("system")), ("Темная", () => ChangeTheme("dark")), ("Светлая", () => ChangeTheme("light")), ("Криптопанк", () => ChangeTheme("neon")));
        ((Button)themes.Children[preferences.Theme == "system" ? 0 : preferences.Theme == "dark" ? 1 : preferences.Theme == "neon" ? 3 : 2]).Background = UiPalette.Card;
        if (OperatingSystem.IsWindows())
        {
            page.Children.Add(Text("Обновления", 19));
            if (DistributionPolicy.UsesStoreUpdates) page.Children.Add(Text("Обновления устанавливаются через Microsoft Store. Проверка GitHub отключена.", 13, Muted));
            else
            {
                var updates = new CheckBox { Content = "Проверять новые версии на GitHub при запуске", IsChecked = preferences.CheckForUpdatesWindows == true, Name = "WindowsUpdateChecks" };
                page.Children.Add(updates);
                page.Children.Add(Text("GitHub получает IP-адрес и обычные данные HTTPS-запроса. Данные кошелька не передаются. При выключении проверки запросов версий нет.", 13, Muted));
                updates.IsCheckedChanged += (_, _) => { preferences.CheckForUpdatesWindows = updates.IsChecked == true; preferences.Save(dataDirectory); };
            }
        }
        page.Children.Add(Text("Окно и трей", 19));
        var minimize = new CheckBox { Content = "Сворачивать в трей", IsChecked = preferences.MinimizeToTray }; var close = new CheckBox { Content = "Закрывать в трей: крестик прячет окно", IsChecked = preferences.CloseToTray }; page.Children.Add(minimize); page.Children.Add(close);
        minimize.IsCheckedChanged += (_, _) => { preferences.MinimizeToTray = minimize.IsChecked == true; preferences.Save(dataDirectory); };
        close.IsCheckedChanged += (_, _) => { preferences.CloseToTray = close.IsChecked == true; preferences.Save(dataDirectory); };
        if (File.Exists(VaultPath)) Button("Экспортировать зашифрованную копию", ExportBackup);
        Button("Профили кошелька",()=>{ShowProfiles();return Task.CompletedTask;});
        page.Children.Add(Text("DC WALLET " + AppInfo.Version + "\nФайл кошелька: " + VaultPath, 12, Muted));
    }
    private void ChangeTheme(string theme) { preferences.Theme = theme; preferences.Save(dataDirectory); ApplyTheme(); ShowSettings(); }
    internal async Task<bool> EnsureWindowsUpdateConsentAsync()
    {
        if (!OperatingSystem.IsWindows()) return true;
        if (DistributionPolicy.UsesStoreUpdates) return false;
        if (preferences.CheckForUpdatesWindows is bool enabled) return enabled;
        var choice = await new UpdateConsentWindow { RequestedThemeVariant = ActualThemeVariant }.ShowDialog<bool?>(this);
        if (choice is null) return false;
        preferences.CheckForUpdatesWindows = choice;
        preferences.Save(dataDirectory);
        return choice == true;
    }
    private async Task ExportBackup()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "dc-wallet-backup.vault", Title = "Сохранить зашифрованный кошелек" });
        if (file is null) { status.Text = "Отменено"; return; }
        if (string.Equals(file.TryGetLocalPath(), VaultPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Выберите другое имя для резервной копии.");
        await using var output = await file.OpenWriteAsync(); output.SetLength(0); await using var input = File.OpenRead(VaultPath); await input.CopyToAsync(output); status.Text = "Зашифрованная копия сохранена. Для восстановления потребуется пароль.";
    }
}
