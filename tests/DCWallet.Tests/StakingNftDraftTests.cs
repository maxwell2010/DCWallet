using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Nethereum.Util;
using Xunit;
namespace DCWallet.Tests;
public sealed class StakingNftDraftTests
{
 const string Owner="0x1111111111111111111111111111111111111111";
 static DecimalClient Client()=>new(DecimalNetwork.Testnet());
 [Theory]
 [InlineData(true,0,"delegateNFT(address,address,uint256)",3)]
 [InlineData(true,500,"delegateNFTHold(address,address,uint256,uint256)",4)]
 [InlineData(false,0,"delegateNFT1155(address,address,uint256,uint256)",4)]
 [InlineData(false,500,"delegateNFT1155Hold(address,address,uint256,uint256,uint256)",5)]
 public void DelegateMatchesPublishedSignature(bool erc721,int hold,string signature,int words){var draft=StakingNftDrafts.Delegate(Client(),Owner,Owner,Owner,0,1,erc721,hold);Assert.Equal("0x"+Sha3Keccack.Current.CalculateHash(signature)[..8],draft.Data[..10]);Assert.Equal(10+words*64,draft.Data.Length);Assert.Equal(CombinedNftDelegation.Proxy,draft.To);Assert.Equal(BigInteger.Zero,draft.ValueWei);}
 [Theory][InlineData(true)][InlineData(false)] public void WithdrawWholePositionHasNoAmount(bool erc721){var d=StakingNftDrafts.Withdraw(Client(),Owner,Owner,Owner,1,erc721,0);Assert.Equal(10+3*64,d.Data.Length);var h=StakingNftDrafts.Withdraw(Client(),Owner,Owner,Owner,1,erc721,500);Assert.Equal(10+4*64,h.Data.Length);}
 [Theory][InlineData(true)][InlineData(false)] public void TransferPreservesHoldAndAppendsValidator(bool erc721){var d=StakingNftDrafts.Transfer(Client(),Owner,Owner,Owner,1,erc721,500,Owner);Assert.Equal(10+5*64,d.Data.Length);Assert.EndsWith(new string('0',24)+Owner[2..],d.Data);}
 [Fact]public void RejectsNegativeHoldAndInvalid721Quantity(){Assert.Throws<ArgumentOutOfRangeException>(()=>StakingNftDrafts.Delegate(Client(),Owner,Owner,Owner,1,1,true,-1));Assert.Throws<ArgumentOutOfRangeException>(()=>StakingNftDrafts.Delegate(Client(),Owner,Owner,Owner,1,2,true,0));Assert.Throws<NotSupportedException>(()=>StakingNftDrafts.Withdraw(new DecimalClient(),Owner,Owner,Owner,1,true,0));}
 [Fact]public void NftPermitUsesPublishedSignatureAndExactWords(){var d=StakingNftDrafts.Delegate721ByPermit(Client(),Owner,Owner,Owner,1,500,27,new byte[32],new byte[32]);Assert.Equal("0x"+Sha3Keccack.Current.CalculateHash("delegateNFTByPermit(address,address,uint256,uint256,uint8,bytes32,bytes32)")[..8],d.Data[..10]);Assert.Equal(10+7*64,d.Data.Length);Assert.Throws<ArgumentException>(()=>StakingNftDrafts.Delegate721ByPermit(Client(),Owner,Owner,Owner,1,500,1,new byte[32],new byte[32]));Assert.Throws<ArgumentException>(()=>StakingNftDrafts.Delegate721ByPermit(Client(),Owner,Owner,Owner,1,500,27,new byte[31],new byte[32]));}
}
