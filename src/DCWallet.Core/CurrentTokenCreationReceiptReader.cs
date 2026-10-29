using Decimal.Web3.TokenCreation;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
namespace DCWallet.Core;
public sealed record CurrentCreatedToken(string Address,bool IsReserveless,string? Creator=null,string? Name=null,string? Symbol=null);
/// <summary>Wallet compatibility for current tuple metadata events and earlier address-only events. No transaction is submitted.</summary>
public static class CurrentTokenCreationReceiptReader
{
    public static IReadOnlyList<CurrentCreatedToken> Read(TransactionReceipt receipt,string tokenCenter,string? expectedCreator=null)
    {
        _=WalletDataReader.Address(tokenCenter);if(expectedCreator is not null)_=WalletDataReader.Address(expectedCreator);
        if(receipt.Status?.Value!=1)throw new InvalidOperationException("Создание токена не подтверждено успешной квитанцией.");
        var result=new List<CurrentCreatedToken>();
        foreach(var decoded in receipt.DecodeAllEvents<WithMetadata>().Where(e=>e.Log.Address.Equals(tokenCenter,StringComparison.OrdinalIgnoreCase)))
        {
            var item=decoded.Event;_=WalletDataReader.Address(item.Address);_=WalletDataReader.Address(item.Meta.Creator);
            if(expectedCreator is not null&&!item.Meta.Creator.Equals(expectedCreator,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Создатель в событии TokenDeployed не совпадает с ожидаемым.");
            result.Add(new(item.Address,false,item.Meta.Creator,item.Meta.Name,item.Meta.Symbol));
        }
        foreach(var item in TokenCreationReceiptReader.DecodeReceipt(receipt,tokenCenter))
        {
            if(expectedCreator is not null)throw new InvalidOperationException("Старое событие создания не содержит создателя. Проверка по этому событию невозможна.");
            result.Add(new(item.Address,item.IsReserveless));
        }
        if(result.Count==0)throw new InvalidOperationException("В квитанции нет события создания от ожидаемого Token Center.");
        if(result.Select(r=>r.Address.ToLowerInvariant()).Distinct().Count()!=result.Count)throw new InvalidOperationException("Дублированный адрес созданного токена в квитанции.");
        return result;
    }
    [Event("TokenDeployed")]public sealed class WithMetadata:IEventDTO
    {[Parameter("address","tokenAddress",1,false)]public string Address{get;set;}="";[Parameter("tuple","meta",2,false)]public TokenCreationMeta Meta{get;set;}=new();}
}
