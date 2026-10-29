using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3.Multisig;
using Decimal.Web3.Transactions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
namespace DCWallet.Tests;
public static class SafeAdvancedNetworkScenario
{
    public const string OwnerRuntimeSha256="b5fab85fd93d7d825b6fa8812834248673a5884e7d5988de99b97dbb6e13d3f0";
    public const string DelegateRuntimeSha256="c428650e7e55862b2daff1ff11c1a941e34a9505b8e72eb8e30689877568d497";
    public static async Task RunAsync(AdvancedTestContext ctx)
    {
        await TestnetSafeProfile.VerifyAsync(ctx.Client);
        using var fixtures=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"SafeQaFixtures","SafeQa.compiled.json")));
        var contracts=fixtures.RootElement.GetProperty("contracts");
        var ownerFixture=contracts.GetProperty("QaSignatureOwner"); var delegateFixture=contracts.GetProperty("QaDelegateTarget");
        Assert.Equal(OwnerRuntimeSha256,ownerFixture.GetProperty("runtimeSha256").GetString());
        Assert.Equal(DelegateRuntimeSha256,delegateFixture.GetProperty("runtimeSha256").GetString());
        var existingOwner=Environment.GetEnvironmentVariable("DC_SAFE_QA_OWNER_CONTRACT");
        var owner=string.IsNullOrWhiteSpace(existingOwner)
            ? await ctx.DeployAsync("Safe QA signature owner deployment",ownerFixture.GetProperty("creationBytecode").GetString()!,OwnerRuntimeSha256)
            : await AdvancedTestContext.RecoverExistingDeploymentAsync(ctx.Client,
                Environment.GetEnvironmentVariable("DC_SAFE_QA_OWNER_DEPLOYMENT_HASH") ?? throw new InvalidOperationException("Explicit owner deployment hash required."), existingOwner,
                ownerFixture.GetProperty("creationBytecode").GetString()!,OwnerRuntimeSha256,
                Environment.GetEnvironmentVariable("DC_SAFE_QA_PREVIOUS_DIRECTORY") ?? throw new InvalidOperationException("Explicit previous public journal directory required."));
        if (!string.IsNullOrWhiteSpace(existingOwner)) ctx.Note("Reuse independently recovered owner deployment",new{owner,hash=Environment.GetEnvironmentVariable("DC_SAFE_QA_OWNER_DEPLOYMENT_HASH"),resubmitted=false});
        var existingSafe=Environment.GetEnvironmentVariable("DC_SAFE_QA_EXISTING_SAFE");
        var resumeDirectory=Environment.GetEnvironmentVariable("DC_SAFE_QA_SAFE_PREVIOUS_DIRECTORY");
        var target=string.IsNullOrWhiteSpace(existingSafe)
            ? await ctx.DeployAsync("Safe QA no-storage delegate target deployment",delegateFixture.GetProperty("creationBytecode").GetString()!,DelegateRuntimeSha256)
            : await AdvancedTestContext.RecoverExistingDeploymentAsync(ctx.Client,
                Environment.GetEnvironmentVariable("DC_SAFE_QA_TARGET_DEPLOYMENT_HASH") ?? throw new InvalidOperationException("Explicit target deployment hash required."),
                Environment.GetEnvironmentVariable("DC_SAFE_QA_TARGET_CONTRACT") ?? throw new InvalidOperationException("Explicit target address required."),
                delegateFixture.GetProperty("creationBytecode").GetString()!,DelegateRuntimeSha256,resumeDirectory ?? throw new InvalidOperationException("Explicit previous Safe directory required."));
        async Task CheckRuntime(string address,string hash)
        {
            var code=await ctx.Client.Web3.Eth.GetCode.SendRequestAsync(address);
            Assert.Equal(hash,Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).ToLowerInvariant());
        }
        await CheckRuntime(owner,OwnerRuntimeSha256); await CheckRuntime(target,DelegateRuntimeSha256);
        var safe=existingSafe;
        if(string.IsNullOrWhiteSpace(safe))
        {
            var created=await ctx.SendAsync("Safe create contract-owner threshold1",TestnetSafeProfile.Create(ctx.Owner,[new(owner,1)],1,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),202020));
            safe=TestnetSafeProfile.CreatedAddress(created);
        }
        else await VerifyExistingSafeFunding(ctx,safe,resumeDirectory!);
        var members=await ctx.Client.Web3.Eth.GetContractQueryHandler<SafeNetworkScenario.Owners>().QueryDeserializingToObjectAsync<SafeNetworkScenario.OwnersOutput>(new(),safe);
        Assert.Equal(owner,Assert.Single(members.Members).Owner,StringComparer.OrdinalIgnoreCase);
        if(string.IsNullOrWhiteSpace(existingSafe)) await ctx.SendAsync("Safe contract-owner fund two wei",TransactionDraft.Del(ctx.Owner,safe,"0.000000000000000002"));
        var queries=new SafeQueries(ctx.Client.Web3,safe,ctx.Network);
        Assert.Equal(0,await queries.GetNonceAsync());
        Assert.Equal(new BigInteger(2),(await ctx.Client.Web3.Eth.GetBalance.SendRequestAsync(safe)).Value);
        ctx.Note("Safe advanced signature stage",new{safe,owner,target,stage="modern1271-before-read",resumed=!string.IsNullOrWhiteSpace(existingSafe)});
        var call=SafeTransactionDrafts.BuildDel(safe,ctx.Owner,1,0);
        // Both signature hooks must reject incorrect digests/signatures and accept this specific QA signature.
        var signature=ctx.SignSafe(call).Data;
        var contract=ctx.Client.Web3.Eth.GetContract(ownerFixture.GetProperty("abi").GetRawText(),owner);
        const string hookAbi="""[{"type":"function","name":"isValidSignature","inputs":[{"type":"bytes32"},{"type":"bytes"}],"outputs":[{"type":"bytes4"}]}]""";
        var modern=ctx.Client.Web3.Eth.GetContract(hookAbi,owner).GetFunction("isValidSignature");
        var diagnosticHash=SafeAdvancedTransactions.CalculateHash(call,202020);
        ctx.Note("Safe contract signature local diagnostic",new{stage="before-modern1271",hash=diagnosticHash,has0x=signature.StartsWith("0x",StringComparison.Ordinal),signatureLength=(signature.Length-2)/2,recoveredAddress=new Nethereum.Signer.MessageSigner().EcRecover(Convert.FromHexString(diagnosticHash[2..]),signature)});
        string actualMagic;
        try {actualMagic=Convert.ToHexString(await AdvancedTestContext.ReadOnlyRetryAsync(()=>modern.CallAsync<byte[]>(Convert.FromHexString(diagnosticHash[2..]),Convert.FromHexString(signature[2..])))).ToLowerInvariant();}
        catch(Exception exception){ctx.Note("Safe contract signature RPC diagnostic",new{stage="modern1271-call",errorType=exception.GetType().FullName});throw;}
        ctx.Note("Safe contract signature result",new{stage="modern1271-response",actualMagic,expectedMagic="1626ba7e",hash=diagnosticHash});
        Assert.Equal("1626ba7e",actualMagic);
        Assert.Equal("ffffffff",Convert.ToHexString(await AdvancedTestContext.ReadOnlyRetryAsync(()=>modern.CallAsync<byte[]>(new byte[32],Convert.FromHexString(signature[2..])))).ToLowerInvariant());
        ctx.Note("Safe advanced signature stage",new{safe,owner,target,stage="modern1271-verified"});
        if(Environment.GetEnvironmentVariable("DC_SAFE_DIAGNOSTIC_ONLY")=="1") {ctx.Note("Safe diagnostic read-only complete",new{safe,owner,target,noExecution=true});return;}
        async Task<string> Simulate(TransactionDraft draft)=>await ctx.Client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=ctx.Owner,To=draft.To,Data=draft.Data,Value=new HexBigInteger(0)});
        var bad=SafeAdvancedTransactions.Execute(ctx.Owner,call,202020,[new(owner,"contract:0x"+new string('0',130))],new HashSet<string>());
        var rejected=false; try{await Simulate(bad);}catch(Nethereum.JsonRpc.Client.RpcResponseException e)when(e.Message.Contains("revert",StringComparison.OrdinalIgnoreCase)){rejected=true;}
        Assert.True(rejected,"The owner contract must reject wrong signatures before paid execution.");
        async Task Execute(SafeTransactionDraft proposal,string label)
        {
            await CheckRuntime(owner,OwnerRuntimeSha256);await CheckRuntime(target,DelegateRuntimeSha256);
            // SafeQueries delegates hashing to SDK which only accepts Call; query getTransactionHash directly for operation1.
            const string hashAbi="""[{"type":"function","name":"getTransactionHash","inputs":[{"type":"address"},{"type":"uint256"},{"type":"bytes"},{"type":"uint8"},{"type":"uint256"},{"type":"uint256"},{"type":"uint256"},{"type":"address"},{"type":"address"},{"type":"uint256"}],"outputs":[{"type":"bytes32"}]}]""";
            var chainHash=await ctx.Client.Web3.Eth.GetContract(hashAbi,safe).GetFunction("getTransactionHash").CallAsync<byte[]>(proposal.To,proposal.ValueWei,Convert.FromHexString(proposal.Data[2..]),proposal.Operation,proposal.SafeTxGas,proposal.BaseGas,proposal.GasPriceWei,proposal.GasToken,proposal.RefundReceiver,proposal.Nonce);
            var hash=SafeAdvancedTransactions.CalculateHash(proposal,202020);Assert.Equal(hash,"0x"+Convert.ToHexString(chainHash).ToLowerInvariant());
            var proof=ctx.SignSafe(proposal).Data;
            var draft=SafeAdvancedTransactions.Execute(ctx.Owner,proposal,202020,[new(owner,"contract:"+proof)],new HashSet<string>());
            var result=await Simulate(draft);Assert.Equal(BigInteger.One,new BigInteger(Convert.FromHexString(result[2..]),true,true));
            var receipt=await ctx.SendAsync(label,draft);SafeReceiptVerifier.RequireSuccess(receipt,safe,hash);
            Assert.Equal(proposal.Nonce+1,await queries.GetNonceAsync());
        }
        await Execute(call,"Safe validated contract-owner Call one wei");
        var data=ctx.Client.Web3.Eth.GetContract(delegateFixture.GetProperty("abi").GetRawText(),target).GetFunction("payOneWei").GetData(ctx.Owner);
        await Execute(new(safe,target,0,data,1,0,0,0,SafeTransactionDraft.ZeroAddress,SafeTransactionDraft.ZeroAddress,1),"Safe validated contract-owner DelegateCall one wei");
        Assert.Equal(BigInteger.Zero,(await ctx.Client.Web3.Eth.GetBalance.SendRequestAsync(safe)).Value);
        var afterMembers=await ctx.Client.Web3.Eth.GetContractQueryHandler<SafeNetworkScenario.Owners>().QueryDeserializingToObjectAsync<SafeNetworkScenario.OwnersOutput>(new(),safe);
        Assert.Equal(owner,Assert.Single(afterMembers.Members).Owner,StringComparer.OrdinalIgnoreCase);
        ctx.Note("Safe contract-owner/delegate verified",new{safe,owner,target,nonce=2,returnedWei=2,wrongSignatureRejected=true,modern1271Checked=true,ownerRuntimeSha256=OwnerRuntimeSha256,delegateRuntimeSha256=DelegateRuntimeSha256});
    }
    private static async Task VerifyExistingSafeFunding(AdvancedTestContext ctx,string safe,string directory)
    {
        using var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"report.json")));
        Assert.Equal(202020,report.RootElement.GetProperty("chainId").GetInt32());
        Assert.Equal(ctx.Owner,report.RootElement.GetProperty("owner").GetString(),StringComparer.OrdinalIgnoreCase);
        foreach(var label in new[]{"Safe create contract-owner threshold1","Safe contract-owner fund two wei"})
        {
            var row=Assert.Single(report.RootElement.GetProperty("cases").EnumerateArray(),c=>c.GetProperty("label").GetString()==label);var hash=row.GetProperty("hash").GetString()!;
            var receipt=await AdvancedTestContext.ReadOnlyRetryAsync(()=>ctx.Client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash));
            var tx=await AdvancedTestContext.ReadOnlyRetryAsync(()=>ctx.Client.Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash));
            Assert.Equal(1,receipt.Status.Value);Assert.Equal(ctx.Owner,tx.From,StringComparer.OrdinalIgnoreCase);Assert.Equal(hash,receipt.TransactionHash,StringComparer.OrdinalIgnoreCase);Assert.Equal(tx.BlockHash,receipt.BlockHash);
            var block=await AdvancedTestContext.ReadOnlyRetryAsync(()=>ctx.Client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new BlockParameter(receipt.BlockNumber)));Assert.Equal(receipt.BlockHash,block.BlockHash);
            if(label.StartsWith("Safe create")){Assert.Equal(TestnetSafeProfile.Factory,tx.To,StringComparer.OrdinalIgnoreCase);Assert.Equal(safe,TestnetSafeProfile.CreatedAddress(receipt),StringComparer.OrdinalIgnoreCase);Assert.Equal(0,tx.Value.Value);}
            else{Assert.Equal(safe,tx.To,StringComparer.OrdinalIgnoreCase);Assert.Equal(2,tx.Value.Value);Assert.Equal("0x",tx.Input);}
        }
        ctx.Note("Existing Safe create/fund receipts verified",new{safe,reused=true,noDuplicateFunding=true});
    }
}
