using System.Numerics;
using DCWallet.Core;
using Decimal.Web3.Nft;
using Decimal.Web3.Transactions;
namespace DCWallet.Tests;
/// <summary>Read-only staging only. Root sender explicitly reviews returned draft; this never broadcasts.</summary>
internal static class StakingNftNetworkScenario
{
 public static async Task<TransactionDraft> StageOwned721Async(AdvancedTestContext ctx,string collection,BigInteger id,string validator,BigInteger hold)
 {
  if(hold<0 || (hold>0 && hold<=DateTimeOffset.UtcNow.ToUnixTimeSeconds()))throw new ArgumentOutOfRangeException(nameof(hold));
  await CombinedNftDelegation.VerifyAsync(ctx.Client);
  var nft=new Erc721Service(ctx.Client);
  if(!(await nft.OwnerOfAsync(collection,id)).Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("QA does not own requested NFT721.");
  var existing=await CombinedNftDelegation.GetStakeAsync(ctx.Client,collection,id,ctx.Owner,validator,hold);
  if(existing?.IsActive==true)throw new InvalidOperationException("Do not alter an existing stake during QA.");
  var approved=(await nft.GetApprovedAsync(collection,id)).Equals(CombinedNftDelegation.Proxy,StringComparison.OrdinalIgnoreCase)||await nft.IsApprovedForAllAsync(collection,ctx.Owner,CombinedNftDelegation.Proxy);
  ctx.Note("NFT721 stage read-only",new{collection,id=id.ToString(),validator,hold=hold.ToString(),approved,policy="Root sender must preserve original approval and explicitly review lock maturity before write."});
  if(!approved)throw new InvalidOperationException("Exact NFT approval is required before delegation; no automatic approval or write.");
  var draft=StakingNftDrafts.Delegate(ctx.Client,ctx.Owner,validator,collection,id,1,true,hold);
  await ctx.Client.EstimateFeeAsync(draft);
  return draft;
 }
}
