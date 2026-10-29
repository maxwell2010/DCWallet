using System.Text.Json;
using Decimal.Web3;
using Decimal.Web3.Tokens;
using Decimal.Web3.Transactions;
using Xunit;
namespace DCWallet.Tests;
public sealed class StandReadFactAttribute:FactAttribute
{
    public StandReadFactAttribute() { if(Environment.GetEnvironmentVariable("DC_TESTNET_STAND_READ")!="1") Skip="Explicit public testnet stand read opt-in required"; }
}
public class TestnetStandReadTests
{
    [StandReadFact] public async Task ExistingPublicStandReadsAndSelfTransferQuote()
    {
        if(Environment.GetEnvironmentVariable("DC_TESTNET_STAND_READ")!="1") return;
        const string owner="0xb92bd490c8e9e5d9c46d71666418d4e7ae4534e9",token="0xc310f25e3429efc5ede11a908682f0a31cfc92ce";
        var client=new DecimalClient(DecimalNetwork.Testnet() with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")});
        Assert.Equal(202020,(await client.Web3.Eth.ChainId.SendRequestAsync()).Value);
        var contracts=await client.ResolveSystemContractsAsync(); var balance=await client.GetBalanceWeiAsync(owner);
        var service=new Erc20Service(client,token); var metadata=await service.GetMetadataAsync(); var raw=await service.GetBalanceRawAsync(owner);
        var draft=TransactionDraft.Del(owner,owner,"0.000000000000000001","DC WALLET testnet quote"); var quote=await client.EstimateFeeAsync(draft);
        Assert.True(quote.CanAfford); Assert.True(quote.MaximumFeeWei<=Units.Parse("0.1"));
        var result=new {chainId=202020,owner,balanceDel=Units.Format(balance),token,metadata.Symbol,metadata.Decimals,tokenRaw=raw.ToString(),contracts.DelegationContract,contracts.NftCenterContract,contracts.TokenCenterContract,feeEstimated=quote.EstimatedFeeDel,feeMaximum=quote.MaximumFeeDel,broadcast=false};
        var report=Environment.GetEnvironmentVariable("DC_TESTNET_STAND_READ_REPORT"); if(report is not null) File.WriteAllText(report,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
    }
}
