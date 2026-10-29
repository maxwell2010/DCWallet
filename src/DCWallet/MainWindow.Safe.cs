using System.Numerics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;

namespace DCWallet;
public sealed partial class MainWindow
{
    private sealed record SafeProposal(long ChainId, SafeTransactionDraft Transaction);
    private const string SafeReadAbi = """
    [{"type":"function","name":"getOwnersWithWeights","inputs":[],"outputs":[{"type":"tuple[]","components":[{"name":"owner","type":"address"},{"name":"weight","type":"uint256"}]}]},
     {"type":"function","name":"getWeightThreshold","inputs":[],"outputs":[{"type":"uint256"}]},
     {"type":"function","name":"nonce","inputs":[],"outputs":[{"type":"uint256"}]}]
    """;
    private async Task<(List<string> Owners, List<SafeMember> Members, BigInteger Threshold, BigInteger Nonce)> ReadSafe(string address)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(address, "^0x[0-9a-fA-F]{40}$")) throw new ArgumentException("Введите EVM-адрес Safe.");
        if ((await client.Web3.Eth.ChainId.SendRequestAsync()).Value != network.ChainId) throw new InvalidOperationException("RPC подключен к другой сети.");
        var code = await client.Web3.Eth.GetCode.SendRequestAsync(address);
        if (string.IsNullOrEmpty(code) || code == "0x") throw new InvalidOperationException("По адресу нет контракта Safe.");
        var contract = client.Web3.Eth.GetContract(SafeReadAbi, address);
        var block = new BlockParameter(await client.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync());
        var owners = await contract.GetFunction("getOwnersWithWeights").CallDeserializingToObjectAsync<WeightedOwnersOutput>(block);
        var members = owners.Members.Select(m => new SafeMember(m.Owner, m.Weight)).ToList();
        var threshold = await contract.GetFunction("getWeightThreshold").CallAsync<BigInteger>(block);
        var nonce = await contract.GetFunction("nonce").CallAsync<BigInteger>(block);
        SafeQuorum.Validate(members, threshold);
        return (members.Select(m => m.Owner).ToList(), members, threshold, nonce);
    }
    private void ShowSafe()
    {
        if (!RequireWallet()) return;
        Reset("Общий счет Safe", "Взвешенный Decimal Safe: предложение, подписи участников, исполнение.");
        SelectNav("Общий счет");
        Button("Создать общий счет Safe", () => { ShowSafeCreation(); return Task.CompletedTask; });
        var safe = Field("Адрес существующего Safe");
        var info = Text("Владельцы, порог и nonce загружаются из контракта.", 13, Muted); page.Children.Add(info);
        Button("Загрузить владельцев и порог", async () => { var state = await ReadSafe(safe.Text ?? ""); info.Text = $"Порог: {state.Threshold}; nonce: {state.Nonce}\n" + string.Join('\n', state.Members.Select(m => m.Owner + " · вес " + m.Weight)); });
        var recipient = Field("Получатель"); var token = Field("Контракт ERC20 (пусто для DEL)"); target.Children.Add(AttachTokenPicker(token));
        var amount = Field("Количество (для ERC20 минимальные единицы, для DEL обычная сумма)");
        var delegateMode = new CheckBox { Content = "Расширенная операция DelegateCall" };
        var delegateConsent = new CheckBox { Content = new TextBlock { Text = "Проверил код и calldata: DelegateCall может изменить владельцев и все хранилище Safe", TextWrapping = Avalonia.Media.TextWrapping.Wrap } };
        var delegateData = new TextBox { AcceptsReturn = true, Watermark = "0x calldata для DelegateCall", MinHeight = 70 };
        var advanced = new StackPanel { Spacing = 8 }; advanced.Children.Add(delegateMode); advanced.Children.Add(delegateData); advanced.Children.Add(delegateConsent);
        page.Children.Add(new Expander { Header = "Расширенный вызов контракта", Content = advanced });
        void RequireConsent(SafeTransactionDraft tx) { if (tx.Operation == 1 && delegateConsent.IsChecked != true) throw new InvalidOperationException("Подтвердите проверку кода и calldata DelegateCall."); }

        SafeProposal? proposal = null; var version = epoch; var chain = network.ChainId;
        var summary = new TextBox { IsReadOnly = true, AcceptsReturn = true, MinHeight = 140, TextWrapping = Avalonia.Media.TextWrapping.Wrap }; page.Children.Add(summary);
        void Display() { var tx = proposal!.Transaction; summary.Text = $"Сеть: {proposal.ChainId}\nSafe: {tx.SafeAddress}\nПолучатель/контракт: {tx.To}\nDEL wei: {tx.ValueWei}\nNonce: {tx.Nonce}\nData: {tx.Data}\nОперация: {(tx.Operation == 1 ? "DELEGATECALL - запись в хранилище Safe" : "CALL")}\nSafeTx hash: {SafeAdvancedTransactions.CalculateHash(tx, proposal.ChainId)}"; }
        Button("Создать предложение", async () =>
        {
            var inputSafe = safe.Text ?? ""; var inputRecipient = recipient.Text ?? ""; var inputToken = token.Text ?? ""; var inputAmount = amount.Text ?? "0"; var inputData = delegateData.Text ?? "0x"; var inputDelegate = delegateMode.IsChecked == true;
            var state = await ReadSafe(inputSafe);
            if (version != epoch || chain != network.ChainId || inputSafe != (safe.Text ?? "") || inputRecipient != (recipient.Text ?? "") || inputToken != (token.Text ?? "") || inputAmount != (amount.Text ?? "0") || inputData != (delegateData.Text ?? "0x") || inputDelegate != (delegateMode.IsChecked == true)) throw new InvalidOperationException("Параметры предложения изменились.");
            var tx = inputDelegate
                ? new SafeTransactionDraft(inputSafe, inputRecipient, 0, inputData, 1, 0, 0, 0, SafeTransactionDraft.ZeroAddress, SafeTransactionDraft.ZeroAddress, state.Nonce)
                : string.IsNullOrWhiteSpace(inputToken) ? SafeTransactionDrafts.BuildDel(inputSafe, inputRecipient, Units.Parse(inputAmount), state.Nonce)
                : SafeTransactionDrafts.BuildErc20Transfer(inputSafe, inputToken, inputRecipient, BigInteger.Parse(inputAmount), state.Nonce);
            if (version != epoch || chain != network.ChainId) return;
            RequireConsent(tx); SafeAdvancedTransactions.CalculateHash(tx, chain); proposal = new(chain, tx); Display();
        }, true);
        Button("Экспортировать предложение", async () =>
        {
            if (proposal is null) throw new InvalidOperationException("Сначала создайте или импортируйте предложение.");
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "safe-proposal.json" }); if (file is null) return;
            await using var stream = await file.OpenWriteAsync(); stream.SetLength(0); await JsonSerializer.SerializeAsync(stream, proposal, OperationCatalog.Json);
        });
        Button("Импортировать предложение", async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, Title = "Предложение Safe" }); if (files.Count == 0) return;
            await using var stream = await files[0].OpenReadAsync(); using var reader = new StreamReader(stream);
            var buffer = new char[65537]; var count = await reader.ReadBlockAsync(buffer); if (count > 65536) throw new InvalidDataException("Слишком большой файл предложения.");
            var imported = JsonSerializer.Deserialize<SafeProposal>(new string(buffer, 0, count), OperationCatalog.Json) ?? throw new InvalidDataException("Пустое предложение.");
            if (imported.ChainId != network.ChainId) throw new InvalidOperationException("Предложение другой сети.");
            SafeAdvancedTransactions.CalculateHash(imported.Transaction, imported.ChainId);
            if (imported.Transaction.Operation > 1 || imported.Transaction.SafeTxGas != 0 || imported.Transaction.BaseGas != 0 || imported.Transaction.GasPriceWei != 0 || imported.Transaction.GasToken != SafeTransactionDraft.ZeroAddress || imported.Transaction.RefundReceiver != SafeTransactionDraft.ZeroAddress) throw new InvalidDataException("Поддерживается Call/DelegateCall без возмещения комиссии из Safe.");
            if (version != epoch || chain != network.ChainId) return;
            delegateConsent.IsChecked = false; proposal = imported; safe.Text = imported.Transaction.SafeAddress; Display();
        });
        var signatures = Field("Подписи: адрес:0xHEX, адрес:approved_hash или адрес:contract:0xHEX", multiline: true);
        var password = Field("Пароль кошелька для подписи предложения", secret: true);
        Button("Подписать показанное предложение", async () =>
        {
            if (proposal is null || signer is null) throw new InvalidOperationException("Откройте кошелек и предложение.");
            var active = proposal; var owner = Address; var activeClient = client;
            void CurrentProposal() { if (version != epoch || chain != network.ChainId || owner != Address || !ReferenceEquals(activeClient, client) || !ReferenceEquals(active, proposal)) throw new InvalidOperationException("Предложение или кошелек изменились."); RequireConsent(active.Transaction); }
            RequireConsent(active.Transaction); var state = await ReadSafe(active.Transaction.SafeAddress); await CheckSafeHash(active.Transaction, chain); CurrentProposal();
            if (state.Nonce != active.Transaction.Nonce || !state.Owners.Contains(Address, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Nonce изменился или счет не является владельцем Safe.");
            var credential = await Vault.OpenCredentialAsync(VaultPath, password.Text ?? ""); password.Text = "";
            try
            {
                CurrentProposal(); using var signing = CreateCredentialSigner(credential);
                var signature = new SafeSignature(signing.Address, signing.SignDigest(Convert.FromHexString(SafeAdvancedTransactions.CalculateHash(active.Transaction, chain)[2..])));
                if (!signature.Signer.Equals(Address, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Подпись не соответствует открытому счету.");
                signatures.Text = (signatures.Text + "\n" + signature.Signer + ":" + signature.Data).Trim();
            }
            finally { credential = credential with { Secret = "" }; }
        });
        Button("Подтвердить предложение в сети (approveHash)", async () =>
        {
            if (proposal is null) throw new InvalidOperationException("Нет предложения.");
            var active = proposal; var owner = Address; var activeClient = client;
            void CurrentProposal() { if (version != epoch || chain != network.ChainId || owner != Address || !ReferenceEquals(activeClient, client) || !ReferenceEquals(active, proposal)) throw new InvalidOperationException("Предложение или кошелек изменились."); RequireConsent(active.Transaction); }
            RequireConsent(active.Transaction); var state = await ReadSafe(active.Transaction.SafeAddress); await CheckSafeHash(active.Transaction, chain); CurrentProposal();
            if (state.Nonce != active.Transaction.Nonce || !state.Owners.Contains(Address, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Nonce изменился или счет не является владельцем.");
            CurrentProposal();
            await Preview(WeightedSafeDrafts.Approve(Address, active.Transaction), "On-chain подтверждение Safe: " + SafeAdvancedTransactions.CalculateHash(active.Transaction, chain), "Подтверждение предложения Safe");
        });
        Button("Проверить подписи и подготовить исполнение", async () =>
        {
            if (proposal is null) throw new InvalidOperationException("Нет предложения.");
            var active = proposal; var owner = Address; var activeClient = client;
            void CurrentProposal() { if (version != epoch || chain != network.ChainId || owner != Address || !ReferenceEquals(activeClient, client) || !ReferenceEquals(active, proposal)) throw new InvalidOperationException("Предложение или кошелек изменились."); RequireConsent(active.Transaction); }
            RequireConsent(active.Transaction); var state = await ReadSafe(active.Transaction.SafeAddress); await CheckSafeHash(active.Transaction, chain); CurrentProposal();
            var entries = (signatures.Text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line => { var parts = line.Split(':', 2); if (parts.Length != 2) throw new ArgumentException("Формат подписи: адрес:0xHEX"); return new SafeSignature(parts[0].Trim(), parts[1].Trim()); }).ToArray();
            if (state.Nonce != active.Transaction.Nonce || entries.Any(s => !state.Owners.Contains(s.Signer, StringComparer.OrdinalIgnoreCase))) throw new InvalidOperationException("Nonce изменился, недостаточно подписей или подпись постороннего счета.");
            SafeQuorum.SignedWeight(state.Members, state.Threshold, entries.Select(e => e.Signer));
            var approvedOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string approvedAbi = """[{"type":"function","name":"approvedHashes","inputs":[{"type":"address"},{"type":"bytes32"}],"outputs":[{"type":"uint256"}]}]""";
            var hash = Convert.FromHexString(SafeAdvancedTransactions.CalculateHash(active.Transaction, chain)[2..]);
            foreach (var entry in entries.Where(e => e.Data == "approved_hash"))
            {
                if (await client.Web3.Eth.GetContract(approvedAbi, active.Transaction.SafeAddress).GetFunction("approvedHashes").CallAsync<BigInteger>(entry.Signer, hash) <= 0) throw new InvalidOperationException("Подтверждение владельца еще не записано в Safe.");
                approvedOwners.Add(entry.Signer);
            }
            foreach (var entry in entries.Where(e => e.Data.StartsWith("contract:", StringComparison.Ordinal)))
            {
                var ownerCode = await client.Web3.Eth.GetCode.SendRequestAsync(entry.Signer);
                if (string.IsNullOrEmpty(ownerCode) || ownerCode == "0x") throw new InvalidOperationException("Contract-подпись требует владельца-контракта.");
            }
            // Target Safe validates its own signature hook (legacy bytes or EIP-1271-compatible handler).
            // A bytes32 magic response alone cannot prove compatibility with this Safe implementation.
            var draft = SafeAdvancedTransactions.Execute(Address, active.Transaction, chain, entries, approvedOwners);
            var simulation = await client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput { From = Address, To = draft.To, Data = draft.Data, Value = new Nethereum.Hex.HexTypes.HexBigInteger(0) });
            if (simulation.Length != 66 || new BigInteger(Convert.FromHexString(simulation[2..]), true, true) != 1) throw new InvalidOperationException("Симуляция Safe не подтвердила успешное внутреннее исполнение.");
            CurrentProposal();
            var proposalSafe = active.Transaction.SafeAddress; var proposalHash = SafeAdvancedTransactions.CalculateHash(active.Transaction, chain);
            await Preview(draft, "Исполнение Safe: " + proposalHash, (active.Transaction.Operation == 1 ? "DELEGATECALL: " : "Safe: ") + active.Transaction.To + " · " + Units.Format(active.Transaction.ValueWei) + " DEL", receiptVerifier: receipt => SafeReceiptVerifier.RequireSuccess(receipt, proposalSafe, proposalHash));
        }, true);
    }
    [FunctionOutput] public sealed class WeightedOwnersOutput : IFunctionOutputDTO
    {
        [Parameter("tuple[]", "owners", 1)] public List<WeightedOwnerDto> Members { get; set; } = [];
    }
    [Struct("Owner")] public sealed class WeightedOwnerDto
    {
        [Parameter("address", "owner", 1)] public string Owner { get; set; } = "";
        [Parameter("uint256", "weight", 2)] public BigInteger Weight { get; set; }
    }
    private async Task CheckSafeHash(SafeTransactionDraft tx, long chain)
    {
        if (tx.Operation == 1)
        {
            var targetCode = await client.Web3.Eth.GetCode.SendRequestAsync(tx.To);
            if (string.IsNullOrEmpty(targetCode) || targetCode == "0x") throw new InvalidOperationException("DelegateCall требует адрес контракта с кодом.");
        }
        const string abi = """
        [{"type":"function","name":"getTransactionHash","inputs":[{"type":"address","name":"to"},{"type":"uint256","name":"value"},{"type":"bytes","name":"data"},{"type":"uint8","name":"operation"},{"type":"uint256","name":"safeTxGas"},{"type":"uint256","name":"baseGas"},{"type":"uint256","name":"gasPrice"},{"type":"address","name":"gasToken"},{"type":"address","name":"refundReceiver"},{"type":"uint256","name":"nonce"}],"outputs":[{"type":"bytes32"}]}]
        """;
        var bytes = await client.Web3.Eth.GetContract(abi, tx.SafeAddress).GetFunction("getTransactionHash").CallAsync<byte[]>(tx.To, tx.ValueWei, Convert.FromHexString(tx.Data[2..]), tx.Operation, tx.SafeTxGas, tx.BaseGas, tx.GasPriceWei, tx.GasToken, tx.RefundReceiver, tx.Nonce);
        if (!("0x" + Convert.ToHexString(bytes)).Equals(SafeAdvancedTransactions.CalculateHash(tx, chain), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Safe использует другой домен или формат хэша. Подпись и исполнение остановлены.");
    }
}
