using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Nft;
using Decimal.Web3.Transactions;
namespace DCWallet.Tests;
public static class Nft721FixturePlan
{
 public const string Center="0x11aE5297d0B5886914661938FcB8AEdc08622194";
 public sealed record CreatePlan(TransactionDraft Draft,string AbiVariant,FeeQuote Quote);
 public static async Task<CreatePlan> StageCreationAsync(DecimalClient client,string owner){
  await CombinedNftDelegation.VerifyAsync(client);
  var resolved=await client.ResolveSystemContractsAsync();
  if(!resolved.NftCenterContract.Equals(Center,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("QA NFT center differs from reviewed registry.");
  var code=await client.Web3.Eth.GetCode.SendRequestAsync(Center);if(code is "0x" or "0x0")throw new InvalidOperationException("NFT center has no code.");
  var builder=new DecimalNftDrafts(resolved,Center);var symbol="qa"+Guid.NewGuid().ToString("N")[..8];
  var candidates=new[]{(draft:builder.BuildCreateErc721Draft(owner,owner,symbol,"DC QA ERC721","ipfs://dc-qa-721",true),abi:"createERC721"),(draft:builder.BuildCreateDrc721LegacyDraft(owner,owner,symbol,"DC QA ERC721","ipfs://dc-qa-721",true,true),abi:"createDRC721 legacy")};
  foreach(var candidate in candidates){try{var quote=await client.EstimateFeeAsync(candidate.draft);if(!quote.CanAfford||quote.MaximumFeeWei>Units.Parse("0.6"))throw new InvalidOperationException("Fixture creation blocked: max fee="+quote.MaximumFeeDel+" tDEL, affordable="+quote.CanAfford+", cap=0.6 tDEL (creation only), ABI="+candidate.abi);return new(candidate.draft,candidate.abi,quote);}catch(DecimalTransactionException){}}
  throw new NotSupportedException("Neither reviewed NFT721 creation ABI passed read-only estimation. No write should be attempted.");
 }
 public static async Task<TransactionDraft> StageReserveMintAsync(DecimalClient client,string owner,string collection){await CombinedNftDelegation.VerifyAsync(client);var nft=new Erc721Service(client);if(!(await nft.CreatorAsync(collection)).Equals(owner,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Only a newly QA-owned collection may be used.");if(await nft.BalanceOfAsync(collection,owner)!=0)throw new InvalidOperationException("Collection already has owner assets; do not mutate existing fixtures.");var draft=CurrentNftDrafts.Mint721WithDel(client,owner,collection,owner,"ipfs://dc-qa-721/1",Units.Parse("0.001"));var quote=await client.EstimateFeeAsync(draft);if(!quote.CanAfford||quote.MaximumFeeWei>Units.Parse("0.5"))throw new InvalidOperationException("Fixture mint fee exceeds QA cap.");return draft;}
}
