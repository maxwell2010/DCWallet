using DCWallet.Core;
using Xunit;
namespace DCWallet.Tests;
public sealed class NftDelegationSupportTests
{
    [Fact] public void DeprecatedTestnetContractFailsBeforeRpc() => Assert.Throws<NotSupportedException>(() => NftDelegationSupport.RequireSupported(202020, NftDelegationSupport.DeprecatedTestnetProxy.ToUpperInvariant()));
    [Fact] public void ReplacingAddressWithCombinedProxyDoesNotInventNewAbi() => Assert.Throws<NotSupportedException>(() => NftDelegationSupport.RequireSupported(202020, NftDelegationSupport.CombinedTestnetProxy));
    [Fact] public void UnverifiedTestnetAddressAlsoFailsClosed() => Assert.Throws<NotSupportedException>(() => NftDelegationSupport.RequireSupported(202020, "0x1111111111111111111111111111111111111111"));
    [Fact] public void OtherNetworksAreUnaffectedByThisTestnetSpecificBlock() => NftDelegationSupport.RequireSupported(75, "0xe45adfcc739a0d10ce9462b58866c9a1a06035e2");
}
