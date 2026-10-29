using System.Numerics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Nft;
using Decimal.Web3.Staking;
using Decimal.Web3.Tokens;
using Decimal.Web3.Transactions;

namespace DCWallet;
public sealed partial class MainWindow
{
    private bool automaticLoading;
    private readonly Func<DecimalClient, WalletDataReader>? dataReaderFactory;
    private long dataGeneration;
    private bool dataLoading;
    private string dataOwner = "";
    private long dataChain;
    private DateTimeOffset dataReadAt;
    private BigInteger? nativeBalance;
    private DataSet<TokenBalance>? tokenData;
    private DataSet<NftBalance>? nftData;
    private DataSet<WalletStake>? stakeData;
    private readonly List<string> dataErrors = [];
    private CancellationTokenSource? dataCancellation, historyCancellation;
    private Action? dataChanged;
    private event Action? PortfolioUpdated;
    private bool portfolioNotificationPending;
    private void NotifyPortfolio()
    {
        if (busy) { portfolioNotificationPending = true; return; }
        portfolioNotificationPending = false; PortfolioUpdated?.Invoke(); dataChanged?.Invoke();
    }
    private void FlushPortfolioNotification() { if (portfolioNotificationPending) NotifyPortfolio(); }
    private HistoryPage? historyData;
    private string historyOwner = "";
    private void ClearWalletData()
    {
        dataOwner = ""; dataChain = 0; dataReadAt = default;
        nativeBalance = null; tokenData = null; nftData = null; stakeData = null;
        tokenValues.Clear(); dataErrors.Clear();
        historyData = null; historyOwner = ""; manuallyVerifiedHistory.Clear();
    }
    private void CancelDataLoads() { dataGeneration++; historyGeneration++; dataCancellation?.Cancel(); historyCancellation?.Cancel(); valuationCancellation?.Cancel(); dataLoading = false; historyLoading = false; portfolioNotificationPending = false; dataChanged = null; historyChanged = null; }
    private void DataPage(Action render)
    {
        if (dataOwner != Address.ToLowerInvariant() || dataChain != network.ChainId)
        {
            CancelDataLoads(); dataOwner = Address.ToLowerInvariant(); dataChain = network.ChainId; nativeBalance = null; tokenData = null; tokenValues.Clear(); nftData = null; stakeData = null; dataReadAt = default; historyData = null; historyOwner = "";
        }
        dataChanged = render;
        if (automaticLoading && dataReadAt == default && !dataLoading) ScheduleDataLoad();
    }
    private void ScheduleDataLoad() => Dispatcher.UIThread.Post(async () => { if (!dataLoading && Address.Length > 0) await ReadWalletDataAsync(); });
    private async Task ReadWalletDataAsync()
    {
        if (dataLoading || Address.Length == 0) return;
        dataLoading = true; var generation = ++dataGeneration; var owner = Address.ToLowerInvariant(); var chain = network.ChainId; var version = epoch; var activeClient = client;
        dataCancellation?.Dispose(); dataCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(8)); var ct = dataCancellation.Token;
        using var reader = dataReaderFactory?.Invoke(activeClient) ?? new WalletDataReader(activeClient, Path.Combine(dataDirectory, "discovery"));
        dataErrors.Clear();
        async Task Fetch<T>(Func<Task<T>> read, Action<T> assign, string name)
        {
            try { var result = await read(); if (dataGeneration == generation && epoch == version && Address.Equals(owner, StringComparison.OrdinalIgnoreCase) && network.ChainId == chain && !ct.IsCancellationRequested) { assign(result); NotifyPortfolio(); } }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (dataGeneration == generation && epoch == version) dataErrors.Add(name + ": " + e.Message); }
        }
        try
        {
            await Task.WhenAll(Fetch(() => reader.NativeBalanceAsync(owner, ct), value => nativeBalance = value, "DEL"), Fetch(() => reader.TokensAsync(owner, ct), value => { tokenData = value; tokenValues.Clear(); if (automaticLoading && value.Items.Count > 0) _ = ReadTokenValuesAsync(value); }, "Токены"), Fetch(() => reader.NftsAsync(owner, ct), value => nftData = value, "NFT"), Fetch(() => reader.StakesAsync(owner, ct), value => stakeData = value, "Стейкинг"));
        }
        finally
        {
            if (dataGeneration == generation) dataLoading = false;
            if (dataGeneration == generation && epoch == version && Address.Equals(owner, StringComparison.OrdinalIgnoreCase) && network.ChainId == chain)
            {
                dataReadAt = DateTimeOffset.UtcNow; SetConnection(nativeBalance is not null); if (!busy) dataChanged?.Invoke();
            }
        }
    }
    private static string StakeDate(BigInteger timestamp)
        => timestamp >= 0 && timestamp <= 253402300799 ? DateTimeOffset.FromUnixTimeSeconds((long)timestamp).ToLocalTime().ToString("g") : timestamp + " (Unix timestamp)";
    private void DataStatus()
    {
        if (dataLoading) target.Children.Add(Text("Загружаем токены, NFT и стейки...", 12, Muted));
        var issues = dataErrors.Concat(tokenData?.Issues ?? []).Concat(nftData?.Issues ?? []).Concat(stakeData?.Issues ?? []).Distinct().ToArray();
        if (issues.Length > 0) target.Children.Add(new Expander { Header = "Часть источников недоступна или охват неполный", Content = Text(string.Join("\n", issues), 12, UiPalette.Error), HorizontalAlignment = HorizontalAlignment.Stretch });
        Button("Обновить все активы", async () => { await ReadWalletDataAsync(); dataChanged?.Invoke(); });
    }
    private Action DynamicData(Action render)
    {
        var host = new StackPanel { Spacing = 14 }; target.Children.Add(host);
        void Update()
        {
            var previous = target; host.Children.Clear(); target = host;
            try { render(); } finally { target = previous; }
        }
        DataPage(Update); Update(); return Update;
    }
    private void RenderTokens()
    {
        if (tokenData is null) { target.Children.Add(Text("Токены еще не загружены.", 13, Muted)); return; }
        if (tokenData.Items.Count == 0) target.Children.Add(Text(tokenData.Complete ? "Ненулевые балансы токенов не найдены." : "В доступных каталогах ненулевые балансы не найдены. Охват может быть неполным.", 13, Muted));
        foreach (var holding in tokenData.Items)
        {
            var row = new StackPanel { Spacing = 6 }; var quantity = Text(holding.Symbol + "   " + AmountDisplay.Compact(holding.Raw, holding.Decimals), 17); ToolTip.SetTip(quantity, "Точно: " + holding.Amount + " " + holding.Symbol); row.Children.Add(quantity); row.Children.Add(TokenValuationLine(holding)); row.Children.Add(Text(holding.Name + "\n" + holding.Contract, 12, Muted));
            if (signer is not null) row.Children.Add(MakeButton("Отправить " + holding.Symbol, () => { ShowSendAsset(holding); return Task.CompletedTask; })); target.Children.Add(Card(row, 14));
        }
        target.Children.Add(Text("Балансы проверены в блоке " + tokenData.Block, 12, Muted));
    }
    private void ShowNfts()
    {
        if (!RequireWallet()) return; Reset("NFT", "Коллекции и номера определяются каталогами. Владение и количество проверяются через контракт."); SelectNav("NFT"); SetPageWidth(800);
        Segments(("Мои NFT", ShowNfts), ("Создать коллекцию", () => ShowOperations("Nft")), ("Другие операции", () => ShowOperations("Nft")));
        var search = Field("Поиск по коллекции, контракту или номеру NFT"); var limit = 48; Action? render = null;
        render = DynamicData(() =>
        {
            if (nftData is null) target.Children.Add(Text("NFT еще не загружены.", 13, Muted));
            else
            {
                var query = search.Text?.Trim() ?? ""; var filtered = nftData.Items.Where(n => query.Length == 0 || (n.Collection + " " + n.Contract + " " + n.TokenId).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
                target.Children.Add(Text($"Найдено: {filtered.Length}. Всего NFT-позиций: {nftData.Items.Count}.", 13, Muted));
                if (nftData.Items.Count == 0) target.Children.Add(Text(nftData.Complete ? "NFT не найдены." : "В доступных каталогах NFT не найдены. Внешние коллекции могут отсутствовать в индексе.", 13, Muted));
                foreach (var nft in filtered.Take(limit))
                {
                    var content = new StackPanel { Spacing = 10 }; content.Children.Add(Text(nft.Collection + " · #" + nft.TokenId, 20)); content.Children.Add(Text($"{nft.Standard} · Количество: {nft.Raw}\n{nft.Contract}", 13, Muted));
                    if (signer is not null)
                    {
                        content.Children.Add(MakeButton("Передать NFT", () => { ShowNftTransfer(nft); return Task.CompletedTask; }));
                        if (network.ChainId == 202020 && nft.Standard is "ERC1155" or "ERC721") content.Children.Add(MakeButton("Делегировать NFT", () => { ShowCombinedNftStaking(nft); return Task.CompletedTask; }));
                    }
                    target.Children.Add(Card(content));
                }
                if (filtered.Length > limit) Button("Показать следующие 48 NFT", () => { limit += 48; render!(); return Task.CompletedTask; });
                target.Children.Add(Text("Проверено в блоке " + nftData.Block, 12, Muted));
            }
            foreach (var stake in stakeData?.Items.Where(s => s.TokenType is 2 or 3) ?? []) target.Children.Add(Card(Text($"NFT #{stake.TokenId} · В стейкинге\n{stake.Token}\nКоличество: {stake.Raw}")));
            DataStatus();
        });
        search.TextChanged += (_, _) => { limit = 48; render(); };
    }
    private void ShowNftTransfer(NftBalance nft)
    {
        Reset("Передать NFT", nft.Collection + " · #" + nft.TokenId); Back(ShowNfts); BeginCard(); var to = Field("Получатель 0x..."); var amount = Field("Количество", "1"); amount.IsReadOnly = nft.Standard == "ERC721"; EndCard();
        Button("Продолжить", async () =>
        {
            var quantity = WalletDataReaderQuantity(amount.Text ?? ""); TransactionDraft draft;
            if (nft.Standard == "ERC721")
            {
                var service = new Erc721Service(client); if (!(await service.OwnerOfAsync(nft.Contract, nft.TokenId)).Equals(Address, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Адрес больше не владеет NFT."); draft = service.SafeTransferFrom(Address, nft.Contract, to.Text?.Trim() ?? "", nft.TokenId);
            }
            else
            {
                var service = new Erc1155Service(client); if (quantity <= 0 || quantity > await service.BalanceOfAsync(nft.Contract, Address, nft.TokenId)) throw new InvalidOperationException("Недостаточно NFT."); draft = service.SafeTransferFrom(Address, nft.Contract, to.Text?.Trim() ?? "", nft.TokenId, quantity);
            }
            await Preview(draft, "Передача NFT", quantity + " × NFT #" + nft.TokenId);
        }, true);
    }
    private static BigInteger WalletDataReaderQuantity(string input) => input.Length is > 0 and <= 78 && input.All(char.IsAsciiDigit) ? BigInteger.Parse(input) : throw new ArgumentException("Введите целое количество.");
    private void ShowStakePortfolio()
    {
        if (!RequireWallet()) return; Reset("Стейкинг", "Активные стейки, hold и заявки разморозки загружаются автоматически."); SelectNav("Стейкинг"); SetPageWidth(800); DataPage(ShowStakePortfolio);
        Segments(("Мои стейки", ShowStakePortfolio), ("Делегировать", () => ShowStakingForm(false)), ("Отозвать", () => ShowStakingForm(true)), ("Мой валидатор", () => ShowOperations("Validators")));
        if (network.ChainId == 202020) Button("NFT-стейкинг", () => { ShowCombinedNftWorkspace(); return Task.CompletedTask; });
        if (stakeData is null) page.Children.Add(Text("Стейки еще не загружены.", 13, Muted));
        else
        {
            if (stakeData.Items.Count == 0) page.Children.Add(Text(stakeData.Complete ? "Активные позиции не найдены." : "В доступных источниках позиции не найдены. Полнота не подтверждена.", 13, Muted));
            foreach (var stake in stakeData.Items)
            {
                BeginCard(); var quantity = Text((stake.Decimals is { } decimals ? AmountDisplay.Compact(stake.Raw, decimals) : stake.Amount) + " " + stake.Symbol, 20); ToolTip.SetTip(quantity, "Точно: " + stake.Amount + " " + stake.Symbol); target.Children.Add(quantity); target.Children.Add(Text((stake.ValidatorName.Length > 0 ? stake.ValidatorName : stake.Validator) + "\n" + stake.Token, 12, Muted));
                target.Children.Add(Text(stake.Kind == "active" ? stake.Hold > 0 ? "Hold до " + StakeDate(stake.Hold) : "Активный стейк" : stake.Kind == "transfer" ? stake.Unfreeze <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ? "Перенос ожидает обработки сетью" : "Стейк переносится: ожидается разморозка" : stake.CanComplete ? "Разморозка завершена. Можно завершить заявку." : "Ожидается разморозка", 14));
                if (signer is not null)
                {
                    if (stake.Kind == "active") Button("Отозвать / перенести", () => { ShowStakeOperation(stake); return Task.CompletedTask; });
                    else if (stake.Kind == "withdraw" && stake.CanComplete) Button("Завершить отзыв №" + stake.Id, async () =>
                    {
                        var contract = await client.ResolveSystemContractAsync(stake.TokenType is 2 or 3 ? "delegation-nft" : "delegation"); var fresh = await new FrozenStakeReader(client.Web3, contract).GetFrozenStakeAsync(BigInteger.Parse(stake.Id));
                        if (!fresh.Stake.Delegator.Equals(Address, StringComparison.OrdinalIgnoreCase) || !fresh.IsMatured(DateTimeOffset.UtcNow.ToUnixTimeSeconds())) throw new InvalidOperationException("Заявка еще не готова или уже завершена.");
                        await Preview(new DelegationDrafts(network, contract).BuildCompleteDraft(Address, new[] { BigInteger.Parse(stake.Id) }), "Завершение отзыва №" + stake.Id, stake.Amount + " " + stake.Symbol);
                    });
                }
                EndCard();
            }
            page.Children.Add(Text("Проверено в блоке " + stakeData.Block, 12, Muted));
        }
        DataStatus();
    }
    private void ShowStakeOperation(WalletStake stake)
    {
        if (network.ChainId == 202020 && stake.TokenType is 2 or 3) { ShowCombinedNftWorkspaceFor(stake); return; }
        Reset("Управление стейком", stake.Amount + " " + stake.Symbol); Back(ShowStakePortfolio); BeginCard();
        target.Children.Add(Text((stake.ValidatorName.Length > 0 ? stake.ValidatorName : stake.Validator) + "\n" + stake.Token, 13, Muted));
        var action = new ComboBox { ItemsSource = stake.TokenType is 2 or 3 ? new[] { "Отозвать", "Перенести к другому валидатору" } : new[] { "Отозвать", "Перенести к другому валидатору", "Изменить hold" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch }; target.Children.Add(action);
        var amount = Field("Количество " + stake.Symbol, stake.Decimals is { } d ? Units.Format(stake.Raw, d) : stake.Raw.ToString());
        var nextValidator = Field("Новый валидатор (для переноса)"); target.Children.Add(AttachValidatorPicker(nextValidator));
        target.Children.Add(Text("Новая дата hold (для изменения hold)", 13, Muted)); var holdDate = new CalendarDatePicker { SelectedDate = DateTime.Today.AddYears(1), HorizontalAlignment = HorizontalAlignment.Stretch }; target.Children.Add(holdDate); EndCard();
        Button("Продолжить", async () =>
        {
            var raw = Units.Parse(amount.Text ?? "", stake.Decimals ?? 0); if (raw <= 0) throw new ArgumentException("Введите положительное количество.");
            var isNft = stake.TokenType is 2 or 3; var contract = await client.ResolveSystemContractAsync(isNft ? "delegation-nft" : "delegation");
            if (isNft) NftDelegationSupport.RequireSupported(network.ChainId, contract);
            DelegationStake current;
            if (isNft)
            {
                var result = stake.Hold > 0
                    ? await client.Web3.Eth.GetContractQueryHandler<GetNftHoldStake>().QueryDeserializingToObjectAsync<GetStakeOutput>(new() { Validator = stake.Validator, Delegator = Address, Collection = stake.Token, TokenId = stake.TokenId, Hold = stake.Hold }, contract)
                    : await client.Web3.Eth.GetContractQueryHandler<GetNftStake>().QueryDeserializingToObjectAsync<GetStakeOutput>(new() { Validator = stake.Validator, Delegator = Address, Collection = stake.Token, TokenId = stake.TokenId }, contract);
                current = DelegationStake.FromContract(result.Stake);
            }
            else if (stake.Hold > 0 && stake.Symbol == "DEL")
            {
                var result = await client.Web3.Eth.GetContractQueryHandler<GetNativeHoldStake>().QueryDeserializingToObjectAsync<GetStakeOutput>(new() { Validator = stake.Validator, Delegator = Address, Hold = stake.Hold }, contract); current = DelegationStake.FromContract(result.Stake);
            }
            else current = stake.Hold > 0 ? await new StakeReader(client.Web3, contract).GetHoldStakeAsync(stake.Validator, Address, stake.Token, stake.Hold) : await new StakeReader(client.Web3, contract).GetStakeAsync(stake.Validator, Address, stake.Token);
            if (!current.Delegator.Equals(Address, StringComparison.OrdinalIgnoreCase) || !current.Token.Equals(stake.Token, StringComparison.OrdinalIgnoreCase) || !current.Validator.Equals(stake.Validator, StringComparison.OrdinalIgnoreCase) || current.HoldTimestamp != stake.Hold || current.AmountRaw < raw) throw new InvalidOperationException("Позиция изменилась. Обновите стейки и проверьте количество.");
            TransactionDraft draft;
            if (isNft)
            {
                var nftDrafts = new DecimalNftDrafts(network, await client.ResolveSystemContractAsync("nft-center"), contract);
                draft = action.SelectedIndex == 0 ? stake.Hold > 0 ? nftDrafts.BuildWithdrawHoldDraft(Address, stake.Validator, stake.Token, stake.TokenId, raw, stake.Hold) : nftDrafts.BuildWithdrawDraft(Address, stake.Validator, stake.Token, stake.TokenId, raw)
                    : stake.Hold > 0 ? nftDrafts.BuildTransferHoldStakeDraft(Address, stake.Validator, stake.Token, stake.TokenId, raw, stake.Hold, nextValidator.Text?.Trim() ?? "") : nftDrafts.BuildTransferStakeDraft(Address, stake.Validator, stake.Token, stake.TokenId, raw, nextValidator.Text?.Trim() ?? "");
            }
            else
            {
                var drafts = new DelegationDrafts(network, contract);
                if (action.SelectedIndex == 2)
                {
                    var date = holdDate.SelectedDate ?? throw new ArgumentException("Выберите новую дату hold."); var timestamp = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Local)).ToUnixTimeSeconds(); if (timestamp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() || timestamp <= stake.Hold) throw new ArgumentException("Новая дата должна быть позже текущего hold и текущего времени.");
                    draft = drafts.BuildHoldDraft(Address, stake.Validator, stake.Token, raw, stake.Hold, timestamp);
                }
                else draft = action.SelectedIndex == 0 ? stake.Hold > 0 ? drafts.BuildWithdrawHoldDraft(Address, stake.Validator, stake.Token, raw, stake.Hold) : drafts.BuildWithdrawDraft(Address, stake.Validator, stake.Token, raw)
                    : stake.Hold > 0 ? drafts.BuildTransferHoldDraft(Address, stake.Validator, stake.Token, raw, stake.Hold, nextValidator.Text?.Trim() ?? "") : drafts.BuildTransferDraft(Address, stake.Validator, stake.Token, raw, nextValidator.Text?.Trim() ?? "");
            }
            await Preview(draft, action.SelectedItem?.ToString() ?? "Операция стейкинга", amount.Text + " " + stake.Symbol);
        }, true);
    }
}
