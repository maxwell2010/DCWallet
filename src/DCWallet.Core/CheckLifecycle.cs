using Decimal.Web3.Checks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
namespace DCWallet.Core;
/// <summary>Ephemeral check signer. Never serialize the bearer key or an unredeemed signature.</summary>
public sealed class CheckSecret : IDisposable
{
    private EthECKey? key = EthECKey.GenerateKey();
    public string Address => Current.GetPublicAddress();
    private EthECKey Current => key ?? throw new ObjectDisposedException(nameof(CheckSecret));
    public static CheckSecret FromProtected(byte[] encrypted,Func<byte[],byte[]> unprotect)
    {
        var bytes=unprotect(encrypted);
        try
        {
            if(bytes.Length!=32)throw new ArgumentException("Invalid encrypted check key.");
            return new CheckSecret { key=new EthECKey((byte[])bytes.Clone(),true) };
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }
    public byte[] ExportProtected(Func<byte[],byte[]> protect)
    {
        var bytes=(byte[])Current.GetPrivateKeyAsBytes().Clone();
        try { return protect(bytes); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }
    public byte[] SignRedeem(byte[] hash, string redeemer)
        => Convert.FromHexString(new MessageSigner().Sign(ChecksService.CalculateRedeemSigningDigest(hash, redeemer), Current).TrimStartHex());
    public void Dispose() { if(key is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(key.GetPrivateKeyAsBytes()); key = null; }
}
internal static class CheckHex { public static string TrimStartHex(this string value) => value.StartsWith("0x") ? value[2..] : value; }
public static class CheckLifecycle
{
    /// <summary>The deployed nonces() view is caller-scoped; omitting From reads the zero-address nonce.</summary>
    public static async Task<System.Numerics.BigInteger> GetNonceForAsync(Decimal.Web3.DecimalClient client,string contract,string creator)
    {
        _=WalletDataReader.Address(contract);_=WalletDataReader.Address(creator);
        if((await client.Web3.Eth.ChainId.SendRequestAsync()).Value!=client.Network.ChainId)throw new InvalidOperationException("Другая сеть RPC.");
        var result=await client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=creator,To=contract,Data="0x93757526"});
        if(result.Length!=66)throw new InvalidDataException("Неверный ответ nonces().");
        return new System.Numerics.BigInteger(Convert.FromHexString(result[2..]),true,true);
    }
    public static byte[] CreatedHash(TransactionReceipt receipt, string contract, string signer)
    {
        if(receipt.Status?.Value != 1) throw new InvalidOperationException("Создание чека не подтверждено.");
        var matches = receipt.DecodeAllEvents<CreatedCheck>().Where(e => e.Log.Address.Equals(contract,StringComparison.OrdinalIgnoreCase) && e.Event.Check.Signer.Equals(signer,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(matches.Length != 1 || matches[0].Event.Hash.Length != 32) throw new InvalidOperationException("Нет единственного события созданного чека с ожидаемым подписантом.");
        return (byte[])matches[0].Event.Hash.Clone();
    }
    public static byte RedeemStatus(TransactionReceipt receipt,string contract,byte[] checkHash)
    {
        if(receipt.Status?.Value != 1) throw new InvalidOperationException("Погашение чека не подтверждено.");
        var events = ChecksReceiptReader.DecodeRedeemed(receipt,contract).Where(e=>e.CheckHash.SequenceEqual(checkHash)).ToArray();
        if(events.Length != 1) throw new InvalidOperationException("Нет единственного события погашения этого чека.");
        return events[0].Status;
    }
    [Event("CreatedCheck")] public sealed class CreatedCheck:IEventDTO
    {
        [Parameter("bytes32","checkHash",1,false)] public byte[] Hash {get;set;}=[];
        [Parameter("tuple","check",2,false)] public CheckTuple Check {get;set;}=new();
    }
    [Struct("Check")] public sealed class CheckTuple
    {
        [Parameter("bytes32","checkDetails",1)] public byte[] Details {get;set;}=[];
        [Parameter("address","signer",2)] public string Signer {get;set;}="";
        [Parameter("uint8","status",3)] public byte Status {get;set;}
    }
}
