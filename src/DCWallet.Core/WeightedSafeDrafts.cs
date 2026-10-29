using System.Numerics;
using Decimal.Web3.Transactions;
using Decimal.Web3.Multisig;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;
namespace DCWallet.Core;
/// <summary>Decimal Console profile sdk_extensions_v1.json, mainnet75. Runtime code must be pinned before preparing.</summary>
public static class WeightedSafeDrafts
{
    public static string CreatedAddress(TransactionReceipt receipt)
    {
        if (receipt.Status?.Value != 1) throw new InvalidOperationException("Создание Safe не подтверждено как успешное.");
        var creation = receipt.DecodeAllEvents<ProxyCreation>().Where(e => e.Log.Address.Equals(Factory, StringComparison.OrdinalIgnoreCase) && e.Event.Singleton.Equals(Implementation, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (creation.Length != 1 || !System.Text.RegularExpressions.Regex.IsMatch(creation[0].Event.Proxy, "^0x[0-9a-fA-F]{40}$")) throw new InvalidOperationException("Нет однозначного события ProxyCreation проверенной фабрики Safe.");
        return creation[0].Event.Proxy;
    }
    [Event("ProxyCreation")] public sealed class ProxyCreation : IEventDTO
    {
        [Parameter("address", "proxy", 1, true)] public string Proxy { get; set; } = "";
        [Parameter("address", "singleton", 2, false)] public string Singleton { get; set; } = "";
    }
    public static TransactionDraft Approve(string owner, SafeTransactionDraft tx)
    {
        var message = new ApproveHash { Transaction = new TransactionTuple { To = tx.To, Value = tx.ValueWei, Data = Convert.FromHexString(tx.Data[2..]), Operation = tx.Operation, SafeTxGas = tx.SafeTxGas, BaseGas = tx.BaseGas, GasPrice = tx.GasPriceWei, GasToken = tx.GasToken, RefundReceiver = tx.RefundReceiver, Nonce = tx.Nonce } };
        var encoded = new FunctionMessageEncodingService<ApproveHash>(tx.SafeAddress, owner).GetCallData(message);
        return TransactionDraft.ContractCall(owner, tx.SafeAddress, "0x" + Convert.ToHexString(encoded).ToLowerInvariant());
    }
    [Function("approveHash")] public sealed class ApproveHash : FunctionMessage
    {
        [Parameter("tuple", "transactionData", 1)] public TransactionTuple Transaction { get; set; } = new();
    }
    [Struct("TransactionData")] public sealed class TransactionTuple
    {
        [Parameter("address", "to", 1)] public string To { get; set; } = "";
        [Parameter("uint256", "value", 2)] public BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)] public byte[] Data { get; set; } = [];
        [Parameter("uint8", "operation", 4)] public byte Operation { get; set; }
        [Parameter("uint256", "safeTxGas", 5)] public BigInteger SafeTxGas { get; set; }
        [Parameter("uint256", "baseGas", 6)] public BigInteger BaseGas { get; set; }
        [Parameter("uint256", "gasPrice", 7)] public BigInteger GasPrice { get; set; }
        [Parameter("address", "gasToken", 8)] public string GasToken { get; set; } = "";
        [Parameter("address", "refundReceiver", 9)] public string RefundReceiver { get; set; } = "";
        [Parameter("uint256", "nonce", 10)] public BigInteger Nonce { get; set; }
    }
    public static TransactionDraft Execute(string executor, SafeTransactionDraft tx, BigInteger chain, IReadOnlyList<SafeSignature> signatures, IReadOnlySet<string> approvedOwners)
    {
        SafeTransactions.CalculateHash(tx, chain);
        var ordered = signatures.OrderBy(s => s.Signer, StringComparer.OrdinalIgnoreCase).ToArray();
        if (ordered.Length == 0 || ordered.Select(s => s.Signer).Distinct(StringComparer.OrdinalIgnoreCase).Count() != ordered.Length) throw new ArgumentException("Нужны уникальные подписи владельцев.");
        var packed = new List<byte>();
        foreach (var signature in ordered)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(signature.Signer, "^0x[0-9a-fA-F]{40}$")) throw new ArgumentException("Некорректный адрес подписи.");
            if (signature.Data == "approved_hash")
            {
                if (!approvedOwners.Contains(signature.Signer)) throw new ArgumentException("On-chain подтверждение владельца не проверено.");
                var raw = new byte[65]; Convert.FromHexString(signature.Signer[2..]).CopyTo(raw, 12); raw[64] = 1; packed.AddRange(raw);
            }
            else packed.AddRange(Convert.FromHexString(SafeTransactions.PackEoaSignatures(tx, chain, [signature])[2..]));
        }
        var message = new SafeTransactions.SafeExecTransaction { To = tx.To, Value = tx.ValueWei, Data = Convert.FromHexString(tx.Data[2..]), Operation = tx.Operation, SafeTxGas = tx.SafeTxGas, BaseGas = tx.BaseGas, SafeGasPrice = tx.GasPriceWei, GasToken = tx.GasToken, RefundReceiver = tx.RefundReceiver, Signatures = packed.ToArray() };
        var encoded = new FunctionMessageEncodingService<SafeTransactions.SafeExecTransaction>(tx.SafeAddress, executor).GetCallData(message);
        return TransactionDraft.ContractCall(executor, tx.SafeAddress, "0x" + Convert.ToHexString(encoded).ToLowerInvariant());
    }
    public const string Implementation = "0x15949c33775154549d073168c1094c5f3b28b5cb", Factory = "0x92466f09d5c82e8ddb8aaa7c5adc63d43111f6c1";
    public const string ImplementationSha256 = "23413b3f39372f34ba7d3edb306ac16dd7e55e6dcff93b4e9697c7f9750e2601", FactorySha256 = "a74bdd377b7e91b67d2af0166c25534664b93b9a89fd60b1a15608c5b5d87fed";
    public static TransactionDraft Create(string from, IReadOnlyList<SafeMember> members, BigInteger threshold, BigInteger salt, long chainId)
    {
        if (chainId != 75) throw new NotSupportedException("Фабрика Decimal Safe подтверждена только для mainnet75.");
        if (members.Count > 20) throw new ArgumentException("Для создания Safe поддерживается до 20 владельцев.");
        SafeQuorum.Validate(members, threshold);
        if (salt < 0 || salt >= (BigInteger.One << 256)) throw new ArgumentException("Salt должен быть uint256.");
        var setup = new SafeSetup { Owners = members.Select(m => new WeightedOwner { Owner = m.Owner, Weight = m.Weight }).ToList(), Threshold = threshold };
        var init = new FunctionMessageEncodingService<SafeSetup>(Implementation, from).GetCallData(setup);
        var data = new FunctionMessageEncodingService<SafeCreate>(Factory, from).GetCallData(new SafeCreate { Singleton = Implementation, Initializer = init, Salt = salt });
        return TransactionDraft.ContractCall(from, Factory, "0x" + Convert.ToHexString(data).ToLowerInvariant());
    }
    [Struct("Owner")] public sealed class WeightedOwner
    {
        [Parameter("address", "owner", 1)] public string Owner { get; set; } = "";
        [Parameter("uint256", "weight", 2)] public BigInteger Weight { get; set; }
    }
    [Function("setup")] public sealed class SafeSetup : FunctionMessage
    {
        [Parameter("tuple[]", "owners", 1)] public List<WeightedOwner> Owners { get; set; } = [];
        [Parameter("uint256", "threshold", 2)] public BigInteger Threshold { get; set; }
        [Parameter("address", "to", 3)] public string To { get; set; } = TransactionDraft.BurnAddress;
        [Parameter("bytes", "data", 4)] public byte[] Data { get; set; } = [];
        [Parameter("address", "fallbackHandler", 5)] public string Handler { get; set; } = TransactionDraft.BurnAddress;
        [Parameter("address", "paymentToken", 6)] public string PaymentToken { get; set; } = TransactionDraft.BurnAddress;
        [Parameter("uint256", "payment", 7)] public BigInteger Payment { get; set; }
        [Parameter("address", "paymentReceiver", 8)] public string PaymentReceiver { get; set; } = TransactionDraft.BurnAddress;
    }
    [Function("createProxyWithNonce", "address")] public sealed class SafeCreate : FunctionMessage
    {
        [Parameter("address", "singleton", 1)] public string Singleton { get; set; } = "";
        [Parameter("bytes", "initializer", 2)] public byte[] Initializer { get; set; } = [];
        [Parameter("uint256", "saltNonce", 3)] public BigInteger Salt { get; set; }
    }
}
