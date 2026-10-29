using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public class CombinedNftDelegationTests
{
 const string Owner="0x1111111111111111111111111111111111111111";
 [Fact]public void PublishedCombinedSelectorsAndTargetAreExact(){var c=new DecimalClient(DecimalNetwork.Testnet());var d=CombinedNftDelegation.Delegate1155(c,Owner,Owner,Owner,1,1);Assert.Equal(CombinedNftDelegation.Proxy,d.To);Assert.StartsWith("0x319b41a6",d.Data);Assert.Equal(0,d.ValueWei);Assert.StartsWith("0xe29192c3",CombinedNftDelegation.Withdraw1155(c,Owner,Owner,Owner,1).Data);}
 [Fact]public void ProfileCannotBuildForMainnet()=>Assert.Throws<NotSupportedException>(()=>CombinedNftDelegation.Delegate1155(new DecimalClient(),Owner,Owner,Owner,1,1));
 [Fact]public void RejectZeroQuantity()=>Assert.Throws<ArgumentOutOfRangeException>(()=>CombinedNftDelegation.Delegate1155(new DecimalClient(DecimalNetwork.Testnet()),Owner,Owner,Owner,1,0));
}
