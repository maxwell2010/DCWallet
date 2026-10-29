using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Nft;
using Nethereum.Util;
using Xunit;
namespace DCWallet.Tests;
public class CurrentNftDraftTests
{
 const string Owner="0x1111111111111111111111111111111111111111";
 [Fact]public void CurrentNativeMintMatchesPublished721SelectorAndPreservesLegacyPayload(){var client=new DecimalClient(DecimalNetwork.Testnet());var legacy=new DecimalNftDrafts(client.Network,Owner).BuildMintErc721ByEthDraft(Owner,Owner,Owner,"ipfs://qa",1);var mapped=CurrentNftDrafts.AdaptLegacyNativeDraft(client,legacy);var current=CurrentNftDrafts.Mint721WithDel(client,Owner,Owner,Owner,"ipfs://qa",1);Assert.Equal("0x"+Sha3Keccack.Current.CalculateHash("mintByDEL(address,string)")[..8],mapped.Data[..10]);Assert.Equal(legacy.Data[10..],mapped.Data[10..]);Assert.Equal(current.Data,mapped.Data);Assert.Equal(legacy.ValueWei,mapped.ValueWei);Assert.Equal(legacy.To,mapped.To);}
 [Fact]public void CurrentNativeMint1155MatchesPublishedSelector(){var d=CurrentNftDrafts.Mint1155WithDel(new DecimalClient(DecimalNetwork.Testnet()),Owner,Owner,Owner,0,2,"ipfs://qa",1);Assert.Equal("0x"+Sha3Keccack.Current.CalculateHash("mintByDEL(address,uint256,uint256,string)")[..8],d.Data[..10]);Assert.Throws<ArgumentOutOfRangeException>(()=>CurrentNftDrafts.Mint1155WithDel(new DecimalClient(DecimalNetwork.Testnet()),Owner,Owner,Owner,0,0,"ipfs://qa",1));}
 [Fact]public void MainnetDraftRemainsUnchanged(){var client=new DecimalClient();var old=new DecimalNftDrafts(client.Network,Owner).BuildMintErc721ByEthDraft(Owner,Owner,Owner,"ipfs://qa",1);Assert.Same(old,CurrentNftDrafts.AdaptLegacyNativeDraft(client,old));Assert.Throws<NotSupportedException>(()=>CurrentNftDrafts.Mint721WithDel(client,Owner,Owner,Owner,"ipfs://qa",1));}
}
