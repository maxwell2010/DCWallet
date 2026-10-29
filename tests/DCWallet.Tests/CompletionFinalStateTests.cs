using System.Numerics;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Decimal.Web3.Nft;
using Decimal.Web3.Staking;
using Decimal.Web3.Tokens;
using Xunit;
namespace DCWallet.Tests;
public sealed class CompletionFinalStateTests
{
    [CompletionAuditFact,Trait("Category","TestnetRead")]
    public async Task NewLifecycleAssetsAreRestoredWithoutPendingWithdrawalOrSafeBalance()
    {
        var root=Environment.GetEnvironmentVariable("DC_COMPLETION_ROOT")??throw new InvalidOperationException("Explicit completion artifact directory required.");
        var client=new DecimalClient(DecimalNetwork.Testnet()with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")});var owner=AdvancedTestContext.ExpectedOwner;
        async Task<T> Read<T>(Func<Task<T>> read){await Task.Delay(2000);return await AdvancedTestContext.ReadOnlyRetryAsync(read);}
        Assert.Equal(202020,(await Read(()=>client.Web3.Eth.ChainId.SendRequestAsync())).Value);
        const string nft="0x9Cc8BFB95BaAE03BfE6E0a0E00548762BEDdac54",safe="0x3076623e219610396eb7da67700af90ddcb73df6",checksToken="0xc310f25e3429efc5ede11a908682f0a31cfc92ce";
        var nftOwner=await Read(()=>new Erc721Service(client).OwnerOfAsync(nft,1));Assert.Equal(owner,nftOwner,StringComparer.OrdinalIgnoreCase);
        var frozen=new FrozenStakeReader(client.Web3,CombinedNftDelegation.Proxy);
        var nftFrozen=await Read(()=>frozen.GetFrozenStakeAsync(230));Assert.Equal(0,nftFrozen.FreezeStatus);
        var tokenFrozen=await Read(()=>frozen.GetFrozenStakeAsync(231));Assert.Equal(0,tokenFrozen.FreezeStatus);
        var token=new Erc20Service(client,Erc20StakingNetworkScenario.Token);
        var balance=await Read(()=>token.GetBalanceRawAsync(owner));Assert.Equal(BigInteger.Parse("2510366318"),balance);
        var allowance=await Read(()=>token.GetAllowanceRawAsync(owner,CombinedNftDelegation.Proxy));Assert.Equal(BigInteger.Zero,allowance);
        var stake=await Read(()=>new StakeReader(client.Web3,CombinedNftDelegation.Proxy).GetStakeAsync(Erc20StakingNetworkScenario.Validator,owner,Erc20StakingNetworkScenario.Token));Assert.Equal(BigInteger.Zero,stake.AmountRaw);
        var safeNonce=await Read(()=>new SafeQueries(client.Web3,safe,client.Network).GetNonceAsync());Assert.Equal(new BigInteger(2),safeNonce);
        var safeBalance=(await Read(()=>client.Web3.Eth.GetBalance.SendRequestAsync(safe))).Value;Assert.Equal(BigInteger.Zero,safeBalance);
        var checksBalance=await Read(()=>new Erc20Service(client,checksToken).GetBalanceRawAsync(owner));Assert.Equal(BigInteger.Parse("100000000000000000000"),checksBalance);
        await File.WriteAllTextAsync(Path.Combine(root,"completion-final-state.json"),JsonSerializer.Serialize(new{chainId=202020,owner,nftCollection=nft,nftId="1",nftOwner,nftFrozen230Status=nftFrozen.FreezeStatus,tokenFrozen231Status=tokenFrozen.FreezeStatus,reserveToken=Erc20StakingNetworkScenario.Token,reserveBalanceRaw=balance.ToString(),reserveStakeRaw=stake.AmountRaw.ToString(),delegationAllowanceRaw=allowance.ToString(),safe,safeNonce=safeNonce.ToString(),safeBalanceWei=safeBalance.ToString(),checksToken,checksBalanceRaw=checksBalance.ToString(),verification="read-only-public-rpc",broadcast=false},new JsonSerializerOptions{WriteIndented=true}));
    }
}
