using System.Numerics;
using DCWallet.Core;
using Decimal.Web3.Nft;
using Decimal.Web3.Staking;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
namespace DCWallet.Tests;
[Event("NFTCreated")]public class QaLegacyNftCreated:IEventDTO {
 [Parameter("address","tokenAddress",1,false)]public string Address{get;set;}="";
 [Parameter("uint8","nftType",2,true)]public byte NftType{get;set;}
 [Parameter("tuple","nft",3,false)]public LegacyNftCollectionMeta Meta{get;set;}=new();
}
public static class Nft721NetworkScenario
{
 public const string Validator="0x05BaBC196B96C22f763f7e30a84E74665A920f98";
 public sealed record Pending(string Collection,string TokenId,string RequestIndex,string UnfreezeTimestamp,string CreationHash,string WithdrawalHash);
 public static async Task<Pending> BeginAsync(AdvancedTestContext ctx){
  var plan=await Nft721FixturePlan.StageCreationAsync(ctx.Client,ctx.Owner);var receipt=await ctx.SendAsync("NFT721 create disposable QA collection",plan.Draft,true);
  var created=receipt.DecodeAllEvents<QaLegacyNftCreated>().Where(e=>e.Log.Address.Equals(Nft721FixturePlan.Center,StringComparison.OrdinalIgnoreCase)).ToArray();
  if(created.Length!=1||!created[0].Event.Meta.Creator.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)||created[0].Event.Meta.ContractUri!="ipfs://dc-qa-721"||created[0].Event.Meta.Name!="DC QA ERC721"||!created[0].Event.Meta.Symbol.StartsWith("qa",StringComparison.Ordinal))throw new InvalidOperationException("Fresh NFT collection creation event mismatch.");
  return await ResumeCreatedCollectionAsync(ctx,created[0].Event.Address,receipt.TransactionHash);
 }
 public static async Task<Pending> ResumeCreatedCollectionAsync(AdvancedTestContext ctx,string collection,string creationHash){
  await CombinedNftDelegation.VerifyAsync(ctx.Client);var origin=await ctx.Client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(creationHash)??throw new InvalidOperationException("Creation receipt unavailable.");
  var canonical=await ctx.Client.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(origin.BlockNumber);if(!canonical.BlockHash.Equals(origin.BlockHash,StringComparison.OrdinalIgnoreCase)||origin.Status?.Value!=1||!origin.From.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)||!origin.To.Equals(Nft721FixturePlan.Center,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Noncanonical creation proof.");
  var events=origin.DecodeAllEvents<QaLegacyNftCreated>().Where(e=>e.Log.Address.Equals(Nft721FixturePlan.Center,StringComparison.OrdinalIgnoreCase)&&e.Event.Address.Equals(collection,StringComparison.OrdinalIgnoreCase)).ToArray();if(events.Length!=1||!events[0].Event.Meta.Creator.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)||events[0].Event.Meta.ContractUri!="ipfs://dc-qa-721"||events[0].Event.Meta.Name!="DC QA ERC721")throw new InvalidOperationException("QA collection identity mismatch.");
  var nft=new Erc721Service(ctx.Client);ctx.Note("NFT721 resume existing empty collection",new{collection,creationHash});
  if(await nft.BalanceOfAsync(collection,ctx.Owner)!=0)throw new InvalidOperationException("Collection already has assets; recover mint/delegation record, do not mint again.");
  var mint=await Nft721FixturePlan.StageReserveMintAsync(ctx.Client,ctx.Owner,collection);var minted=await ctx.SendAsync("NFT721 mint one new QA NFT with 0.001 tDEL reserve",mint);
  var movements=NftReceiptReader.DecodeMovements(minted,collection).Where(m=>m.From.Equals("0x0000000000000000000000000000000000000000",StringComparison.OrdinalIgnoreCase)&&m.To.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)).ToArray();
  if(movements.Length!=1||movements[0].TokenIds.Count!=1)throw new InvalidOperationException("Expected exactly one NFT mint event.");var id=movements[0].TokenIds[0];
  if(!(await nft.OwnerOfAsync(collection,id)).Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)||await nft.BalanceOfAsync(collection,ctx.Owner)!=1||await nft.TokenUriAsync(collection,id)!="ipfs://dc-qa-721/1")throw new InvalidOperationException("Fresh NFT ownership or URI mismatch.");
  ctx.Note("NFT721 minted recovery",new{collection,id=id.ToString(),mintHash=minted.TransactionHash});
  return await ResumeOwnedMintAsync(ctx,collection,id,creationHash,minted.TransactionHash);
 }
 public static async Task<Pending> ResumeOwnedMintAsync(AdvancedTestContext ctx,string collection,BigInteger id,string creationHash,string mintHash="0x92d8f49d49008b5fdacb3eedef60c8ea297781d256dfb37c2dd696c15938f7a8"){
  await CombinedNftDelegation.VerifyAsync(ctx.Client);
  var origin=await ctx.Client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(creationHash)??throw new InvalidOperationException("Creation receipt unavailable.");var originBlock=await ctx.Client.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(origin.BlockNumber);
  if(origin.Status?.Value!=1||!originBlock.BlockHash.Equals(origin.BlockHash,StringComparison.OrdinalIgnoreCase)||!origin.From.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)||!origin.To.Equals(Nft721FixturePlan.Center,StringComparison.OrdinalIgnoreCase)||!origin.DecodeAllEvents<QaLegacyNftCreated>().Any(e=>e.Log.Address.Equals(Nft721FixturePlan.Center,StringComparison.OrdinalIgnoreCase)&&e.Event.Address.Equals(collection,StringComparison.OrdinalIgnoreCase)&&e.Event.Meta.Creator.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)&&e.Event.Meta.ContractUri=="ipfs://dc-qa-721"))throw new InvalidOperationException("Canonical QA creation proof missing.");
  var mint=await ctx.Client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(mintHash)??throw new InvalidOperationException("Mint receipt unavailable.");var mintBlock=await ctx.Client.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(mint.BlockNumber);
  if(mint.Status?.Value!=1||!mintBlock.BlockHash.Equals(mint.BlockHash,StringComparison.OrdinalIgnoreCase)||!mint.From.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)||!mint.To.Equals(collection,StringComparison.OrdinalIgnoreCase)||!NftReceiptReader.DecodeMovements(mint,collection).Any(m=>m.From.Equals("0x0000000000000000000000000000000000000000",StringComparison.OrdinalIgnoreCase)&&m.To.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)&&m.TokenIds.Count==1&&m.TokenIds[0]==id))throw new InvalidOperationException("Canonical QA mint proof missing.");
  var nft=new Erc721Service(ctx.Client);if(!(await nft.OwnerOfAsync(collection,id)).Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)||await nft.BalanceOfAsync(collection,ctx.Owner)!=1)throw new InvalidOperationException("QA minted NFT is no longer owned.");
  var active=await CombinedNftDelegation.GetStakeAsync(ctx.Client,collection,id,ctx.Owner,Validator,0);if(active?.IsActive==true)throw new InvalidOperationException("Position already delegated; resume withdrawal instead.");
  if(!(await nft.GetApprovedAsync(collection,id)).Equals(CombinedNftDelegation.Proxy,StringComparison.OrdinalIgnoreCase))await ctx.SendAsync("NFT721 approve only new QA token",nft.Approve(ctx.Owner,collection,CombinedNftDelegation.Proxy,id));
  var draft=await StakingNftNetworkScenario.StageOwned721Async(ctx,collection,id,Validator,0);await ctx.SendAsync("NFT721 delegate only new QA token",draft,reviewedNft721Delegation:true);
  ctx.Note("NFT721 delegated before withdraw",new{collection,id=id.ToString(),creationHash});return await WithdrawAsync(ctx,collection,id,creationHash);
 }
 public static async Task<Pending> WithdrawAsync(AdvancedTestContext ctx,string collection,BigInteger id,string creationHash){
  await CombinedNftDelegation.VerifyAsync(ctx.Client);
  var origin=await ctx.Client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(creationHash)??throw new InvalidOperationException("Creation receipt unavailable.");
  if(!origin.DecodeAllEvents<QaLegacyNftCreated>().Any(e=>e.Log.Address.Equals(Nft721FixturePlan.Center,StringComparison.OrdinalIgnoreCase)&&e.Event.Address.Equals(collection,StringComparison.OrdinalIgnoreCase)&&e.Event.Meta.Creator.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("QA collection creation proof missing.");
  var active=await CombinedNftDelegation.GetStakeAsync(ctx.Client,collection,id,ctx.Owner,Validator,0);if(active?.IsActive!=true||active.NftType!=2||active.Amount!=1)throw new InvalidOperationException("NFT721 active stake mismatch.");
  var withdrawn=await ctx.SendAsync("NFT721 withdraw only new QA position",StakingNftDrafts.Withdraw(ctx.Client,ctx.Owner,Validator,collection,id,true,0));var requests=WithdrawalReceiptReader.DecodeReceipt(withdrawn,CombinedNftDelegation.Proxy);
  if(requests.Count!=1)throw new InvalidOperationException("Expected exactly one NFT721 withdrawal.");var request=requests[0];
  if(!request.Stake.Delegator.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)||!request.Stake.Token.Equals(collection,StringComparison.OrdinalIgnoreCase)||request.Stake.TokenId!=id||request.Stake.AmountRaw!=1||request.Stake.TokenType!=2)throw new InvalidOperationException("NFT721 withdrawal coordinates mismatch.");
  var pending=new Pending(collection,id.ToString(),request.StakeIndex.ToString(),request.UnfreezeTimestamp.ToString(),creationHash,withdrawn.TransactionHash);ctx.Note("NFT721 pending public recovery",pending);await File.WriteAllTextAsync(Path.Combine(ctx.DirectoryPath,"nft721-staking-pending.json"),System.Text.Json.JsonSerializer.Serialize(pending));return pending;
 }
 public static async Task CompleteAsync(AdvancedTestContext ctx,Pending pending){ctx.Note("NFT721 completion stage",new{stage="profile-before-read",pending.RequestIndex});await AdvancedTestContext.ReadOnlyRetryAsync(async()=>{await CombinedNftDelegation.VerifyAsync(ctx.Client);return true;});ctx.Note("NFT721 completion stage",new{stage="profile-verified",pending.RequestIndex});var proofReceipt=await AdvancedTestContext.ReadOnlyRetryAsync(()=>ctx.Client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(pending.WithdrawalHash))??throw new InvalidOperationException("Withdrawal receipt unavailable.");var proof=WithdrawalReceiptReader.DecodeReceipt(proofReceipt,CombinedNftDelegation.Proxy);if(proof.Count!=1||proof[0].StakeIndex.ToString()!=pending.RequestIndex||proof[0].UnfreezeTimestamp.ToString()!=pending.UnfreezeTimestamp)throw new InvalidOperationException("NFT721 pending receipt proof differs.");var id=BigInteger.Parse(pending.TokenId);var index=BigInteger.Parse(pending.RequestIndex);ctx.Note("NFT721 completion stage",new{stage="receipt-verified",pending.RequestIndex});var frozen=await AdvancedTestContext.ReadOnlyRetryAsync(()=>new FrozenStakeReader(ctx.Client.Web3,CombinedNftDelegation.Proxy).GetFrozenStakeAsync(index));ctx.Note("NFT721 completion stage",new{stage="frozen-verified",pending.RequestIndex});
  if(!frozen.IsMatured(DateTimeOffset.UtcNow.ToUnixTimeSeconds())||!frozen.Stake.Delegator.Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)||!frozen.Stake.Token.Equals(pending.Collection,StringComparison.OrdinalIgnoreCase)||frozen.Stake.TokenId!=id||frozen.Stake.AmountRaw!=1||frozen.Stake.TokenType!=2||!frozen.Stake.Validator.Equals(Validator,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("NFT721 request is not the exact owned matured QA withdrawal.");
  await ctx.SendAsync("NFT721 complete only new QA withdrawal",new DelegationDrafts(ctx.Network,CombinedNftDelegation.Proxy).BuildCompleteDraft(ctx.Owner,new[]{index}));var nft=new Erc721Service(ctx.Client);
  if(!(await AdvancedTestContext.ReadOnlyRetryAsync(()=>nft.OwnerOfAsync(pending.Collection,id))).Equals(ctx.Owner,StringComparison.OrdinalIgnoreCase)||await AdvancedTestContext.ReadOnlyRetryAsync(()=>nft.BalanceOfAsync(pending.Collection,ctx.Owner))!=1||(await AdvancedTestContext.ReadOnlyRetryAsync(()=>new FrozenStakeReader(ctx.Client.Web3,CombinedNftDelegation.Proxy).GetFrozenStakeAsync(index))).FreezeStatus!=0)throw new InvalidOperationException("NFT721 final balance/status mismatch.");
  if(!(await AdvancedTestContext.ReadOnlyRetryAsync(()=>nft.GetApprovedAsync(pending.Collection,id))).Equals("0x0000000000000000000000000000000000000000",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Temporary approval was not cleared by transfer.");ctx.Note("NFT721 lifecycle restored",pending);
 }
}
