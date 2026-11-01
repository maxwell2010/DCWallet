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
        var shortHold = new CheckBox { Content = "Короткий HOLD - только Testnet", IsVisible = false, Name = "DesktopShortHold" }; target.Children.Add(shortHold);
        var holdMinutes = new TextBox { Text = "5", Name = "DesktopHoldMinutes", MinHeight = 34 };
        var minutesHost = new StackPanel { Spacing = 5, IsVisible = false }; minutesHost.Children.Add(Text("Срок HOLD в минутах (2-60)")); minutesHost.Children.Add(holdMinutes); target.Children.Add(minutesHost);
        void HoldControls() { holdDate.IsVisible = hold.IsChecked == true && !(network.ChainId == 202020 && shortHold.IsChecked == true); shortHold.IsVisible = network.ChainId == 202020 && hold.IsChecked == true; minutesHost.IsVisible = shortHold.IsVisible && shortHold.IsChecked == true; }
        hold.IsCheckedChanged += (_, _) => { Invalidate(); HoldControls(); };
        shortHold.IsCheckedChanged += (_, _) => { Invalidate(); HoldControls(); };
        holdMinutes.TextChanged += (_, _) => Invalidate();
        holdDate.SelectedDateChanged += (_, _) => Invalidate();
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
        string StakeInputs() => string.Join("|", SelectedToken()?.Contract, SelectedNft()?.Contract, SelectedNft()?.TokenId.ToString(), amount.Text, validator.Text, hold.IsChecked?.ToString(), holdDate.SelectedDate?.Ticks.ToString(), shortHold.IsChecked?.ToString(), holdMinutes.Text);
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
                var service = new Erc20Service(client, token.Contract);
                if (await service.GetAllowanceRawAsync(actionOwner, spender) != 0) throw new InvalidOperationException("Сначала явно отзовите прежнее разрешение, затем разрешите точную сумму.");
                draft = service.BuildApproveDraft(Address, spender, raw);
            }
            else if (SelectedNft() is { } nft)
                draft = nft.Standard == "ERC721" ? new Erc721Service(client).Approve(Address, nft.Contract, spender, nft.TokenId) : new Erc1155Service(client).SetApprovalForAll(Address, nft.Contract, spender, true);
            else throw new InvalidOperationException("Для DEL разрешение не требуется.");
            if (actionEpoch != epoch || actionOwner != Address || !ReferenceEquals(actionClient, client) || inputs != StakeInputs()) throw new InvalidOperationException("Параметры делегирования или кошелек изменились.");
            await Preview(draft, "Разрешение контракту делегирования. После исполнения вернитесь к делегированию.", SelectedNft() is { Standard: "ERC1155" } ? "Разрешение на управление всей NFT-коллекцией" : "Разрешение для выбранного актива");
        });
        Button("Отозвать разрешение выбранного токена", async () =>
        {
            var actionEpoch = epoch; var actionOwner = Address; var actionClient = client; var inputs = StakeInputs();
            var token = SelectedToken() ?? throw new InvalidOperationException("Выберите ERC20-токен.");
            var spender = await Spender(); var service = new Erc20Service(actionClient, token.Contract);
            if (await service.GetAllowanceRawAsync(actionOwner, spender) == 0) throw new InvalidOperationException("Разрешение уже равно нулю.");
            if (actionEpoch != epoch || actionOwner != Address || !ReferenceEquals(actionClient, client) || inputs != StakeInputs()) throw new InvalidOperationException("Параметры делегирования или кошелек изменились.");
            await Preview(service.BuildApproveDraft(actionOwner, spender, BigInteger.Zero), "Отзыв разрешения контракту делегирования", "Разрешение станет равным нулю.");
        });
        Button("Делегировать", async () =>
        {
            var actionEpoch = epoch; var actionOwner = Address; var actionClient = client; var actionSigner = signer; var inputs = StakeInputs();
            var validatorAddress = validator.Text?.Trim() ?? ""; var contract = await Spender(); var raw = Units.Parse(amount.Text ?? "", SelectedToken()?.Decimals ?? (SelectedNft() is null ? 18 : 0));
            if (raw <= 0) throw new ArgumentException("Введите положительное количество.");
            long timestamp = 0;
            if (hold.IsChecked == true)
            {
                var block = await actionClient.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(Nethereum.RPC.Eth.DTOs.BlockParameter.CreateLatest()) ?? throw new InvalidOperationException("Время блока недоступно.");
                var blockTime = checked((long)block.Timestamp.Value);
                if (network.ChainId == 202020 && shortHold.IsChecked == true)
                {
                    if (!int.TryParse(holdMinutes.Text, out var minutes) || minutes is < 2 or > 60) throw new ArgumentException("Тестовый HOLD должен быть от 2 до 60 минут.");
                    timestamp = checked(blockTime + minutes * 60);
                }
                else timestamp = new DateTimeOffset(DateTime.SpecifyKind(holdDate.SelectedDate ?? throw new ArgumentException("Выберите дату hold."), DateTimeKind.Local)).ToUnixTimeSeconds();
                if (timestamp <= blockTime + 60) throw new ArgumentException("Дата hold должна быть больше времени блока хотя бы на минуту.");
            }
            string StakeSummary(string symbol) => amount.Text + " " + symbol + "\nВалидатор: " + validatorAddress
                + (timestamp > 0 ? "\nHOLD до: " + DateTimeOffset.FromUnixTimeSeconds(timestamp).ToUniversalTime().ToString("u") + "\nHOLD Unix: " + timestamp : "");
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
                    if (raw > await service.GetBalanceRawAsync(actionOwner)) throw new InvalidOperationException("Недостаточно токенов.");
                    var allowance = await service.GetAllowanceRawAsync(actionOwner, contract);
                    if (allowance < raw)
                    {
                        if (allowance != 0) throw new InvalidOperationException("Прежнего разрешения недостаточно. Сначала явно отзовите его, затем повторите делегирование.");
                        async Task ValidatePermitStake()
                        {
                            if (actionEpoch != epoch || actionOwner != Address || !ReferenceEquals(actionClient, client) || !ReferenceEquals(actionSigner, signer) || inputs != StakeInputs()) throw new InvalidOperationException("Параметры делегирования или кошелек изменились.");
                            if (!(await actionClient.ResolveSystemContractAsync("delegation")).Equals(contract, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Контракт делегирования изменился.");
                            if (await service.GetAllowanceRawAsync(actionOwner, contract) != 0 || await service.GetBalanceRawAsync(actionOwner) < raw) throw new InvalidOperationException("Разрешение или баланс изменились. Повторите расчет.");
                            if (await new ReservableTokenService(actionClient, token.Contract).GetReserveWeiAsync() <= 0) throw new InvalidOperationException("Для делегирования требуется резервный токен.");
                            if (timestamp != 0)
                            {
                                var block = await actionClient.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(Nethereum.RPC.Eth.DTOs.BlockParameter.CreateLatest()) ?? throw new InvalidOperationException("Время блока недоступно.");
                                if (timestamp <= checked((long)block.Timestamp.Value) + 60) throw new InvalidOperationException("Срок HOLD устарел. Повторите расчет.");
                            }
                            if (actionEpoch != epoch || actionOwner != Address || !ReferenceEquals(actionClient, client) || !ReferenceEquals(actionSigner, signer) || inputs != StakeInputs()) throw new InvalidOperationException("Параметры делегирования или кошелек изменились.");
                        }
                        await ValidatePermitStake();
                        await PreviewWithPermit(token.Contract, contract, raw, timestamp > 0 ? "Делегирование токена с HOLD" : "Делегирование токена",
                            StakeSummary(token.Symbol),
                            permit => timestamp > 0 ? drafts.BuildDelegateHoldByPermitDraft(actionOwner, validatorAddress, token.Contract, raw, timestamp, permit.Deadline, permit.V, permit.R, permit.S) : drafts.BuildDelegateByPermitDraft(actionOwner, validatorAddress, token.Contract, raw, permit.Deadline, permit.V, permit.R, permit.S),
                            ValidatePermitStake, () => ShowStakingForm(false));
                        return;
                    }
                    draft = timestamp > 0 ? drafts.BuildDelegateHoldDraft(Address, validatorAddress, token.Contract, raw, timestamp) : drafts.BuildDelegateErc20Draft(Address, validatorAddress, token.Contract, raw);
                }
                else draft = timestamp > 0 ? drafts.BuildDelegateHoldDelDraft(Address, validatorAddress, amount.Text ?? "", timestamp) : drafts.BuildDelegateDelDraft(Address, validatorAddress, amount.Text ?? "");
            }
            if (actionEpoch != epoch || actionOwner != Address || !ReferenceEquals(actionClient, client) || inputs != StakeInputs()) throw new InvalidOperationException("Параметры делегирования или кошелек изменились.");
            await Preview(draft, "Делегирование" + (timestamp > 0 ? " с hold до " + StakeDate(timestamp) : ""), StakeSummary(SelectedToken()?.Symbol ?? (SelectedNft() is { } n ? "NFT #" + n.TokenId : "DEL")));
            if (network.ChainId == 202020 && SelectedNft() is { Standard: "ERC1155" or "ERC721" } selectedNft)
            {
                using var locator = new WalletDataReader(client, Path.Combine(dataDirectory, "discovery")); locator.RememberNftStake(Address, validatorAddress, selectedNft.Contract, selectedNft.TokenId, 0);
            }
        }, true);
        var more = new Expander { Header = "Перенос стейка, hold, токены и NFT", HorizontalAlignment = HorizontalAlignment.Stretch };
        more.Content = MakeButton("Открыть расширенные операции стейкинга", () => { ShowOperations("Staking"); return Task.CompletedTask; }); page.Children.Add(Card(more));
        Button("Управление своим валидатором", () => { ShowOperations("Validators"); return Task.CompletedTask; });
    }
}
