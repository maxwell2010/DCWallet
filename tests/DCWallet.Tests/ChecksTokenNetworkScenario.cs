using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3.Checks;
using Decimal.Web3.Tokens;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
namespace DCWallet.Tests;
/// <summary>Root-only isolated QA-token lifecycle. No test attribute: parent must explicitly dispatch this scenario.</summary>
public static class ChecksTokenNetworkScenario
{
    private sealed record Recovery(string Token,string Contract,string Hash,string Signer,bool Permit,string OriginalBalanceRaw,string PriorAllowanceRaw);
    public static async Task RunAsync(AdvancedTestContext ctx,bool permit)
    {
        const string contract="0xb6161CAA8Bd7167C2fa67b93b041FF9a94d6a735";
        Assert.Equal(202020,ctx.Network.ChainId);
        var token=Environment.GetEnvironmentVariable("DC_CHECKS_QA_TOKEN")??throw new InvalidOperationException("Explicit disposable QA token address required.");
        var erc=new Erc20Service(ctx.Client,token);var before=await erc.GetBalanceRawAsync(ctx.Owner);Assert.True(before>=1);
        var service=new ChecksService(ctx.Client.Web3,contract);var implementation=await service.GetImplementationAsync();Assert.Equal("0xbaca5a733002f5323abe4c83c91b87212e7a3ca3",implementation.ToLowerInvariant());var code=await ctx.Client.Web3.Eth.GetCode.SendRequestAsync(implementation);Assert.Equal("7a9a364077d8ba3f03e77cfb09e79599faea344568ffa245903ccdb90691139e",Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).ToLowerInvariant());
        using var bearer=new CheckSecret();var recovery=Path.Combine(ctx.DirectoryPath,"token-check-secret.encrypted");await File.WriteAllBytesAsync(recovery,bearer.ExportProtected(ctx.Protect));
        var priorAllowance=await erc.GetAllowanceRawAsync(ctx.Owner,contract);
        if(!permit&&priorAllowance<1)await ctx.SendAsync("checks QA token exact one raw approval",erc.BuildApproveDraft(ctx.Owner,contract,1));
        var nonce=await CheckLifecycle.GetNonceForAsync(ctx.Client,contract,ctx.Owner);var block=(await ctx.Client.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
        Decimal.Web3.Transactions.TransactionDraft create;
        if(permit){var signature=await new Erc20PermitService(ctx.Client,token).SignAsync(contract,1,DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(),ctx.Signer);create=service.BuildCreateTokenByPermitDraft(ctx.Owner,new[]{bearer.Address},1,block+10000,nonce,token,new ChecksPermit(signature.Deadline,signature.V,signature.R,signature.S));}
        else create=service.BuildCreateTokenDraft(ctx.Owner,new[]{bearer.Address},1,block+10000,nonce,token);
        var simulation=await ctx.Client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=ctx.Owner,To=contract,Data=create.Data});var bytes=Convert.FromHexString(simulation[2..]);Assert.Equal(96,bytes.Length);var predicted=bytes[64..96];ctx.Note("token check recovery coordinates",new{token,contract,hash="0x"+Convert.ToHexString(predicted).ToLowerInvariant(),bearer= bearer.Address,amountRaw="1",permit});
        await File.WriteAllTextAsync(Path.Combine(ctx.DirectoryPath,"token-check-public.json"),JsonSerializer.Serialize(new Recovery(token,contract,"0x"+Convert.ToHexString(predicted).ToLowerInvariant(),bearer.Address,permit,before.ToString(),priorAllowance.ToString())));
        var receipt=await ctx.SendAsync("checks QA token create one raw"+(permit?" permit":" allowance"),create);var hash=CheckLifecycle.CreatedHash(receipt,contract,bearer.Address);Assert.Equal(predicted,hash);Assert.Equal(before-1,await erc.GetBalanceRawAsync(ctx.Owner));
        var redeem=service.BuildRedeemDraft(ctx.Owner,new[]{bearer.SignRedeem(hash,ctx.Owner)},new[]{hash});var paid=await ctx.SendAsync("checks QA token redeem one raw",redeem);Assert.Equal(1,CheckLifecycle.RedeemStatus(paid,contract,hash));Assert.Equal(before,await erc.GetBalanceRawAsync(ctx.Owner));
        var rejected=false;try{await ctx.Client.EstimateFeeAsync(redeem);}catch(Decimal.Web3.Transactions.DecimalTransactionException e)when(e.Code==Decimal.Web3.Transactions.DecimalTransactionError.ContractRejected){rejected=true;}catch(Nethereum.JsonRpc.Client.RpcResponseException e)when(e.Message.Contains("revert",StringComparison.OrdinalIgnoreCase)){rejected=true;}Assert.True(rejected);
        if(!permit&&priorAllowance<1&&await erc.GetAllowanceRawAsync(ctx.Owner,contract)>0)await ctx.SendAsync("checks revoke QA token remaining allowance",erc.BuildApproveDraft(ctx.Owner,contract,0));
        File.Delete(recovery);ctx.Note("token check exact returned balance and replay verified",new{token,amountRaw="1",permit});
    }

    public static async Task ResumeAsync(AdvancedTestContext ctx,string directory)
    {
        var folder=Path.GetFullPath(directory);var root=Path.GetFullPath(Path.GetDirectoryName(ctx.DirectoryPath)!);
        if(!folder.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Recovery must be inside the isolated campaign root.");
        var recovery=JsonSerializer.Deserialize<Recovery>(await File.ReadAllTextAsync(Path.Combine(folder,"token-check-public.json")))??throw new InvalidDataException();
        Assert.Equal("0xb6161caa8bd7167c2fa67b93b041ff9a94d6a735",recovery.Contract.ToLowerInvariant());await ChecksDeploymentProfile.VerifyAsync(ctx.Client,recovery.Contract);
        using var bearer=CheckSecret.FromProtected(await File.ReadAllBytesAsync(Path.Combine(folder,"token-check-secret.encrypted")),ctx.Unprotect);Assert.Equal(recovery.Signer,bearer.Address);var hash=Convert.FromHexString(recovery.Hash[2..]);
        var created=false;var redeemed=false;
        foreach(var file in Directory.GetFiles(root,"report.json",SearchOption.AllDirectories))
        {
            using var report=JsonDocument.Parse(await File.ReadAllTextAsync(file));if(!report.RootElement.TryGetProperty("cases",out var cases))continue;
            foreach(var item in cases.EnumerateArray())
            {
                if(!item.TryGetProperty("hash",out var txHash))continue;
                var receipt=await ctx.Client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash.GetString());if(receipt?.Status?.Value!=1)continue;
                try{if(CheckLifecycle.CreatedHash(receipt,recovery.Contract,bearer.Address).SequenceEqual(hash))created=true;}catch(InvalidOperationException){}
                try{if(CheckLifecycle.RedeemStatus(receipt,recovery.Contract,hash)==1)redeemed=true;}catch(InvalidOperationException){}
            }
        }
        if(!created)throw new InvalidOperationException("Original create has no confirmed matching CreatedCheck event. Never recreate automatically.");
        if(!redeemed)
        {
            var erc=new Erc20Service(ctx.Client,recovery.Token);var before=await erc.GetBalanceRawAsync(ctx.Owner);var draft=new ChecksService(ctx.Client.Web3,recovery.Contract).BuildRedeemDraft(ctx.Owner,new[]{bearer.SignRedeem(hash,ctx.Owner)},new[]{hash});
            var receipt=await ctx.SendAsync("checks QA token resume existing redeem one raw",draft);Assert.Equal(1,CheckLifecycle.RedeemStatus(receipt,recovery.Contract,hash));Assert.Equal(before+1,await erc.GetBalanceRawAsync(ctx.Owner));
        }
        var replay=new ChecksService(ctx.Client.Web3,recovery.Contract).BuildRedeemDraft(ctx.Owner,new[]{bearer.SignRedeem(hash,ctx.Owner)},new[]{hash});var rejected=false;
        try{await ctx.Client.EstimateFeeAsync(replay);}catch(Decimal.Web3.Transactions.DecimalTransactionException e)when(e.Code==Decimal.Web3.Transactions.DecimalTransactionError.ContractRejected){rejected=true;}catch(Nethereum.JsonRpc.Client.RpcResponseException e)when(e.Message.Contains("revert",StringComparison.OrdinalIgnoreCase)){rejected=true;}Assert.True(rejected);
        File.Delete(Path.Combine(folder,"token-check-secret.encrypted"));ctx.Note("resumed existing token check without recreation",new{recovery.Token,recovery.Hash});
    }
}
