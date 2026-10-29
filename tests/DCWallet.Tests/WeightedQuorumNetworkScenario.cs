using System.Numerics;
using DCWallet.Core;
using Decimal.Web3.Multisig;
using Decimal.Web3.Transactions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
namespace DCWallet.Tests;
public static class WeightedQuorumNetworkScenario
{
    public static async Task RunAsync(AdvancedTestContext ctx)
    {
        await TestnetSafeProfile.VerifyAsync(ctx.Client);var second=ctx.OwnerAt(1);
        Assert.NotEqual(ctx.Owner.ToLowerInvariant(),second.ToLowerInvariant());
        var receipt=await ctx.SendAsync("Safe quorum create two owners weights2+1 threshold3",TestnetSafeProfile.Create(ctx.Owner,[new(ctx.Owner,2),new(second,1)],3,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),202020));
        var safe=TestnetSafeProfile.CreatedAddress(receipt);
        const string abi="""[{"type":"function","name":"getWeightThreshold","inputs":[],"outputs":[{"type":"uint256"}]}]""";
        Assert.Equal(3,await ctx.Client.Web3.Eth.GetContract(abi,safe).GetFunction("getWeightThreshold").CallAsync<BigInteger>());
        var members=await ctx.Client.Web3.Eth.GetContractQueryHandler<SafeNetworkScenario.Owners>().QueryDeserializingToObjectAsync<SafeNetworkScenario.OwnersOutput>(new(),safe);
        Assert.Equal(2,members.Members.Count);Assert.Contains(members.Members,m=>m.Owner.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)&&m.Weight==2);Assert.Contains(members.Members,m=>m.Owner.Equals(second,StringComparison.OrdinalIgnoreCase)&&m.Weight==1);
        ctx.Note("Safe multiowner weighted profile",new{safe,owners=new[]{new{address=ctx.Owner,weight=2},new{address=second,weight=1}},threshold=3});
        await ctx.SendAsync("Safe quorum fund one wei",TransactionDraft.Del(ctx.Owner,safe,"0.000000000000000001"));
        var proposal=SafeTransactionDrafts.BuildDel(safe,ctx.Owner,1,0);var queries=new SafeQueries(ctx.Client.Web3,safe,ctx.Network);
        Assert.Equal(SafeTransactions.CalculateHash(proposal,202020),await queries.GetTransactionHashAsync(proposal),StringComparer.OrdinalIgnoreCase);
        var insufficient=SafeTransactions.BuildExecutionDraft(ctx.Owner,proposal,202020,[ctx.SignSafe(proposal)]);
        var rejected=false;
        try{await ctx.Client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=ctx.Owner,To=safe,Data=insufficient.Data,Value=new HexBigInteger(0)});}
        catch(Nethereum.JsonRpc.Client.RpcResponseException e)when(e.Message.Contains("revert",StringComparison.OrdinalIgnoreCase)){rejected=true;}
        Assert.True(rejected,"One owner with weight2 must not satisfy threshold3.");
        Assert.Equal(0,await queries.GetNonceAsync());Assert.Equal(BigInteger.One,(await ctx.Client.Web3.Eth.GetBalance.SendRequestAsync(safe)).Value);
        var draft=SafeTransactions.BuildExecutionDraft(ctx.Owner,proposal,202020,[ctx.SignSafe(proposal,1),ctx.SignSafe(proposal)]);
        var raw=await ctx.Client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=ctx.Owner,To=safe,Data=draft.Data,Value=new HexBigInteger(0)});Assert.Equal(BigInteger.One,new BigInteger(Convert.FromHexString(raw[2..]),true,true));
        receipt=await ctx.SendAsync("Safe quorum execute two EOA signatures",draft);SafeReceiptVerifier.RequireSuccess(receipt,safe,SafeTransactions.CalculateHash(proposal,202020));
        Assert.Equal(1,await queries.GetNonceAsync());Assert.Equal(BigInteger.Zero,(await ctx.Client.Web3.Eth.GetBalance.SendRequestAsync(safe)).Value);
        ctx.Note("Safe multiowner quorum confirmed",new{safe,threshold=3,insufficientWeightRejected=2,twoSignaturesSucceeded=true,nonce=1,balanceWei="0"});
    }
}
