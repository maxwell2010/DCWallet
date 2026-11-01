using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Staking;
using Decimal.Web3.Tokens;
using Decimal.Web3.TokenEconomy;
using Decimal.Web3.Transactions;
using Decimal.Web3.Nft;

namespace DCWallet;
public sealed partial class MainWindow
{
    private void ShowAssets()
    {
        if (!RequireWallet()) return;
        Reset("", ""); SelectNav("Обзор"); SetPageWidth(800); DataPage(ShowAssets);
        BeginCard(); target.Spacing = 20; target.Children.Add(Text(preferences.AccountName, 14, Muted));
        var balance = Text(nativeBalance is { } rawDel ? AmountDisplay.Compact(rawDel) : "-", 38); balance.FontWeight = FontWeight.SemiBold;
        if (nativeBalance is { } exactBalance) ToolTip.SetTip(balance, "Точно: " + Units.Format(exactBalance) + " DEL");
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }; line.Children.Add(balance); var unit = Text("DEL", 18, Muted); unit.VerticalAlignment = VerticalAlignment.Center; line.Children.Add(unit); target.Children.Add(line);
        var pending = Text(nativeBalance is null ? "Баланс еще не загружен" : "Баланс подтвержден в выбранной сети", 12, Muted); target.Children.Add(pending);
        var address = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        address.Children.Add(new SelectableTextBlock { Text = Address, Foreground = Mint, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, FontSize = 14 });
        var copy = MakeButton("Копировать", async () => { if (Clipboard is not null) await Clipboard.SetTextAsync(Address); status.Text = "Адрес скопирован"; }); Grid.SetColumn(copy, 1); address.Children.Add(copy); target.Children.Add(new Border { Child = address, Padding = new Thickness(12, 8), Background = UiPalette.Soft, BorderBrush = UiPalette.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7) });
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var action in new (string Name, Action Action)[] { ("Отправить", ShowSend), ("Получить", ShowReceive), ("Обменять", ShowSwap), ("Делегировать", ShowStaking) })
        {
            var b = MakeButton(action.Name, () => { action.Action(); return Task.CompletedTask; }, action.Name == "Отправить"); b.Margin = new Thickness(0, 0, 8, 4); actions.Children.Add(b);
        }
        target.Children.Add(actions); target.Children.Add(new Separator()); target.Children.Add(Text("Монеты", 15)); RenderTokens();
        var coin = Text(nativeBalance is { } currentRaw ? "DEL     " + AmountDisplay.Compact(currentRaw) : "Обновите баланс, чтобы увидеть количество DEL.", 13, Muted); target.Children.Add(coin);
        Button("Обновить DEL", async () =>
        {
            try
            {
                var owner = Address; var version = epoch; var activeClient = client;
                if ((await activeClient.Web3.Eth.ChainId.SendRequestAsync()).Value != network.ChainId) throw new InvalidOperationException("RPC вернул другую сеть.");
                var raw = await activeClient.GetBalanceWeiAsync(owner); if (owner != Address || version != epoch || !ReferenceEquals(activeClient, client)) return;
                nativeBalance = raw; var amount = AmountDisplay.Compact(raw); balance.Text = amount; coin.Text = "DEL     " + amount; ToolTip.SetTip(balance, "Точно: " + Units.Format(raw) + " DEL"); ToolTip.SetTip(coin, "Точно: " + Units.Format(raw) + " DEL"); pending.Text = "Баланс подтвержден в выбранной сети"; SetConnection(true); status.Text = "Баланс обновлен";
            }
            catch { SetConnection(false); throw; }
        }); EndCard();
        page.Children.Add(Text("Decimal " + (network.ChainId == 75 ? "Mainnet" : "Testnet") + (signer is null ? " · Только просмотр" : " · Ключи на этом устройстве"), 12, Muted)); DataStatus();
    }
    private void ShowSend() => ShowSendAsset(null);
    private void ShowSendAsset(TokenBalance? preset)
    {
        if (!RequireWallet()) return;
        SelectNav("Отправить"); Reset("Отправить", ""); SetPageWidth(640);
        if (signer is null) { page.Children.Add(Card(Text("Этот счет открыт только для просмотра. Для перевода откройте кошелек с ключом."))); return; }
        var segments = Segments(("Одному получателю", ShowSend), ("Нескольким сразу", ShowMultisend), ("Сжигание", ShowBurn)); ((Button)segments.Children[0]).Background = UiPalette.Card;
        BeginCard(); var recipient = Field("Получатель"); recipient.Watermark = "0x...";
        if (preferences.Contacts.Count > 0)
        {
            var contacts = new ComboBox { ItemsSource = preferences.Contacts.Select(p => p.Value + " · " + p.Key).ToArray(), PlaceholderText = "Из адресной книги...", HorizontalAlignment = HorizontalAlignment.Stretch };
            contacts.SelectionChanged += (_, _) => { if (contacts.SelectedIndex >= 0) recipient.Text = preferences.Contacts.Keys.ElementAt(contacts.SelectedIndex); }; target.Children.Add(contacts);
        }
        target.Children.Add(Text("Монета")); var availableTokens = tokenData?.Items.ToArray() ?? []; var asset = new ComboBox { ItemsSource = new[] { "DEL" }.Concat(availableTokens.Select(t => t.ToString())).Append("Другой ERC20").ToArray(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch }; target.Children.Add(asset);
        var amount = Field("Сумма"); amount.Watermark = "0.0";
        var all = MakeButton("Все", async () =>
        {
            if (asset.SelectedIndex == 0)
            {
                var fee = await client.EstimateFeeAsync(TransactionDraft.Del(Address, recipient.Text?.Trim() ?? "", "0")); var available = fee.NativeBalanceWei - fee.MaximumFeeWei;
                if (available <= 0) throw new InvalidOperationException("Недостаточно DEL для оплаты комиссии."); amount.Text = Units.Format(available);
            }
            else { var selected = asset.SelectedIndex <= availableTokens.Length ? availableTokens[asset.SelectedIndex - 1] : null; if (selected is null) throw new InvalidOperationException("Выберите загруженный токен."); var service = new Erc20Service(client, selected.Contract); amount.Text = Units.Format(await service.GetBalanceRawAsync(Address), await service.GetDecimalsAsync()); }
        });
        var amountGroup = (StackPanel)amount.Parent!; amountGroup.Children.Remove(amount);
        var amountRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,54"), ColumnSpacing = 8 }; amountRow.Children.Add(amount); Grid.SetColumn(all, 1); all.HorizontalAlignment = HorizontalAlignment.Stretch; amountRow.Children.Add(all); amountGroup.Children.Add(amountRow);
        var messageTarget = target; var details = new StackPanel { Spacing = 14 }; target = details;
        var memo = Field("Сообщение (необязательно)"); var token = Field("Контракт токена ERC20"); target = messageTarget;
        var tokenGroup = (Control)token.Parent!; tokenGroup.IsVisible = false;
        var advanced = new Expander { Header = "Сообщение и дополнительные параметры", Content = details, Background = UiPalette.Card, BorderBrush = UiPalette.Line, HorizontalAlignment = HorizontalAlignment.Stretch }; target.Children.Add(advanced);
        asset.SelectionChanged += (_, _) => { Invalidate(); var known = asset.SelectedIndex > 0 && asset.SelectedIndex <= availableTokens.Length ? availableTokens[asset.SelectedIndex - 1] : null; if (known is not null) token.Text = known.Contract; tokenGroup.IsVisible = asset.SelectedIndex > availableTokens.Length; ((Control)memo.Parent!).IsVisible = asset.SelectedIndex == 0; if (tokenGroup.IsVisible) advanced.IsExpanded = true; };
        void UpdateAssets()
        {
            var selected = asset.SelectedIndex == 0 ? null : token.Text;
            availableTokens = tokenData?.Items.ToArray() ?? [];
            asset.ItemsSource = new[] { "DEL" }.Concat(availableTokens.Select(t => t.ToString())).Append("Другой ERC20").ToArray();
            var index = selected is null ? -1 : Array.FindIndex(availableTokens, t => t.Contract.Equals(selected, StringComparison.OrdinalIgnoreCase));
            asset.SelectedIndex = selected is null ? 0 : index >= 0 ? index + 1 : availableTokens.Length + 1;
            if (selected is not null) token.Text = selected;
        }
        PortfolioUpdated += UpdateAssets;
        asset.AttachedToVisualTree += (_, _) => { PortfolioUpdated -= UpdateAssets; PortfolioUpdated += UpdateAssets; };
        asset.DetachedFromVisualTree += (_, _) => PortfolioUpdated -= UpdateAssets;
        if (preset is not null) { var idx = Array.FindIndex(availableTokens, t => t.Contract == preset.Contract); asset.SelectedIndex = idx >= 0 ? idx + 1 : availableTokens.Length + 1; token.Text = preset.Contract; }
        EndCard(); page.Children.Add(Text("Перед отправкой покажем получателя, сумму и комиссию.", 12, Muted));
        Button("Продолжить", async () =>
        {
            TransactionDraft draft;
            if (asset.SelectedIndex == 0)
            {
                draft = TransactionDraft.Del(Address, recipient.Text?.Trim() ?? "", amount.Text ?? "", string.IsNullOrEmpty(memo.Text) ? null : memo.Text);
                if (draft.ValueWei <= 0) throw new ArgumentException("Сумма должна быть больше нуля.");
            }
            else
            {
                var service = new Erc20Service(client, token.Text?.Trim() ?? ""); var decimals = await service.GetDecimalsAsync(); var raw = Units.Parse(amount.Text ?? "", decimals);
                if (raw <= 0 || await service.GetBalanceRawAsync(Address) < raw) throw new InvalidOperationException("Проверьте количество и доступный баланс токена."); draft = service.BuildTransferDraft(Address, recipient.Text?.Trim() ?? "", raw);
            }
            await Preview(draft, "Перевод " + (asset.SelectedIndex == 0 ? "DEL" : "ERC20"), asset.SelectedIndex > 0 ? amount.Text + " " + (asset.SelectedIndex <= availableTokens.Length ? availableTokens[asset.SelectedIndex - 1].Symbol : "ERC20") : null);
        }, true);
    }
    private void ShowTokens()
    {
        if (!RequireWallet()) return;
        SelectNav("Токены"); Reset("Токены", "Найденные токены и балансы проверяются через RPC выбранной сети."); DynamicData(() => { RenderTokens(); DataStatus(); });
        Segments(("Мои токены", ShowTokens), ("Создать токен", () => ShowOperations("TokenCreation")), ("Управление", () => ShowOperations("Tokens")));
        BeginCard(); var contract = Field("Адрес контракта"); contract.Watermark = "0x..."; var output = Text("Выберите токен для проверки баланса.", 13, Muted); target.Children.Add(output);
        Button("Показать токен", async () =>
        {
            var service = new Erc20Service(client, contract.Text?.Trim() ?? ""); var metadata = await service.GetMetadataAsync(); var balance = await service.GetBalanceRawAsync(Address);
            output.Text = metadata.Name + "\n" + Units.Format(balance, metadata.Decimals) + " " + metadata.Symbol; SetConnection(true); status.Text = "Данные подтверждены контрактом";
        }, true); EndCard();
    }
    private void ShowStaking() => ShowStakePortfolio();
    private void ShowWithdrawals()
    {
        if (!RequireWallet()) return;
        Reset("Заявки на вывод", "Истечение срока разморозки не завершает отзыв. Состояние заявки проверяется по контракту."); Back(ShowStaking);
        BeginCard(); var index = Field("Номер заявки"); var info = Text("Введите номер, полученный при создании заявки.", 13, Muted); target.Children.Add(info);
        Button("Проверить заявку", async () =>
        {
            var contract = await client.ResolveSystemContractAsync("delegation"); var row = await new FrozenStakeReader(client.Web3, contract).GetFrozenStakeAsync(BigInteger.Parse(index.Text ?? ""));
            if (!row.Stake.Delegator.Equals(Address, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Заявка принадлежит другому адресу.");
            info.Text = $"Заявка №{row.Index}\nТокен: {row.Stake.Token}\nКоличество в минимальных единицах: {row.Stake.AmountRaw}\n" + (row.IsMatured(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) ? "Можно завершить отзыв" : row.IsActiveWithdrawal ? "Ожидается разморозка" : "Заявка неактивна"); status.Text = "Проверено по RPC";
        }, true); EndCard(); Button("Завершение и другие операции", () => { ShowOperations("Staking"); return Task.CompletedTask; });
    }
}
