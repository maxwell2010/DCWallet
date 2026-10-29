using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Tokens;
using Decimal.Web3.TokenEconomy;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
namespace DCWallet.Tests;
/// <summary>Explicit root-only isolated reserve A -> B -> DEL roundtrip. No test attribute, no implicit dispatch.</summary>
public static class SwapConvertNetworkScenario
{
    private const string A="0x37748f3e857a71e4334125a935052ce1e603b7b5", B="0x66efc62bbb7b0f2289b4330fa012aa6335d6bb7d", Center="0xb8f9c4a26b91a65ac0d3f34e230a4c9ad09aef4a";
    public static async Task RunAsync(AdvancedTestContext ctx,bool permit=false)
    {
        Assert.Equal(202020,ctx.Network.ChainId);Assert.Equal(202020,(await ctx.Client.Web3.Eth.ChainId.SendRequestAsync()).Value);
        Assert.Equal(Center,ctx.Network.TokenCenterContract.ToLowerInvariant());
        var ercA=new Erc20Service(ctx.Client,A);var ercB=new Erc20Service(ctx.Client,B);var reserveA=new ReservableTokenService(ctx.Client,A);var reserveB=new ReservableTokenService(ctx.Client,B);var economy=new TokenEconomyDrafts(Center);
        var oldA=await ercA.GetBalanceRawAsync(ctx.Owner);var oldB=await ercB.GetBalanceRawAsync(ctx.Owner);var allowanceA=await ercA.GetAllowanceRawAsync(ctx.Owner,Center);var allowanceB=await ercB.GetAllowanceRawAsync(ctx.Owner,Center);
        if(allowanceA==(BigInteger.One<<256)-1)throw new InvalidOperationException("Do not replace/restore an infinite baseline allowance in this QA roundtrip.");
        var head=await ctx.Client.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();var block=new BlockParameter(head);
        foreach(var token in new[]{A,B}){Assert.NotEqual("0x",await ctx.Client.Web3.Eth.GetCode.SendRequestAsync(token,block));var r=new ReservableTokenService(ctx.Client,token);Assert.True(await r.GetReserveWeiAsync(block)>0);var crr=await r.GetCrrAsync(block);Assert.InRange(crr,BigInteger.One,new BigInteger(100));}
        var input=Units.Parse("0.001");var quoteA=await reserveA.CalculateBuyOutputRawAsync(input,block);Assert.True(quoteA>0);var minA=quoteA*99/100;Assert.True(minA>0);
        ctx.Note("reserve convert original asset and allowance baselines",new{A,B,Center,balanceA=oldA.ToString(),balanceB=oldB.ToString(),allowanceA=allowanceA.ToString(),allowanceB=allowanceB.ToString(),buyDel="0.001",permit});
        var bought=await ctx.SendAsync("convert buy only new QA amount of reserve A",economy.BuildBuyTokenForExactDelDraft(ctx.Owner,A,input,minA,ctx.Owner));
        var newA=await ercA.GetBalanceRawAsync(ctx.Owner)-oldA;Assert.True(newA>=minA);Assert.Equal(newA,NetTransfer(bought,A,ctx.Owner));Assert.Equal(oldB,await ercB.GetBalanceRawAsync(ctx.Owner));ctx.Note("convert isolated new A balance delta",new{amountRaw=newA.ToString()});
        if(!permit)await ctx.SendAsync("convert approve exact newly bought A",ercA.BuildApproveDraft(ctx.Owner,Center,newA));
        var quoteHead=await ctx.Client.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();var pinned=new BlockParameter(quoteHead);var beforeBlock=await ctx.Client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(pinned);
        var del=await reserveA.CalculateSellOutputWeiAsync(newA,pinned);var quoteB=await reserveB.CalculateBuyOutputRawAsync(del,pinned);Assert.True(del>0&&quoteB>0);var minB=quoteB*99/100;Assert.True(minB>0);var afterBlock=await ctx.Client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(pinned);Assert.Equal(beforeBlock.BlockHash,afterBlock.BlockHash);
        TransactionDraft Convert(BigInteger minimum)=>economy.BuildConvertDraft(ctx.Owner,A,B,newA,minimum,ctx.Owner);
        var impossible=Convert((BigInteger.One<<256)-1);
        if(permit){var negativePermit=await ctx.WalletSigner.SignPermitAsync(ctx.Client,A,Center,newA,DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());impossible=economy.BuildConvertWithPermitDraft(ctx.Owner,A,B,newA,(BigInteger.One<<256)-1,ctx.Owner,negativePermit.Deadline,negativePermit.V,negativePermit.R,negativePermit.S);}
        var rejected=false;
        try{await ctx.Client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=ctx.Owner,To=Center,Data=impossible.Data});}catch(Nethereum.JsonRpc.Client.RpcResponseException e)when(e.Message.Contains("revert",StringComparison.OrdinalIgnoreCase)){rejected=true;}Assert.True(rejected);
        TransactionDraft draft;
        if(permit){var signature=await ctx.WalletSigner.SignPermitAsync(ctx.Client,A,Center,newA,DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());draft=economy.BuildConvertWithPermitDraft(ctx.Owner,A,B,newA,minB,ctx.Owner,signature.Deadline,signature.V,signature.R,signature.S);}
        else draft=Convert(minB);
        ctx.Note("convert quotes pinned to one canonical block",new{block=quoteHead.Value.ToString(),sellAWei=del.ToString(),buyBRaw=quoteB.ToString(),minBRaw=minB.ToString(),negativeImpossibleMinRejected=rejected});
        var converted=await ctx.SendAsync("reserve A to B actual convert"+(permit?" permit":" exact allowance"),draft);Assert.Equal(oldA,await ercA.GetBalanceRawAsync(ctx.Owner));var newB=await ercB.GetBalanceRawAsync(ctx.Owner)-oldB;Assert.True(newB>=minB);Assert.Equal(-newA,NetTransfer(converted,A,ctx.Owner));Assert.Equal(newB,NetTransfer(converted,B,ctx.Owner));
        var sellQuote=await reserveB.CalculateSellOutputWeiAsync(newB);Assert.True(sellQuote>0);var sellMin=sellQuote*99/100;Assert.True(sellMin>0);
        var delBeforeSell=await ctx.Client.GetBalanceWeiAsync(ctx.Owner);
        var sold=await ctx.SendAsync("convert sell only newly received reserve B back to DEL",economy.BuildSellExactTokensForDelDraft(ctx.Owner,B,newB,sellMin,ctx.Owner));Assert.Equal(-newB,NetTransfer(sold,B,ctx.Owner));
        var soldTransaction=await ctx.Client.Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(sold.TransactionHash);var paidFee=sold.GasUsed.Value*(sold.EffectiveGasPrice?.Value??soldTransaction.GasPrice.Value);Assert.True(await ctx.Client.GetBalanceWeiAsync(ctx.Owner)+paidFee-delBeforeSell>=sellMin);
        Assert.Equal(oldA,await ercA.GetBalanceRawAsync(ctx.Owner));Assert.Equal(oldB,await ercB.GetBalanceRawAsync(ctx.Owner));
        if(await ercA.GetAllowanceRawAsync(ctx.Owner,Center)!=allowanceA)await ctx.SendAsync("convert restore finite original A allowance",ercA.BuildApproveDraft(ctx.Owner,Center,allowanceA));Assert.Equal(allowanceA,await ercA.GetAllowanceRawAsync(ctx.Owner,Center));Assert.Equal(allowanceB,await ercB.GetAllowanceRawAsync(ctx.Owner,Center));
        ctx.Note("convert roundtrip preserved all pre-existing tokens and allowances",new{A,B,consumedOnlyNewA=newA.ToString(),soldOnlyNewB=newB.ToString(),minimumDelReturned=sellMin.ToString(),permit});
    }
    private static BigInteger NetTransfer(TransactionReceipt receipt,string token,string owner)=>receipt.DecodeAllEvents<Transfer>().Where(e=>e.Log.Address.Equals(token,StringComparison.OrdinalIgnoreCase)).Aggregate(BigInteger.Zero,(n,e)=>n+(e.Event.To.Equals(owner,StringComparison.OrdinalIgnoreCase)?e.Event.Value:0)-(e.Event.From.Equals(owner,StringComparison.OrdinalIgnoreCase)?e.Event.Value:0));
    [Event("Transfer")]public sealed class Transfer:IEventDTO
    {[Parameter("address","from",1,true)]public string From{get;set;}="";[Parameter("address","to",2,true)]public string To{get;set;}="";[Parameter("uint256","value",3,false)]public BigInteger Value{get;set;}}
}
