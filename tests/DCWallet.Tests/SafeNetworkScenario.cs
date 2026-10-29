using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Decimal.Web3.Transactions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
namespace DCWallet.Tests;
public static class SafeNetworkScenario
{
    const string Abi="""
    [{"type":"function","name":"getWeightThreshold","inputs":[],"outputs":[{"type":"uint256"}]},
    {"type":"function","name":"getOwnersWithWeights","inputs":[],"outputs":[{"type":"tuple[]","components":[{"type":"address","name":"owner"},{"type":"uint256","name":"weight"}]}]},
    {"type":"function","name":"approvedHashes","inputs":[{"type":"address"},{"type":"bytes32"}],"outputs":[{"type":"uint256"}]}]
    """;
    public static async Task RunAsync(AdvancedTestContext ctx)
    {
        await TestnetSafeProfile.VerifyAsync(ctx.Client);
        var created=await ctx.SendAsync("Safe create weighted QA owner",TestnetSafeProfile.Create(ctx.Owner,[new(ctx.Owner,2)],2,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),202020));
        var safe=TestnetSafeProfile.CreatedAddress(created);ctx.Note("Safe created",new{safe,singleton=TestnetSafeProfile.Singleton,factory=TestnetSafeProfile.Factory});
        var contract=ctx.Client.Web3.Eth.GetContract(Abi,safe);Assert.Equal(2,await contract.GetFunction("getWeightThreshold").CallAsync<BigInteger>());
        var members=await ctx.Client.Web3.Eth.GetContractQueryHandler<Owners>().QueryDeserializingToObjectAsync<OwnersOutput>(new(),safe);
        var owner=Assert.Single(members.Members);Assert.Equal(ctx.Owner,owner.Owner,StringComparer.OrdinalIgnoreCase);Assert.Equal(2,owner.Weight);
        await ctx.SendAsync("Safe fund QA 0.001 DEL",TransactionDraft.Del(ctx.Owner,safe,"0.001"));
        var queries=new SafeQueries(ctx.Client.Web3,safe,ctx.Client.Network);Assert.Equal(0,await queries.GetNonceAsync());
        async Task CheckHash(SafeTransactionDraft proposal)
        {
            Assert.Equal(SafeTransactions.CalculateHash(proposal,202020),await queries.GetTransactionHashAsync(proposal),StringComparer.OrdinalIgnoreCase);
        }
        async Task Simulate(TransactionDraft draft,bool expected)
        {
            var raw=await ctx.Client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=draft.From,To=draft.To,Data=draft.Data,Value=new HexBigInteger(0)});
            Assert.Equal(expected,BigInteger.Parse("0"+raw[2..],System.Globalization.NumberStyles.HexNumber)==1);
        }
        var first=SafeTransactionDrafts.BuildDel(safe,ctx.Owner,Units.Parse("0.0004"),0);await CheckHash(first);
        var signed=ctx.SignSafe(first);var execution=SafeTransactions.BuildExecutionDraft(ctx.Owner,first,202020,[signed]);await Simulate(execution,true);
        var receipt=await ctx.SendAsync("Safe EOA execute return 0.0004 DEL",execution);SafeReceiptVerifier.RequireSuccess(receipt,safe,SafeTransactions.CalculateHash(first,202020));Assert.Equal(1,await queries.GetNonceAsync());
        var second=SafeTransactionDrafts.BuildDel(safe,ctx.Owner,Units.Parse("0.0006"),1);await CheckHash(second);
        await ctx.SendAsync("Safe approveHash QA owner",WeightedSafeDrafts.Approve(ctx.Owner,second));
        var hash=Convert.FromHexString(SafeTransactions.CalculateHash(second,202020)[2..]);Assert.True(await contract.GetFunction("approvedHashes").CallAsync<BigInteger>(ctx.Owner,hash)>0);
        var approved=WeightedSafeDrafts.Execute(ctx.Owner,second,202020,[new(ctx.Owner,"approved_hash")],new HashSet<string>(StringComparer.OrdinalIgnoreCase){ctx.Owner});await Simulate(approved,true);
        receipt=await ctx.SendAsync("Safe approved_hash execute return 0.0006 DEL",approved);SafeReceiptVerifier.RequireSuccess(receipt,safe,SafeTransactions.CalculateHash(second,202020));Assert.Equal(2,await queries.GetNonceAsync());Assert.Equal(0,(await ctx.Client.Web3.Eth.GetBalance.SendRequestAsync(safe)).Value);
        // Insufficient Safe balance: require failure before any paid execution.
        var failure=SafeTransactionDrafts.BuildDel(safe,ctx.Owner,Units.Parse("0.001"),2);await CheckHash(failure);
        var failDraft=SafeTransactions.BuildExecutionDraft(ctx.Owner,failure,202020,[ctx.SignSafe(failure)]);
        var rejected=false;try{await Simulate(failDraft,false);rejected=true;}catch(Exception e)when(e.Message.Contains("GS013",StringComparison.OrdinalIgnoreCase)){rejected=true;}Assert.True(rejected,"Safe negative simulation unexpectedly succeeded.");
        ctx.Note("Safe lifecycle verified",new{safe,nonce="2",balance="0",eoa=true,approvedHash=true,negativeSimulation=true});
    }
    [Nethereum.ABI.FunctionEncoding.Attributes.Function("getOwnersWithWeights",typeof(OwnersOutput))]public class Owners:Nethereum.Contracts.FunctionMessage{}
    [Nethereum.ABI.FunctionEncoding.Attributes.FunctionOutput]public class OwnersOutput:Nethereum.ABI.FunctionEncoding.Attributes.IFunctionOutputDTO
    {[Nethereum.ABI.FunctionEncoding.Attributes.Parameter("tuple[]","owners",1)]public List<Member> Members{get;set;}=[];}
    [Nethereum.ABI.FunctionEncoding.Attributes.Struct("Owner")]public class Member
    {[Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address","owner",1)]public string Owner{get;set;}="";[Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256","weight",2)]public BigInteger Weight{get;set;}}
}
