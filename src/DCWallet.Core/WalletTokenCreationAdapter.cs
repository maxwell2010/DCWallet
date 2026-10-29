using System.Numerics;
using System.Text.Json;
using Decimal.Web3;
using Decimal.Web3.Transactions;
namespace DCWallet.Core;
public static class WalletTokenCreationAdapter
{
    public const string ReserveHelp="Начальный резерв в wei, без комиссии выпуска. Комиссия читается из Token Center и добавляется отдельно. Значение нужно указать явно; минимальный резерв проверяется контрактом.";
    public const string NonMintableCapHelp="Если дополнительный выпуск отключен (mintable=false), capRaw должен быть 0. Это правило текущего Decimal SDK JS; ненулевой cap отклоняется без скрытого изменения суммы.";
    public static async Task<TransactionDraft> AdaptAsync(Operation operation,string json,DecimalClient client,string from,TransactionDraft draft)
    {
        if(operation.Method.DeclaringType?.FullName!="Decimal.Web3.TokenCreation.TokenCreationDrafts")return draft;
        using var document=JsonDocument.Parse(json);var args=document.RootElement;
        if(operation.Method.Name=="BuildCreateReservelessTokenDraft")
        {
            if(!args.GetProperty("mintable").GetBoolean()&&BigInteger.Parse(args.GetProperty("capRaw").GetString()??"")!=0)throw new ArgumentException(NonMintableCapHelp);
            return draft;
        }
        if(operation.Method.Name!="BuildCreateTokenDraft")return draft;
        if(!args.TryGetProperty("reserveValueWei",out var value)||value.ValueKind!=JsonValueKind.String||!BigInteger.TryParse(value.GetString(),out var reserve)||reserve<=0||reserve>=(BigInteger.One<<256))throw new ArgumentException("Укажите reserveValueWei явно: "+ReserveHelp);
        if(!draft.From.Equals(from,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Отправитель изменился.");
        if((await client.Web3.Eth.ChainId.SendRequestAsync()).Value!=client.Network.ChainId)throw new InvalidOperationException("Другая сеть RPC.");
        if(await client.Web3.Eth.GetCode.SendRequestAsync(draft.To) is "0x" or "")throw new InvalidOperationException("Token Center не найден в выбранной сети.");
        const string abi="[{\"type\":\"function\",\"name\":\"getCommissionSymbol\",\"inputs\":[{\"type\":\"string\",\"name\":\"symbol\"}],\"outputs\":[{\"type\":\"uint256\"}],\"stateMutability\":\"view\"}]";
        var symbol=args.GetProperty("symbol").GetString()??throw new ArgumentException("Нужен symbol.");
        var commission=await client.Web3.Eth.GetContract(abi,draft.To).GetFunction("getCommissionSymbol").CallAsync<BigInteger>(symbol);
        if(commission<0||reserve+commission>=(BigInteger.One<<256))throw new InvalidDataException("Комиссия и резерв не помещаются в uint256.");
        var adapted=draft with{ValueWei=reserve+commission};
        // No guessed MIN_RESERVE and no inherited hardcoded 1250 DEL. Estimate on actual deployment before confirmation.
        await client.EstimateFeeAsync(adapted);
        return adapted;
    }
}
