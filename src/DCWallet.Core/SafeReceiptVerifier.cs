using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
namespace DCWallet.Core;
/// <summary>Outer EVM success alone does not prove a Safe's internal action succeeded.</summary>
public static class SafeReceiptVerifier
{
    public static void RequireSuccess(TransactionReceipt receipt,string safeAddress,string proposalHash)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        _=WalletDataReader.Address(safeAddress);
        if(proposalHash is null || proposalHash.Length!=66 || !proposalHash.StartsWith("0x",StringComparison.OrdinalIgnoreCase) || !proposalHash[2..].All(Uri.IsHexDigit)) throw new ArgumentException("Некорректный хеш предложения Safe.",nameof(proposalHash));
        if(receipt.Status?.Value != 1) throw new InvalidOperationException("Внешняя транзакция Safe не подтверждена как успешная.");
        bool Matches(string address,byte[] hash)=>address.Equals(safeAddress,StringComparison.OrdinalIgnoreCase) && ("0x"+Convert.ToHexString(hash)).Equals(proposalHash,StringComparison.OrdinalIgnoreCase);
        // Failure wins even if a malformed or unexpected receipt contains both result events.
        if(receipt.DecodeAllEvents<SafeExecutionFailure>().Any(e=>Matches(e.Log.Address,e.Event.TransactionHash)))
            throw new InvalidOperationException("Safe подтвердил ошибку внутреннего действия. Внешняя комиссия оплачена, операция Safe не выполнена.");
        if(!receipt.DecodeAllEvents<SafeExecutionSuccess>().Any(e=>Matches(e.Log.Address,e.Event.TransactionHash)))
            throw new InvalidOperationException("Нет события успешного внутреннего действия Safe для этого предложения. Проверьте транзакцию в обозревателе.");
    }
    [Event("ExecutionSuccess")]
    public sealed class SafeExecutionSuccess:IEventDTO
    {
        [Parameter("bytes32","txHash",1,true)]public byte[] TransactionHash{get;set;}=[];
        [Parameter("uint256","payment",2,false)]public System.Numerics.BigInteger Payment{get;set;}
    }
    [Event("ExecutionFailure")]
    public sealed class SafeExecutionFailure:IEventDTO
    {
        [Parameter("bytes32","txHash",1,true)]public byte[] TransactionHash{get;set;}=[];
        [Parameter("uint256","payment",2,false)]public System.Numerics.BigInteger Payment{get;set;}
    }
}
