using DCWallet.Core;
using Xunit;
namespace DCWallet.Tests;
public sealed class SafeQuorumTests
{
    private const string A = "0x1111111111111111111111111111111111111111", B = "0x2222222222222222222222222222222222222222", C = "0x3333333333333333333333333333333333333333";
    [Fact] public void WeightedOwnerAloneCanMeetThreshold() => Assert.Equal(10, SafeQuorum.SignedWeight([new(A, 10), new(B, 1)], 10, [A]));
    [Fact] public void TwoLowWeightSignaturesCannotReplaceRequiredWeight() => Assert.Throws<ArgumentException>(() => SafeQuorum.SignedWeight([new(A, 10), new(B, 1), new(C, 1)], 10, [B, C]));
    [Fact] public void DuplicateOwnerCannotAddWeight() => Assert.Throws<ArgumentException>(() => SafeQuorum.SignedWeight([new(A, 10), new(B, 1)], 10, [A, A]));
    [Fact] public void ThresholdAboveTotalRejected() => Assert.Throws<ArgumentException>(() => SafeQuorum.Validate([new(A, 10)], 11));
    [Fact] public void ZeroWeightAndSentinelOwnerRejected()
    {
        Assert.Throws<ArgumentException>(() => SafeQuorum.Validate([new(A, 0)], 1));
        Assert.Throws<ArgumentException>(() => SafeQuorum.Validate([new("0x0000000000000000000000000000000000000001", 1)], 1));
    }
}
