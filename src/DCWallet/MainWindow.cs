using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Decimal.Web3.Tokens;
using Decimal.Web3.Staking;
using QRCoder;

namespace DCWallet;

public sealed partial class MainWindow : Window
{
    private static readonly IBrush Mint = UiPalette.Accent, Muted = UiPalette.Muted;
    private readonly string dataDirectory;
    private string selectedProfileId="legacy";
    private bool privateKeyProfile;
    private string VaultPath => selectedProfileId=="legacy"?Path.Combine(dataDirectory,"wallet.vault"):new VaultProfiles(dataDirectory).Find(selectedProfileId).VaultPath;
    private DecimalNetwork network = DecimalNetwork.Mainnet();
    private DecimalClient client;
    private WalletSigningSession? signer;
    private TransferSession session;
    private PreparedTransfer? prepared;
    private readonly StackPanel page = new() { Spacing = 18, Margin = new Thickness(0, 32, 0, 24) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Mint, Margin = new Thickness(30, 12) };
    private readonly TextBlock account = new() { Text = "Кошелек заблокирован", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Muted };
    private readonly StackPanel nav = new() { Spacing = 2, Margin = new Thickness(12) };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(10) };
    private DateTimeOffset lastInput = DateTimeOffset.UtcNow;
    private bool busy;
    private int accountIndex;
    private long epoch;
    private string? watchAddress;
    private string Address => signer?.Address ?? watchAddress ?? "";
    private string JournalDirectory => Path.Combine(dataDirectory, "journal");

    public MainWindow(string? testDataDirectory = null, DecimalNetwork? initialNetwork = null, bool autoLoad = true, Func<DecimalClient, WalletDataReader>? dataReaderFactory = null)
    {
        automaticLoading = autoLoad; this.dataReaderFactory = dataReaderFactory;
        network = initialNetwork ?? DecimalNetwork.Mainnet();
        dataDirectory = testDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MintCandy", "DCWallet");
        client = new(network); session = new(client, JournalDirectory);
        LoadSelectedProfile(); InitializeShell(); ShowAccess();
    }
    private async Task Run(Func<Task> action)
    {
        if (busy) return; busy = true; page.IsEnabled = false; status.Foreground = UiPalette.Muted; status.Text = "Выполняется...";
        try { await action(); }
        catch (Exception e)
        {
            status.Foreground = UiPalette.Error;
            status.Text = e switch
            {
                System.Security.Cryptography.CryptographicException => "Неверный пароль или поврежденный файл кошелька.",
                FormatException => "Проверьте формат суммы или номера. Для дробной части используйте точку.",
                ArgumentException when e.Message.Contains("20-byte") => "Введите корректный адрес Decimal: 0x и 40 шестнадцатеричных символов.",
                _ => e.Message
            };
        }
        finally { busy = false; page.IsEnabled = true; FlushPortfolioNotification(); }
    }
    private void Invalidate() { prepared = null; session.Clear(); }
    private bool RequireWallet()
    {
        if (signer is not null || watchAddress is not null) return true; ShowAccess(); status.Text = "Сначала откройте кошелек."; return false;
    }
    private void Lock()
    {
        CancelDataLoads();
        epoch++; signer?.Dispose(); signer = null; watchAddress = null; Invalidate(); account.Text = "Кошелек заблокирован"; ShowAccess();
    }
    private void ShowReceive()
    {
        if (!RequireWallet()) return;
        Reset("Получить", $"{network.Name} | chain ID {network.ChainId}");
        page.Children.Add(new SelectableTextBlock { Text = Address, FontSize = 18, TextWrapping = TextWrapping.Wrap });
        using var generator = new QRCodeGenerator(); using var data = generator.CreateQrCode(Address, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data); using var stream = new MemoryStream(qr.GetGraphic(8));
        page.Children.Add(new Image { Source = new Bitmap(stream), Width = 240, Height = 240, HorizontalAlignment = HorizontalAlignment.Left });
        Button("Скопировать адрес", async () => { if (Clipboard is not null) await Clipboard.SetTextAsync(Address); status.Text = "Адрес скопирован"; }, true);
    }
    private void ShowOperations(string? family = null)
    {
        if (family == "Multisig") { ShowSafe(); return; }
        if (!RequireWallet()) return;
        Reset("Инструменты", "Выберите действие и заполните форму. Перед отправкой кошелек покажет параметры и комиссию.");
        var common = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        common.Children.Add(MakeButton("Обычный перевод", () => { ShowSend(); return Task.CompletedTask; }));
        common.Children.Add(MakeButton("Рассылка", () => { ShowMultisend(); return Task.CompletedTask; }));
        common.Children.Add(MakeButton("Чеки", () => { ShowChecks(); return Task.CompletedTask; }));
        common.Children.Add(MakeButton("Общий счет Safe", () => { ShowSafe(); return Task.CompletedTask; })); page.Children.Add(common);
        if (family == "Multisig") Button("Общий счет Safe: предложение и подписи", () => { ShowSafe(); return Task.CompletedTask; }, true);
        var refreshRegistry = MakeButton("Обновить адреса контрактов из реестра сети", async () =>
        {
            network = await client.ResolveSystemContractsAsync(); client = new(network); session.Clear(); session = new(client, JournalDirectory);
            ShowOperations(); status.Text = "Адреса delegation, token-center, nft-center и wdel прочитаны из Contract Center выбранной сети.";
        });
        var group = new ComboBox { ItemsSource = OperationPresentation.Groups.Where(g => g.Code != "Multisig").ToArray(), HorizontalAlignment = HorizontalAlignment.Stretch };
        var operation = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 350 }; page.Children.Add(group); page.Children.Add(operation);
        ToolTip.SetTip(group, "Раздел операций: переводы, токены, NFT, стейкинг и другие действия сети.");
        ToolTip.SetTip(operation, "Выберите конкретное действие. Описание ниже объясняет его результат.");
        var purpose = Text("", 13, Muted); page.Children.Add(purpose);
        var args = Field("Параметры JSON. Суммы Raw/Wei - целые строки. Байты - 0xHEX. Поля $ задают контракты конструктора.", multiline: true); args.MinHeight = 290;
        var useJson = new CheckBox { Content = "Редактировать исходный JSON", IsChecked = false };
        var argsWrapper = (Control)args.Parent!; page.Children.Remove(argsWrapper);
        var form = new StackPanel { Spacing = 12 }; page.Children.Add(form);
        var contractFields = new StackPanel { Spacing = 10 };
        var advanced = new StackPanel { Spacing = 12 }; advanced.Children.Add(Text("Адреса контрактов и исходные параметры предназначены для расширенных операций. Единицы Raw и Wei указаны в подсказках полей.", 12, Muted)); advanced.Children.Add(refreshRegistry); advanced.Children.Add(contractFields); advanced.Children.Add(useJson); advanced.Children.Add(argsWrapper);
        var advancedSection = new Expander { Header = "Расширенные параметры и JSON", Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch, IsExpanded = false }; advanced.IsVisible = false;
        advancedSection.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) advanced.IsVisible = advancedSection.IsExpanded; }; page.Children.Add(advancedSection);
        args.IsVisible = false; argsWrapper.IsVisible = false;
        Func<string> readForm = () => "{}";
        void RebuildForm()
        {
            form.Children.Clear();
            contractFields.Children.Clear();
            using var doc = JsonDocument.Parse(args.Text ?? "{}");
            var editors = new Dictionary<string, Func<System.Text.Json.Nodes.JsonNode?>>();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var editor = ParameterEditor(property.Name, property.Value); (property.Name.StartsWith('$') ? contractFields : form).Children.Add(editor.View); editors[property.Name] = editor.Read;
            }
            readForm = () => { var root = new System.Text.Json.Nodes.JsonObject(); foreach (var pair in editors) root[pair.Key] = pair.Value(); return root.ToJsonString(OperationCatalog.Json); };
        }

        useJson.IsCheckedChanged += (_, _) =>
        {
            Invalidate();
            if (useJson.IsChecked == true) args.Text = readForm();
            else { try { RebuildForm(); } catch (JsonException) { useJson.IsChecked = true; status.Text = "Исправьте JSON перед переключением в форму."; } }
            args.IsVisible = useJson.IsChecked == true; ((Control)args.Parent!).IsVisible = args.IsVisible; form.IsVisible = useJson.IsChecked != true;
        };
        group.SelectionChanged += (_, _) => { operation.ItemsSource = OperationCatalog.All.Where(o => !(o.Method.DeclaringType?.Name == "TokenEconomyDrafts" && o.Method.Name == "BuildMintDraft") && !(network.ChainId is 75 or 202020 && o.Method.DeclaringType?.Name == "DecimalNftDrafts" && o.Method.Name is "BuildCreateErc721Draft" or "BuildCreateErc1155Draft") && o.Group == (group.SelectedItem as OperationGroup)?.Code).ToArray(); operation.SelectedIndex = 0; };
        operation.SelectionChanged += (_, _) => { Invalidate(); if (operation.SelectedItem is Operation op) { purpose.Text = OperationPresentation.Description(op) + (op.Method.Name == "BuildCreateTokenDraft" ? "\n" + WalletTokenCreationAdapter.ReserveHelp : op.Method.Name == "BuildCreateReservelessTokenDraft" ? "\n" + WalletTokenCreationAdapter.NonMintableCapHelp : ""); args.Text = OperationCatalog.Template(op, network, Address); RebuildForm(); } };
        group.SelectedItem = OperationPresentation.Groups.FirstOrDefault(g => g.Code == (family ?? "Transactions")); if (group.SelectedIndex < 0) group.SelectedIndex = 0;
        var acknowledgement = new CheckBox { Content = "Я проверил контракт, ABI и единицы суммы для этой операции" }; page.Children.Add(acknowledgement);
        Button("Подготовить операцию", async () =>
        {
            if (acknowledgement.IsChecked != true) throw new InvalidOperationException("Подтвердите проверку параметров операции.");
            var op = operation.SelectedItem as Operation ?? throw new ArgumentException("Выберите операцию.");
            var operationEpoch = epoch; var operationClient = client; var operationOwner = Address;
            var json = useJson.IsChecked == true ? args.Text ?? "{}" : readForm();
            var parameters = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            foreach (var name in parameters.Select(p => p.Key).Where(k => k.StartsWith('$')).ToArray())
            {
                var slug = name switch { "$delegationContract" or "$delegationAddress" => "delegation", "$tokenCenter" => "token-center", "$nftCenter" or "$nftCenterContract" => "nft-center", "$delegationNftContract" or "$nftDelegationContract" => "delegation-nft", "$masterValidatorAddress" => "master-validator", _ => null };
                if (slug is not null) parameters[name] = await operationClient.ResolveSystemContractAsync(slug);
            }
            OperationInputRules.Validate(parameters.ToJsonString());
            if (op.Group == "Checks" && op.Method.Name.StartsWith("BuildCreate") && parameters["$contract"]?.GetValue<string>() is { } checksContract)
                parameters["nonce"] = (await CheckLifecycle.GetNonceForAsync(operationClient, checksContract, operationOwner)).ToString();
            if (op.Method.DeclaringType?.Name == "NftDelegationDrafts" || op.Method.DeclaringType?.Name == "DecimalNftDrafts" && (op.Method.Name.StartsWith("BuildDelegate") || op.Method.Name.StartsWith("BuildWithdraw") || op.Method.Name.Contains("TransferStake") || op.Method.Name.Contains("TransferHoldStake")))
                NftDelegationSupport.RequireSupported(operationClient.Network.ChainId, "");
            var draft = await OperationCatalog.BuildAsync(op, parameters.ToJsonString(), operationClient, operationOwner);
            draft = await WalletTokenCreationAdapter.AdaptAsync(op, parameters.ToJsonString(), operationClient, operationOwner, draft);
            if (op.Method.DeclaringType?.Name == "DecimalNftDrafts")
                draft = await CurrentNftDrafts.AdaptLegacyNativeDraftAsync(operationClient, draft, operationOwner);
            if (op.Method.DeclaringType == typeof(Decimal.Web3.Multisend.CandySafeMultisend))
            {
                await new Decimal.Web3.Multisend.CandySafeMultisend(operationClient).VerifyDeploymentAsync();
                if (!draft.To.Equals(Decimal.Web3.Multisend.SafeMultisendDeployment.ForNetwork(operationClient.Network).Address, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Выбран другой контракт ERC20-рассылки.");
            }
            if (operationEpoch != epoch || operationOwner != Address || !ReferenceEquals(operationClient, client)) return;
            if (op.Group == "TokenCreation")
            {
                var tokenCenter = draft.To;
                await Preview(draft, OperationPresentation.Title(op), receiptSummary: receipt => "Созданные токены:\n" + string.Join('\n', CurrentTokenCreationReceiptReader.Read(receipt, tokenCenter).Select(item => item.Address)));
            }
            else await Preview(draft, OperationPresentation.Title(op));
        }, true);
    }
    private async Task Preview(TransactionDraft draft, string description, string? assetSummary = null, Action<Nethereum.RPC.Eth.DTOs.TransactionReceipt>? receiptVerifier = null, Func<Nethereum.RPC.Eth.DTOs.TransactionReceipt, string>? receiptSummary = null)
    {
        var activeSigner = signer ?? throw new InvalidOperationException("Кошелек заблокирован."); var current = epoch;
        var transfer = await session.PrepareAsync(draft, activeSigner);
        if (!string.IsNullOrWhiteSpace(preferences.GasPriceCeilingGwei) && transfer.Quote.GasPriceWei > Units.Parse(preferences.GasPriceCeilingGwei, 9))
        {
            session.Clear(); throw new InvalidOperationException("Gas price превышает ваш предел. Измените ограничение в настройках или повторите позже.");
        }
        if (current != epoch || !ReferenceEquals(activeSigner, signer)) { session.Clear(); return; }
        Reset("Проверка транзакции", description, clearPending: false); prepared = transfer;
        BeginCard(); target.Children.Add(Text(assetSummary ?? Units.Format(draft.ValueWei) + " DEL", 28));
        foreach (var item in new[] { ("Сеть", network.Name + " · " + network.ChainId), ("Отправитель", draft.From), ("Получатель / контракт", draft.To), ("Комиссия", transfer.Quote.EstimatedFeeDel + " DEL"), ("Максимальная комиссия", transfer.Quote.MaximumFeeDel + " DEL") })
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*"), ColumnSpacing = 12 };
            row.Children.Add(Text(item.Item1, 13, Muted)); var value = new SelectableTextBlock { Text = item.Item2, FontSize = 13, Foreground = UiPalette.Ink, TextWrapping = TextWrapping.Wrap }; Grid.SetColumn(value, 1); row.Children.Add(value); target.Children.Add(row);
        }
        EndCard();
        var raw = new StackPanel { Spacing = 10 }; raw.Children.Add(Text("Nonce: " + transfer.Signed.Nonce, 12, Muted)); raw.Children.Add(new SelectableTextBlock { Text = "Хэш: " + transfer.Signed.Hash, Foreground = UiPalette.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        raw.Children.Add(new TextBox { Text = draft.Data, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 140 });
        page.Children.Add(new Expander { Header = "Подробности транзакции", Content = raw, Background = UiPalette.Card, HorizontalAlignment = HorizontalAlignment.Stretch });
        var confirm = new CheckBox { Content = $"Подтверждаю параметры и отправку в {network.Name}" }; page.Children.Add(confirm);
        Button("Отправить подтвержденную транзакцию", async () =>
        {
            if (confirm.IsChecked != true) throw new InvalidOperationException("Подтвердите параметры транзакции.");
            if (!ReferenceEquals(prepared, transfer) || signer is null) throw new InvalidOperationException("Подготовьте транзакцию заново.");
            prepared = null;
            var result = await session.SendAsync(transfer);
            Reset("Отправлено в RPC", "Получение хэша не означает успешного исполнения. Проверьте квитанцию.");
            page.Children.Add(new SelectableTextBlock { Text = result.Hash, TextWrapping = TextWrapping.Wrap });
            Button("Проверить исполнение", async () =>
            {
                var receipt = await client.WaitForReceiptAsync(result.Hash, TimeSpan.FromSeconds(10));
                string? details = null;
                if (receipt.Success == true)
                {
                    if (receiptVerifier is not null || receiptSummary is not null)
                    {
                        var raw = await client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(result.Hash) ?? throw new InvalidOperationException("Квитанция пока недоступна.");
                        receiptVerifier?.Invoke(raw); details = receiptSummary?.Invoke(raw);
                    }
                    dataReadAt = default; historyData = null;
                    if (automaticLoading) ScheduleDataLoad();
                }
                status.Text = (receipt.Success is null ? "Еще ожидает подтверждения" : receipt.Success == true ? $"Исполнено. Блок {receipt.BlockNumber}" : "Ошибка исполнения в блокчейне") + (details is null ? "" : "\n" + details);
            });
        }, true);
        Button("Отменить", () => { ShowSend(); return Task.CompletedTask; }); status.Text = "Транзакция подготовлена. Подтверждение доступно в течение 2 минут.";
    }
    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    private static string FieldLabel(string key)
    {
        var label = key.TrimStart('$') switch
        {
            "from" or "sender" => "Счет отправителя", "to" or "recipient" => "Адрес получателя", "validator" => "Адрес валидатора",
            "token" or "tokenAddress" => "Контракт токена", "collection" => "Контракт NFT-коллекции", "contract" => "Адрес контракта",
            "delegationContract" or "delegationAddress" => "Контракт делегирования", "tokenCenter" => "Token Center", "nftCenter" => "NFT Center",
            "amountDel" => "Количество DEL", "amountRaw" => "Количество в минимальных единицах токена (целое число)",
            "valueWei" or "amountWei" => "Сумма DEL в wei (1 DEL = 10^18 wei)", "reserveValueWei" => "Начальный резерв без комиссии (wei, 1 DEL = 10^18 wei)",
            "memo" => "Комментарий (необязательно)", "spender" or "operatorAddress" => "Адрес, которому предоставляется разрешение",
            "tokenId" => "Идентификатор NFT", "name" => "Название", "symbol" => "Символ", "identity" => "Описание / identity",
            "nonce" => "Nonce операции", "holdTimestamp" => "Срок hold (Unix timestamp, секунды)",
            "deadline" => "Крайний срок permit (Unix timestamp)", "data" => "Данные 0xHEX", "index" => "Номер заявки",
            "approved" => "Разрешить управление", "mintable" or "allowMint" => "Разрешить дополнительный выпуск", "burnable" => "Разрешить сжигание",
            "tokenIn" => "Контракт продаваемого токена", "tokenOut" => "Контракт получаемого токена", "newValidator" => "Новый валидатор",
            "amountInRaw" => "Продать, минимальные единицы токена", "amountOutRaw" => "Получить, минимальные единицы токена",
            "minAmountOutRaw" or "minimumAmountOutRaw" => "Минимум к получению, минимальные единицы",
            "maxAmountInRaw" or "maximumAmountInRaw" => "Максимум к списанию, минимальные единицы",
            "decimals" => "Количество знаков после точки", "amount" => "Количество", "owner" => "Владелец", "creator" => "Создатель",
            "initialSupplyRaw" => "Начальный выпуск в минимальных единицах", "maxSupplyRaw" or "capRaw" => "Предельный выпуск в минимальных единицах",
            "initialReserveWei" => "Начальный резерв DEL в wei", "reserveRatio" => "Коэффициент резервирования",
            "uri" or "tokenUri" or "baseUri" => "Ссылка на метаданные NFT", "quantity" => "Количество экземпляров",
            "frozenStakeIndexes" => "Номера заявок для завершения", "holdTimestamps" => "Исходные сроки hold, Unix timestamp",
            "newHoldTimestamp" => "Новый срок hold, Unix timestamp", "holdTimestampOld" => "Исходный срок hold, Unix timestamp",
            "masterValidatorAddress" => "Контракт Master Validator (разрешается из реестра сети)",
            "recipients" or "transfers" => "Получатели и суммы", "amounts" => "Количества в минимальных единицах",
            "tokenIds" => "Идентификаторы NFT", "meta" => "Метаданные валидатора", "details" => "Описание",
            "encodedVm" => "Подтверждение моста (0xHEX)", "unwrapWrappedDel" => "Преобразовать wrapped DEL в DEL",
            "targetChainId" => "Идентификатор целевой сети", "serviceFeeWei" => "Комиссия сервиса в wei",
            _ => key.TrimStart('$')
        };
        return label;
    }
}
