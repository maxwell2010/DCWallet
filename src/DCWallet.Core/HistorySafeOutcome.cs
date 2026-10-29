using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
namespace DCWallet.Core;
/// <summary>Keep canonical outer transaction success separate from Safe's emitted internal outcome.</summary>
public static class HistorySafeOutcome
{
    static readonly string SuccessTopic="0x"+Sha3Keccack.Current.CalculateHash("ExecutionSuccess(bytes32,uint256)");
    static readonly string FailureTopic="0x"+Sha3Keccack.Current.CalculateHash("ExecutionFailure(bytes32,uint256)");
    public static bool IsExecution(string? input)=>input?.StartsWith("0x6a761202",StringComparison.OrdinalIgnoreCase)==true && HistoryCallClassifier.Classify(input,"")=="Вызов: Исполнение предложения Safe";
    static bool Word(string? value)=>value is {Length:66} && value.StartsWith("0x",StringComparison.OrdinalIgnoreCase) && value.AsSpan(2).ToString().All(Uri.IsHexDigit);
    public static bool? Read(string? input,string? target,TransactionReceipt receipt)
    {
        if(receipt.Status?.Value!=1 || !IsExecution(input) || string.IsNullOrEmpty(target))return null;
        var success=false;
        foreach(var log in receipt.Logs??[])
        {
            var topics=log.Topics?.Select(t=>t?.ToString()).ToArray();
            if(!target.Equals(log.Address,StringComparison.OrdinalIgnoreCase)||topics is not {Length:2}||!Word(topics[0])||!Word(topics[1])||!Word(log.Data))continue;
            if(topics[0]!.Equals(FailureTopic,StringComparison.OrdinalIgnoreCase))return false;
            if(topics[0]!.Equals(SuccessTopic,StringComparison.OrdinalIgnoreCase))success=true;
        }
        return success?true:null;
    }
    public static string DisplayStatus(HistoryRow row)=>!row.Success?"Ошибка":row.InnerSuccess==false?"Ошибка внутри Safe":row.InnerSuccess==true?"Исполнено внутри Safe":row.Kind.StartsWith("Safe:",StringComparison.Ordinal)?"Внешняя транзакция подтверждена":"Исполнено";
}
