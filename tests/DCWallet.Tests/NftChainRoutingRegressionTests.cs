using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Nft;
using Xunit;
namespace DCWallet.Tests;
public class NftChainRoutingRegressionTests
{
 const string Owner="0x1111111111111111111111111111111111111111",MainnetLegacy="0xe45adfcc739a0d10ce9462b58866c9a1a06035e2";
 [Fact]public void MainnetLegacySelectorIsNotReplacedByTestnetCombined(){var mainnet=new DecimalClient(DecimalNetwork.Mainnet());NftDelegationSupport.RequireSupported(75,MainnetLegacy);var draft=new DecimalNftDrafts(mainnet.Network,mainnet.Network.NftCenterContract,MainnetLegacy).BuildDelegateErc721Draft(Owner,Owner,Owner,1);Assert.Equal(MainnetLegacy,draft.To,StringComparer.OrdinalIgnoreCase);Assert.StartsWith("0x556e055f",draft.Data);Assert.Same(draft,CurrentNftDrafts.AdaptLegacyNativeDraft(mainnet,draft));Assert.Throws<NotSupportedException>(()=>StakingNftDrafts.Delegate(mainnet,Owner,Owner,Owner,1,1,true,0));}
 [Fact]public void TestnetDedicatedSelectorTargetsOnlyPinnedCombinedProxy(){var testnet=new DecimalClient(DecimalNetwork.Testnet());var draft=StakingNftDrafts.Delegate(testnet,Owner,Owner,Owner,1,1,true,0);Assert.Equal(CombinedNftDelegation.Proxy,draft.To);Assert.StartsWith("0x4a8c7802",draft.Data);Assert.Throws<NotSupportedException>(()=>NftDelegationSupport.RequireSupported(202020,MainnetLegacy));}
}
