using System.Numerics;
using System.Text.Json;
using System.Security.Cryptography;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Checks;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
namespace DCWallet.Tests;
public static class ChecksNetworkScenario
{
    const string Contract="0xb6161CAA8Bd7167C2fa67b93b041FF9a94d6a735";
    const string Implementation="0xbaca5a733002f5323abe4c83c91b87212e7a3ca3";
    const string RuntimeSha256="7a9a364077d8ba3f03e77cfb09e79599faea344568ffa245903ccdb90691139e";
    sealed record Recovery(string Contract,string Hash,string Signer,string AmountWei,bool Redeemed=false,byte? RedemptionStatus=null);
    public static async Task RunAsync(AdvancedTestContext ctx)
    {
        Assert.Equal(202020,ctx.Network.ChainId);
        var checks=new ChecksService(ctx.Client.Web3,Contract);
        var implementation=await checks.GetImplementationAsync();Assert.Equal(Implementation,implementation.ToLowerInvariant());
        var actual=await ctx.Client.Web3.Eth.GetCode.SendRequestAsync(implementation);
        Assert.Equal(RuntimeSha256,Convert.ToHexString(SHA256.HashData(Convert.FromHexString(actual[2..]))).ToLowerInvariant());
        ctx.Note("checks testnet compatibility profile (not source-bytecode identity)",new{contract=Contract,implementation,runtimeSha256=RuntimeSha256,officialJsCommit="c7a72278551c517d3c0a37e170f844e8176e385f"});
        var recoveryDirectory=Environment.GetEnvironmentVariable("DC_CHECKS_RECOVERY_DIRECTORY");
        var folder=Path.GetFullPath(recoveryDirectory??ctx.DirectoryPath);
        var campaignRoot=Path.GetFullPath(Path.GetDirectoryName(ctx.DirectoryPath)!);
        if(!folder.StartsWith(campaignRoot.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Recovery directory must be inside the testnet campaign root.");
        var publicFile=Path.Combine(folder,"check-recovery-public.json");var encryptedFile=Path.Combine(folder,"check-secret.encrypted");
        CheckSecret secret;Recovery recovery;
        if(recoveryDirectory is not null)
        {
            recovery=JsonSerializer.Deserialize<Recovery>(await File.ReadAllTextAsync(publicFile))!;Assert.Equal(Contract,recovery.Contract);Assert.Equal(Units.Parse("0.001").ToString(),recovery.AmountWei);
            secret=CheckSecret.FromProtected(await File.ReadAllBytesAsync(encryptedFile),ctx.Unprotect);Assert.Equal(recovery.Signer,secret.Address);
            ctx.Note("checks recovery existing bearer without recreation",new{directory=folder,recovery.Hash});
        }
        else
        {
            secret=new CheckSecret();var nonce=await CheckLifecycle.GetNonceForAsync(ctx.Client,Contract,ctx.Owner);ctx.Note("checks creator-scoped nonce",new{creator=ctx.Owner,nonce=nonce.ToString()});var block=(await ctx.Client.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
            var create=checks.BuildCreateDelDraft(ctx.Owner,new[]{secret.Address},Units.Parse("0.001"),block+10000,nonce);
            var call=await ctx.Client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=ctx.Owner,To=create.To,Data=create.Data,Value=new Nethereum.Hex.HexTypes.HexBigInteger(create.ValueWei)});
            ctx.Note("checks creation simulation returned",new{bytes=(call.Length-2)/2});
            var result=Convert.FromHexString(call[2..]);Assert.Equal(96,result.Length);Assert.Equal(32,new BigInteger(result[..32],true,true));Assert.Equal(1,new BigInteger(result[32..64],true,true));Assert.Contains(result[64..96],b=>b!=0);
            recovery=new(Contract,"0x"+Convert.ToHexString(result[64..96]).ToLowerInvariant(),secret.Address,Units.Parse("0.001").ToString());
            await File.WriteAllBytesAsync(encryptedFile,secret.ExportProtected(ctx.Protect));await File.WriteAllTextAsync(publicFile,JsonSerializer.Serialize(recovery));
            var created=await ctx.SendAsync("checks create one 0.001 tDEL",create);
            var hash=CheckLifecycle.CreatedHash(created,Contract,secret.Address);Assert.Equal(recovery.Hash,"0x"+Convert.ToHexString(hash).ToLowerInvariant());
        }
        using(secret)
        {
            var hash=Convert.FromHexString(recovery.Hash[2..]);var draft=checks.BuildRedeemDraft(ctx.Owner,new[]{secret.SignRedeem(hash,ctx.Owner)},new[]{hash});
            async Task<BigInteger> Fee(TransactionReceipt receipt)
            {
                var tx=await ctx.Client.Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(receipt.TransactionHash);return receipt.GasUsed.Value*(receipt.EffectiveGasPrice?.Value??tx.GasPrice.Value);
            }
            if(!recovery.Redeemed)
            {
                var before=await ctx.Client.GetBalanceWeiAsync(ctx.Owner);var receipt=await ctx.SendAsync("checks redeem new check",draft);var status=CheckLifecycle.RedeemStatus(receipt,Contract,hash);
                Assert.Equal(before+Units.Parse("0.001")-await Fee(receipt),await ctx.Client.GetBalanceWeiAsync(ctx.Owner));ctx.Note("checks redeemed exact credit",new{status,recovery.Hash});
                recovery=recovery with{Redeemed=true,RedemptionStatus=status};await File.WriteAllTextAsync(publicFile,JsonSerializer.Serialize(recovery));
            }
            var rejected=false;
            try { await ctx.Client.EstimateFeeAsync(draft); }
            catch(Nethereum.JsonRpc.Client.RpcResponseException e)when(e.Message.Contains("revert",StringComparison.OrdinalIgnoreCase)){rejected=true;ctx.Note("checks replay preflight contract revert",new{error=e.GetType().Name});}
            catch(Decimal.Web3.Transactions.DecimalTransactionException e)when(e.Code==Decimal.Web3.Transactions.DecimalTransactionError.ContractRejected){rejected=true;ctx.Note("checks replay preflight contract revert",new{error=e.Code.ToString()});}
            if(!rejected)
            {
                var before=await ctx.Client.GetBalanceWeiAsync(ctx.Owner);var replay=await ctx.SendAsync("checks controlled replay already redeemed (zero extra credit)",draft);var status=CheckLifecycle.RedeemStatus(replay,Contract,hash);
                Assert.Equal(before-await Fee(replay),await ctx.Client.GetBalanceWeiAsync(ctx.Owner));if(recovery.RedemptionStatus is not null)Assert.NotEqual(recovery.RedemptionStatus.Value,status);ctx.Note("checks replay zero credit verified",new{status,recovery.Hash});
            }
            File.Delete(encryptedFile);
        }
    }
}
