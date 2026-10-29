using System.Numerics;
using System.Reflection;
using Decimal.Web3;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
namespace DCWallet.Core;
/// <summary>Calldata intent hints, not contract identity, receipt success or decoded asset movements.</summary>
public static class HistoryCallClassifier
{
    static readonly IReadOnlyDictionary<string,FunctionABI> Functions=Build();
    static string? Description(string name)=>name switch
    {
        "transfer" or "transferFrom" or "safeTransferFrom" or "safeBatchTransferFrom"=>"Перевод актива",
        "approve"=>"Разрешение на актив", "setApprovalForAll"=>"Разрешение оператору активов",
        "buy" or "buyTokenForExactDEL" or "buyExactTokenForDEL"=>"Покупка токена",
        "sell" or "sellExactTokensForDEL" or "sellTokensForExactDEL"=>"Продажа токена",
        "convert"=>"Обмен токенов", "mint" or "mintByETH" or "mintByDEL"=>"Выпуск актива", "burn"=>"Сжигание актива",
        "createChecksDEL" or "createChecksToken" or "createChecksTokenByPermit"=>"Создание чеков", "redeemChecks"=>"Погашение чеков",
        "execTransaction"=>"Исполнение предложения Safe", "approveHash"=>"Подтверждение предложения Safe", "createProxyWithNonce"=>"Создание Safe",
        "aggregate" or "sendToken" or "sendTokenWithPermit"=>"Рассылка активов",
        "createERC721" or "createERC1155" or "createDRC721" or "createDRC1155"=>"Создание коллекции NFT",
        "createToken" or "createTokenReserveless" or "createReservelessToken"=>"Создание токена",
        "addCandidate" or "addCandidateDEL"=>"Создание кандидата в валидаторы",
        "addValidator" or "addValidatorDEL" or "addValidators"=>"Регистрация валидатора", "approveCandidates"=>"Подтверждение кандидатов",
        "pauseSelf" or "pauseValidator"=>"Приостановка валидатора", "unpauseSelf" or "unpauseValidator"=>"Возобновление валидатора",
        "removeValidator"=>"Удаление валидатора", "updateValidatorMeta"=>"Изменение описания валидатора", "addPenalty"=>"Штраф валидатора",
        "updateDetails" or "updateIdentity"=>"Изменение описания актива",
        "complete"=>"Завершение вывода стейка",
        _ when name.StartsWith("delegate",StringComparison.Ordinal)=>"Делегирование",
        _ when name.StartsWith("withdraw",StringComparison.Ordinal)=>"Заявка на вывод стейка",
        "transferStake" or "transferStakeWithReset" or "transferWithReset" or "transferNFT" or "transferNFTHold" or "transferNFT1155" or "transferNFT1155Hold"=>"Перенос стейка",
        _ when name.StartsWith("hold",StringComparison.Ordinal)||name.StartsWith("resetHold",StringComparison.Ordinal)=>"Изменение срока стейка",
        _=>null
    };
    static IReadOnlyDictionary<string,FunctionABI> Build()
    {
        var functions=new List<FunctionABI>();var extractor=new AttributesToABIExtractor();
        foreach(var type in new[]{typeof(DecimalClient).Assembly,typeof(HistoryCallClassifier).Assembly}.SelectMany(a=>a.GetTypes()))
        {
            var attribute=type.GetCustomAttribute<FunctionAttribute>();if(attribute is null||Description(attribute.Name) is null)continue;
            try{functions.Add(extractor.ExtractFunctionABI(type));}catch(ArgumentException){/* Unsupported DTO types are not guessed. */}
        }
        void Add(string name,params string[] types)=>functions.Add(new FunctionABI(name,false,false){InputParameters=types.Select((t,i)=>new Parameter(t,"arg"+i,i+1)).ToArray()});
        Add("approve","address","uint256");Add("transfer","address","uint256");Add("transferFrom","address","address","uint256");Add("setApprovalForAll","address","bool");
        Add("delegateNFT1155","address","address","uint256","uint256");Add("withdrawNFT1155","address","address","uint256");
        Add("mintByDEL","address","string");Add("mintByDEL","address","uint256","uint256","string");
        foreach(var erc721 in new[]{true,false})
        {
            var suffix=erc721?"":"1155";var head=new[]{"address","address","uint256"};
            Add("delegateNFT"+suffix,erc721?head:head.Concat(new[]{"uint256"}).ToArray());
            Add("delegateNFT"+suffix+"Hold",head.Concat(erc721?new[]{"uint256"}:new[]{"uint256","uint256"}).ToArray());
            Add("withdrawNFT"+suffix,head);Add("withdrawNFT"+suffix+"Hold",head.Concat(new[]{"uint256"}).ToArray());
            Add("transferNFT"+suffix,head.Concat(new[]{"address"}).ToArray());Add("transferNFT"+suffix+"Hold",head.Concat(new[]{"uint256","address"}).ToArray());
        }
        return functions.GroupBy(f=>f.Sha3Signature,StringComparer.OrdinalIgnoreCase).Where(g=>g.Select(f=>Description(f.Name)).Distinct().Count()==1).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
    }
    public static string Classify(string? input,string fallback,bool successful=true)
    {
        if(!successful || input is null || input.Length is <10 or >131082 || !input.StartsWith("0x",StringComparison.OrdinalIgnoreCase) || input.Length%2!=0 || !input.AsSpan(2).ToString().All(Uri.IsHexDigit))return fallback;
        if(!Functions.TryGetValue(input.Substring(2,8),out var function))return fallback;
        var parameters=function.InputParameters;
        var payload=Convert.FromHexString(input[10..]);
        if(payload.Length%32!=0)return fallback;
        var headSize=parameters.Sum(p=>p.ABIType.IsDynamic()?32:p.ABIType.FixedSize);
        if(payload.Length<headSize)return fallback;
        var position=0;
        foreach(var parameter in parameters)
        {
            if(parameter.ABIType.IsDynamic())
            {
                var offset=new BigInteger(payload.AsSpan(position,32),true,true);
                if(offset<headSize||offset>payload.Length-32||offset%32!=0)return fallback;
                // Bound array/string/bytes lengths before using them; do not decode untrusted nested values.
                if(parameter.Type.EndsWith("[]",StringComparison.Ordinal)||parameter.Type is "bytes" or "string")
                {
                    var length=new BigInteger(payload.AsSpan((int)offset,32),true,true);
                    var itemSize=parameter.Type.EndsWith("[]",StringComparison.Ordinal)?32:1;
                    if(length>payload.Length || length*itemSize>payload.Length-(int)offset-32)return fallback;
                }
            }
            else if(parameter.ABIType.FixedSize==32)
            {
                var word=payload.AsSpan(position,32);
                if(parameter.Type=="address" && word[..12].ToArray().Any(b=>b!=0))return fallback;
                if(parameter.Type=="bool" && new BigInteger(word,true,true)>1)return fallback;
                if(parameter.Type.StartsWith("uint",StringComparison.Ordinal) && int.TryParse(parameter.Type[4..],out var bits) && bits<256 && new BigInteger(word,true,true)>=(BigInteger.One<<bits))return fallback;
            }
            position+=parameter.ABIType.IsDynamic()?32:parameter.ABIType.FixedSize;
        }
        return "Вызов: "+Description(function.Name);
    }
}
