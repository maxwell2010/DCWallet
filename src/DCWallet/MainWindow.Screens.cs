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
        var segments = Segments(("Одному получателю", ShowSend), ("Нескольким сразу", ShowMultisend), ("Чеком", () => ShowOperations("Checks"))); ((Button)segments.Children[0]).Background = UiPalette.Card;
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
    private void ShowSwap()
    {
        if (!RequireWallet()) return;
        SelectNav("Обмен"); Reset("Обмен", "Покупка и продажа резервных токенов Decimal. Котировку кошелек получает из контракта.");
        BeginCard(); target.Children.Add(Text("Направление")); var direction = new ComboBox { ItemsSource = new[] { "DEL → Токен", "Токен → DEL", "Токен → Токен" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch }; target.Children.Add(direction);
        var token = Field("Контракт резервного токена"); token.Watermark = "0x..."; target.Children.Add(AttachTokenPicker(token)); var amount = Field("Сколько отдать"); amount.Watermark = "0.0";
        var tokenOut = Field("Получить резервный токен"); var outputPicker = AttachTokenPicker(tokenOut); target.Children.Add(outputPicker); ((Control)tokenOut.Parent!).IsVisible = outputPicker.IsVisible = false;
        var slippage = Field("Допустимое проскальзывание, %", "1");
        var quote = Text("Укажите токен и сумму, затем обновите котировку.", 13, Muted); target.Children.Add(quote);
        var ownerEpoch = epoch; TransactionDraft? swapDraft = null; string? quoteInputs = null, quoteSummary = null; DateTimeOffset quotedAt = default;
        string Inputs() => string.Join('|', direction.SelectedIndex, token.Text, tokenOut.Text, amount.Text, slippage.Text, Address, network.ChainId);
        direction.SelectionChanged += (_, _) => { swapDraft = null; Invalidate(); ((Control)tokenOut.Parent!).IsVisible = outputPicker.IsVisible = direction.SelectedIndex == 2; };
        Button("Обновить цену", async () =>
        {
            swapDraft = null; quoteInputs = null;
            var requestedInputs = Inputs(); var requestedDirection = direction.SelectedIndex; var requestedAmount = amount.Text; var requestedToken = token.Text?.Trim() ?? "";
            var quoteClient = client; var quoteOwner = Address; var quoteEpoch = epoch;
            var requestedOutput = tokenOut.Text?.Trim() ?? "";
            if (!decimal.TryParse(slippage.Text, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var percent) || percent < 0 || percent > 20) throw new ArgumentException("Проскальзывание должно быть от 0 до 20%.");
            var at = requestedDirection == 2 ? new Nethereum.RPC.Eth.DTOs.BlockParameter(await quoteClient.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()) : null;
            var tokenAddress = requestedToken; var metadata = await new Erc20Service(quoteClient, tokenAddress).GetMetadataAsync(at);
            var service = new ReservableTokenService(quoteClient, tokenAddress); var input = Units.Parse(requestedAmount ?? "", requestedDirection == 0 ? 18 : metadata.Decimals);
            if (input <= 0) throw new ArgumentException("Введите сумму больше нуля.");
            var outputDecimals = requestedDirection == 0 ? metadata.Decimals : 18; var outputSymbol = requestedDirection == 0 ? metadata.Symbol : "DEL";
            System.Numerics.BigInteger output;
            if (requestedDirection == 2)
            {
                if (requestedOutput.Equals(tokenAddress, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Выберите два разных токена.");
                var outputMeta = await new Erc20Service(quoteClient, requestedOutput).GetMetadataAsync(at);
                var intermediate = await service.CalculateSellOutputWeiAsync(input, at);
                output = await new ReservableTokenService(quoteClient, requestedOutput).CalculateBuyOutputRawAsync(intermediate, at);
                outputDecimals = outputMeta.Decimals; outputSymbol = outputMeta.Symbol;
            }
            else output = requestedDirection == 0 ? await service.CalculateBuyOutputRawAsync(input) : await service.CalculateSellOutputWeiAsync(input);
            if (output <= 0) throw new InvalidOperationException("Контракт вернул нулевую котировку.");
            var basisPoints = (int)(percent * 100); var minimum = output * (10000 - basisPoints) / 10000;
            var center = await quoteClient.ResolveSystemContractAsync("token-center"); var drafts = new TokenEconomyDrafts(center);
            if (requestedInputs != Inputs() || quoteEpoch != epoch || quoteOwner != Address || !ReferenceEquals(quoteClient, client)) throw new InvalidOperationException("Параметры изменились во время расчета. Обновите цену.");
            swapDraft = requestedDirection == 0 ? drafts.BuildBuyTokenForExactDelDraft(quoteOwner, tokenAddress, input, minimum, quoteOwner) : requestedDirection == 1 ? drafts.BuildSellExactTokensForDelDraft(quoteOwner, tokenAddress, input, minimum, quoteOwner) : drafts.BuildConvertDraft(quoteOwner, tokenAddress, requestedOutput, input, minimum, quoteOwner);
            quoteInputs = requestedInputs; quotedAt = DateTimeOffset.UtcNow;
            var exactQuote = $"Получите примерно {Units.Format(output, outputDecimals)} {outputSymbol}\nМинимум: {Units.Format(minimum, outputDecimals)}";
            quote.Text = $"Получите примерно {AmountDisplay.Compact(output, outputDecimals)} {outputSymbol}\nМинимум (≈): {AmountDisplay.Compact(minimum, outputDecimals)}"; ToolTip.SetTip(quote, exactQuote);
            quoteSummary = "Отдаете: " + requestedAmount + " " + (requestedDirection == 0 ? "DEL" : metadata.Symbol) + "\n" + exactQuote; SetConnection(true); status.Text = "Котировка обновлена. Перед отправкой потребуется подтверждение.";
        }); EndCard();
        var approveConvert = Button("Разрешить точную сумму конвертации", async () =>
        {
            if (direction.SelectedIndex != 2) throw new InvalidOperationException("Разрешение требуется только для конвертации токенов.");
            var service = new Erc20Service(client, token.Text?.Trim() ?? ""); var raw = Units.Parse(amount.Text ?? "", await service.GetDecimalsAsync());
            if (raw <= 0) throw new ArgumentException("Введите положительную сумму.");
            await Preview(service.BuildApproveDraft(Address, await client.ResolveSystemContractAsync("token-center"), raw), "Разрешение Token Center на точную сумму конвертации", amount.Text + " токенов");
        }); approveConvert.IsVisible = false;
        direction.SelectionChanged += (_, _) => approveConvert.IsVisible = direction.SelectedIndex == 2;
        Button("Продолжить обмен", async () =>
        {
            if (swapDraft is null || quoteInputs != Inputs() || epoch != ownerEpoch || DateTimeOffset.UtcNow - quotedAt > TimeSpan.FromSeconds(45)) throw new InvalidOperationException("Обновите цену для выбранных параметров.");
            if (direction.SelectedIndex == 2)
            {
                var fingerprint = Inputs(); var selected = swapDraft; var service = new Erc20Service(client, token.Text?.Trim() ?? ""); var raw = Units.Parse(amount.Text ?? "", await service.GetDecimalsAsync());
                if (await service.GetAllowanceRawAsync(Address, selected.To) < raw) throw new InvalidOperationException("Сначала разрешите точную сумму конвертации.");
                if (fingerprint != Inputs() || !ReferenceEquals(selected, swapDraft) || epoch != ownerEpoch) throw new InvalidOperationException("Обновите цену для выбранных параметров.");
            }
            await Preview(swapDraft, "Обмен резервного токена", quoteSummary);
        }, true);
        Button("Другие операции обмена", () => { ShowOperations("TokenEconomy"); return Task.CompletedTask; });
    }
    private void ShowStaking() => ShowStakePortfolio();
    private void ShowStakingForm(bool withdraw)
    {
        if (!RequireWallet()) return;
        if (withdraw) { ShowStakePortfolio(); return; }
        SelectNav("Стейкинг"); Reset("Делегирование", "Выберите актив и валидатора. Сроки возврата определяет контракт сети."); SetPageWidth(800); Back(ShowStakePortfolio);
        var segments = Segments(("Делегирование", () => ShowStakingForm(false)), ("Отзыв", () => ShowStakingForm(true)), ("Заявки на вывод", ShowWithdrawals)); ((Button)segments.Children[withdraw ? 1 : 0]).Background = UiPalette.Card;
        BeginCard(); var validator = Field("Адрес валидатора"); validator.Watermark = "0x..."; target.Children.Add(AttachValidatorPicker(validator));
        var tokens = tokenData?.Items.ToArray() ?? []; var nfts = nftData?.Items.ToArray() ?? [];
        target.Children.Add(Text("Актив"));
        var asset = new ComboBox { ItemsSource = new[] { "DEL" }.Concat(tokens.Select(t => t.ToString())).Concat(nfts.Select(n => n.Collection + " · NFT #" + n.TokenId)).ToArray(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 300 }; target.Children.Add(asset);
        var amount = Field("Количество", "1");
        var hold = new CheckBox { Content = "Делегировать с hold до выбранной даты" }; target.Children.Add(hold);
        var holdDate = new CalendarDatePicker { SelectedDate = DateTime.Today.AddYears(1), IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch }; target.Children.Add(holdDate);
        hold.IsCheckedChanged += (_, _) => { Invalidate(); holdDate.IsVisible = hold.IsChecked == true; };
        asset.SelectionChanged += (_, _) => { Invalidate(); amount.IsReadOnly = asset.SelectedIndex > tokens.Length && asset.SelectedIndex <= tokens.Length + nfts.Length && nfts[asset.SelectedIndex - tokens.Length - 1].Standard == "ERC721"; if (amount.IsReadOnly) amount.Text = "1"; };
        void UpdateStakeAssets()
        {
            var key = asset.SelectedIndex == 0 ? "DEL" : asset.SelectedIndex > 0 && asset.SelectedIndex <= tokens.Length ? tokens[asset.SelectedIndex - 1].Contract : asset.SelectedIndex > tokens.Length ? nfts[asset.SelectedIndex - tokens.Length - 1].Contract + ":" + nfts[asset.SelectedIndex - tokens.Length - 1].TokenId : "DEL";
            tokens = tokenData?.Items.ToArray() ?? []; nfts = nftData?.Items.ToArray() ?? [];
            var keys = new[] { "DEL" }.Concat(tokens.Select(t => t.Contract)).Concat(nfts.Select(n => n.Contract + ":" + n.TokenId)).ToArray();
            asset.ItemsSource = new[] { "DEL" }.Concat(tokens.Select(t => t.ToString())).Concat(nfts.Select(n => n.Collection + " · NFT #" + n.TokenId)).ToArray();
            var index = Array.IndexOf(keys, key); asset.SelectedIndex = index < 0 ? 0 : index;
        }
        PortfolioUpdated += UpdateStakeAssets;
        asset.AttachedToVisualTree += (_, _) => { PortfolioUpdated -= UpdateStakeAssets; PortfolioUpdated += UpdateStakeAssets; };
        asset.DetachedFromVisualTree += (_, _) => PortfolioUpdated -= UpdateStakeAssets;
        EndCard();
        NftBalance? SelectedNft() => asset.SelectedIndex > tokens.Length && asset.SelectedIndex <= tokens.Length + nfts.Length ? nfts[asset.SelectedIndex - tokens.Length - 1] : null;
        TokenBalance? SelectedToken() => asset.SelectedIndex > 0 && asset.SelectedIndex <= tokens.Length ? tokens[asset.SelectedIndex - 1] : null;
        string StakeInputs() => string.Join("|", SelectedToken()?.Contract, SelectedNft()?.Contract, SelectedNft()?.TokenId.ToString(), amount.Text, validator.Text, hold.IsChecked?.ToString(), holdDate.SelectedDate?.Ticks.ToString());
        async Task<string> Spender()
        {
            var nft = SelectedNft() is not null;
            if (network.ChainId == 202020 && SelectedNft() is { Standard: "ERC1155" or "ERC721" })
            {
                await CombinedNftDelegation.VerifyAsync(client); return CombinedNftDelegation.Proxy;
            }
            var contract = await client.ResolveSystemContractAsync(nft ? "delegation-nft" : "delegation");
            if (nft) await NftDelegationSupport.RequireDelegationEnabledAsync(client, contract); return contract;
        }
        Button("Разрешить использование выбранного токена / NFT", async () =>
        {
            var actionEpoch = epoch; var actionOwner = Address; var actionClient = client; var inputs = StakeInputs();
            var spender = await Spender(); TransactionDraft draft;
            if (SelectedToken() is { } token)
            {
                var raw = Units.Parse(amount.Text ?? "", token.Decimals); if (raw <= 0) throw new ArgumentException("Введите положительное количество.");
                draft = new Erc20Service(client, token.Contract).BuildApproveDraft(Address, spender, raw);
            }
            else if (SelectedNft() is { } nft)
                draft = nft.Standard == "ERC721" ? new Erc721Service(client).Approve(Address, nft.Contract, spender, nft.TokenId) : new Erc1155Service(client).SetApprovalForAll(Address, nft.Contract, spender, true);
            else throw new InvalidOperationException("Для DEL разрешение не требуется.");
            if (actionEpoch != epoch || actionOwner != Address || !ReferenceEquals(actionClient, client) || inputs != StakeInputs()) throw new InvalidOperationException("Параметры делегирования или кошелек изменились.");
            await Preview(draft, "Разрешение контракту делегирования. После исполнения вернитесь к делегированию.", SelectedNft() is { Standard: "ERC1155" } ? "Разрешение на управление всей NFT-коллекцией" : "Разрешение для выбранного актива");
        });
        Button("Делегировать", async () =>
        {
            var actionEpoch = epoch; var actionOwner = Address; var actionClient = client; var inputs = StakeInputs();
            var validatorAddress = validator.Text?.Trim() ?? ""; var contract = await Spender(); var raw = Units.Parse(amount.Text ?? "", SelectedToken()?.Decimals ?? (SelectedNft() is null ? 18 : 0));
            if (raw <= 0) throw new ArgumentException("Введите положительное количество.");
            var timestamp = hold.IsChecked == true ? new DateTimeOffset(DateTime.SpecifyKind(holdDate.SelectedDate ?? throw new ArgumentException("Выберите дату hold."), DateTimeKind.Local)).ToUnixTimeSeconds() : 0;
            if (timestamp != 0 && timestamp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) throw new ArgumentException("Дата hold должна быть в будущем.");
            TransactionDraft draft;
            if (SelectedNft() is { } nft)
            {
                var service721 = new Erc721Service(client); var service1155 = new Erc1155Service(client);
                if (nft.Standard == "ERC721")
                {
                    if (!(await service721.OwnerOfAsync(nft.Contract, nft.TokenId)).Equals(Address, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("NFT больше не принадлежит этому счету.");
                    if (!await service721.IsApprovedForAllAsync(nft.Contract, Address, contract) && !(await service721.GetApprovedAsync(nft.Contract, nft.TokenId)).Equals(contract, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Сначала разрешите контракту делегирования использовать этот NFT.");
                }
                else if (raw > await service1155.BalanceOfAsync(nft.Contract, Address, nft.TokenId) || !await service1155.IsApprovedForAllAsync(nft.Contract, Address, contract)) throw new InvalidOperationException("Недостаточно NFT или не выдано разрешение контракту делегирования.");
                if (network.ChainId == 202020 && nft.Standard is "ERC1155" or "ERC721")
                    draft = StakingNftDrafts.Delegate(client, Address, validatorAddress, nft.Contract, nft.TokenId, raw, nft.Standard == "ERC721", timestamp);
                else
                {
                    var drafts = new DecimalNftDrafts(network, await client.ResolveSystemContractAsync("nft-center"), contract);
                    draft = nft.Standard == "ERC721" ? timestamp > 0 ? drafts.BuildDelegateHoldErc721Draft(Address, validatorAddress, nft.Contract, nft.TokenId, timestamp) : drafts.BuildDelegateErc721Draft(Address, validatorAddress, nft.Contract, nft.TokenId)
                        : timestamp > 0 ? drafts.BuildDelegateHoldErc1155Draft(Address, validatorAddress, nft.Contract, nft.TokenId, raw, timestamp) : drafts.BuildDelegateErc1155Draft(Address, validatorAddress, nft.Contract, nft.TokenId, raw);
                }
            }
            else
            {
                var drafts = new DelegationDrafts(network, contract);
                if (SelectedToken() is { } token)
                {
                    var service = new Erc20Service(client, token.Contract);
                    if (raw > await service.GetBalanceRawAsync(Address) || raw > await service.GetAllowanceRawAsync(Address, contract)) throw new InvalidOperationException("Недостаточно токенов или не выдано разрешение контракту делегирования.");
                    draft = timestamp > 0 ? drafts.BuildDelegateHoldDraft(Address, validatorAddress, token.Contract, raw, timestamp) : drafts.BuildDelegateErc20Draft(Address, validatorAddress, token.Contract, raw);
                }
                else draft = timestamp > 0 ? drafts.BuildDelegateHoldDelDraft(Address, validatorAddress, amount.Text ?? "", timestamp) : drafts.BuildDelegateDelDraft(Address, validatorAddress, amount.Text ?? "");
            }
            if (actionEpoch != epoch || actionOwner != Address || !ReferenceEquals(actionClient, client) || inputs != StakeInputs()) throw new InvalidOperationException("Параметры делегирования или кошелек изменились.");
            await Preview(draft, "Делегирование" + (timestamp > 0 ? " с hold до " + StakeDate(timestamp) : ""), amount.Text + " " + (SelectedToken()?.Symbol ?? (SelectedNft() is { } n ? "NFT #" + n.TokenId : "DEL")));
            if (network.ChainId == 202020 && SelectedNft() is { Standard: "ERC1155" or "ERC721" } selectedNft)
            {
                using var locator = new WalletDataReader(client, Path.Combine(dataDirectory, "discovery")); locator.RememberNftStake(Address, validatorAddress, selectedNft.Contract, selectedNft.TokenId, 0);
            }
        }, true);
        var more = new Expander { Header = "Перенос стейка, hold, токены и NFT", HorizontalAlignment = HorizontalAlignment.Stretch };
        more.Content = MakeButton("Открыть расширенные операции стейкинга", () => { ShowOperations("Staking"); return Task.CompletedTask; }); page.Children.Add(Card(more));
        Button("Управление своим валидатором", () => { ShowOperations("Validators"); return Task.CompletedTask; });
    }
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
