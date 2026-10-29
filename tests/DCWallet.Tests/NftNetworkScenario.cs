using System.Numerics;
using DCWallet.Core;
using Decimal.Web3.Nft;
using Decimal.Web3.Staking;
using Xunit;
namespace DCWallet.Tests;
public static class NftNetworkScenario
{
 const string Nft="0x918e742d8dd0de7e7930fe0489cdbce1701fb29d", Validator="0x05BaBC196B96C22f763f7e30a84E74665A920f98";
 public static async Task RunAsync(AdvancedTestContext context)
 {
    var client=context.Client; await CombinedNftDelegation.VerifyAsync(client); var nft=new Erc1155Service(client);
    var before=await nft.BalanceOfAsync(Nft,context.Owner,1); Assert.Equal(BigInteger.One,before);
    var hadApproval=await nft.IsApprovedForAllAsync(Nft,context.Owner,CombinedNftDelegation.Proxy);
    if(!hadApproval)await context.SendAsync("NFT1155 authorize combined delegation",nft.SetApprovalForAll(context.Owner,Nft,CombinedNftDelegation.Proxy,true));
    await context.SendAsync("NFT1155 new ABI delegate one QA unit",CombinedNftDelegation.Delegate1155(client,context.Owner,Validator,Nft,1,1));
    Assert.Equal(BigInteger.Zero,await nft.BalanceOfAsync(Nft,context.Owner,1));
    var receipt=await context.SendAsync("NFT1155 new ABI withdraw only QA unit",CombinedNftDelegation.Withdraw1155(client,context.Owner,Validator,Nft,1));
    var requests=WithdrawalReceiptReader.DecodeReceipt(receipt,CombinedNftDelegation.Proxy); var request=Assert.Single(requests);
    Assert.Equal(context.Owner,request.Stake.Delegator,StringComparer.OrdinalIgnoreCase);Assert.Equal(Nft,request.Stake.Token,StringComparer.OrdinalIgnoreCase);Assert.Equal(BigInteger.One,request.Stake.TokenId);Assert.Equal(BigInteger.One,request.Stake.AmountRaw);
    context.Note("NFT1155 frozen request verified",new{index=request.StakeIndex.ToString(),unfreeze=request.UnfreezeTimestamp.ToString(),nft=Nft,id="1",amount="1",implementation=CombinedNftDelegation.Implementation});
    if(!hadApproval){await context.SendAsync("NFT1155 revoke temporary delegation approval",nft.SetApprovalForAll(context.Owner,Nft,CombinedNftDelegation.Proxy,false));Assert.False(await nft.IsApprovedForAllAsync(Nft,context.Owner,CombinedNftDelegation.Proxy));}
    if(request.UnfreezeTimestamp-DateTimeOffset.UtcNow.ToUnixTimeSeconds()>660){context.Note("NFT1155 awaiting maturity",new{index=request.StakeIndex.ToString(),unfreeze=request.UnfreezeTimestamp.ToString()});return;}
    while(DateTimeOffset.UtcNow.ToUnixTimeSeconds()<request.UnfreezeTimestamp)await Task.Delay(TimeSpan.FromSeconds(20));
    await CombinedNftDelegation.VerifyAsync(client);var frozen=await new FrozenStakeReader(client.Web3,CombinedNftDelegation.Proxy).GetFrozenStakeAsync(request.StakeIndex);Assert.True(frozen.IsMatured(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    await context.SendAsync("NFT1155 complete matured withdrawal",new DelegationDrafts(context.Network,CombinedNftDelegation.Proxy).BuildCompleteDraft(context.Owner,new[]{request.StakeIndex}));
    Assert.Equal(before,await nft.BalanceOfAsync(Nft,context.Owner,1));Assert.Equal(0,(await new FrozenStakeReader(client.Web3,CombinedNftDelegation.Proxy).GetFrozenStakeAsync(request.StakeIndex)).FreezeStatus);
    context.Note("NFT1155 lifecycle restored",new{balance="1",id="1",approvalRestored=true});
 }
}
