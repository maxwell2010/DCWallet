using System.Numerics;
using DCWallet.Core;
using Decimal.Web3.Multisig;
using Xunit;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
namespace DCWallet.Tests;
public sealed class WeightedSafeDraftTests
{
    private const string A = "0x1111111111111111111111111111111111111111", B = "0x2222222222222222222222222222222222222222";
    private static BigInteger Word(byte[] bytes, int offset) => new(bytes.AsSpan(offset, 32), true, true);
    [Fact] public void FactoryCallContainsWeightedSetupAndExactSalt()
    {
        var draft = WeightedSafeDrafts.Create(A, [new(A, 2), new(B, 1)], 3, 999, 75);
        Assert.Equal(WeightedSafeDrafts.Factory, draft.To); Assert.Equal(BigInteger.Zero, draft.ValueWei); Assert.StartsWith("0x1688f0b9", draft.Data);
        var bytes = Convert.FromHexString(draft.Data[10..]); Assert.Equal(999, Word(bytes, 64));
        Assert.Equal(WeightedSafeDrafts.Implementation[2..], Convert.ToHexString(bytes.AsSpan(12, 20)).ToLowerInvariant());
        var offset = (int)Word(bytes, 32); var length = (int)Word(bytes, offset); var setup = bytes.AsSpan(offset + 32, length).ToArray();
        var body = setup[4..]; Assert.Equal(3, Word(body, 32)); var ownersOffset = (int)Word(body, 0);
        Assert.Equal(2, Word(body, ownersOffset)); Assert.Equal(2, Word(body, ownersOffset + 64)); Assert.Equal(1, Word(body, ownersOffset + 128));
        Assert.Equal(A[2..], Convert.ToHexString(body.AsSpan(ownersOffset + 44, 20)).ToLowerInvariant());
        Assert.Equal(B[2..], Convert.ToHexString(body.AsSpan(ownersOffset + 108, 20)).ToLowerInvariant());
    }
    [Fact] public void FactoryRejectsOtherChainAndOversizedWeight()
    {
        Assert.Throws<NotSupportedException>(() => WeightedSafeDrafts.Create(A, [new(A, 1)], 1, 0, 202020));
        Assert.Throws<ArgumentException>(() => WeightedSafeDrafts.Create(A, [new(A, 1001)], 1, 0, 75));
    }
    [Fact] public void ApprovedHashRequiresVerifiedOnchainOwner()
    {
        var tx = SafeTransactionDrafts.BuildDel(A, B, 10, 7);
        Assert.Throws<ArgumentException>(() => WeightedSafeDrafts.Execute(A, tx, 75, [new(A, "approved_hash")], new HashSet<string>()));
        var draft = WeightedSafeDrafts.Execute(A, tx, 75, [new(A, "approved_hash")], new HashSet<string>(StringComparer.OrdinalIgnoreCase) { A });
        var bytes = Convert.FromHexString(draft.Data[10..]); var sigOffset = (int)Word(bytes, 9 * 32);
        Assert.Equal(65, Word(bytes, sigOffset)); Assert.Equal(1, bytes[sigOffset + 32 + 64]);
        Assert.Equal(A[2..], Convert.ToHexString(bytes.AsSpan(sigOffset + 44, 20)).ToLowerInvariant());
        Assert.All(bytes.AsSpan(sigOffset + 64, 32).ToArray(), b => Assert.Equal(0, b));
    }
    [Fact] public void FactoryReceiptRequiresCorrectEmitterAndSingleton()
    {
        object Event(string emitter, string implementation) => new { address = emitter, topics = new[] { "0x" + Nethereum.Util.Sha3Keccack.Current.CalculateHash("ProxyCreation(address,address)"), "0x" + A[2..].PadLeft(64, '0') }, data = "0x" + implementation[2..].PadLeft(64, '0') };
        TransactionReceipt Receipt(object log) => JsonConvert.DeserializeObject<TransactionReceipt>(JsonConvert.SerializeObject(new { status = "0x1", logs = new[] { log } }))!;
        Assert.Equal(A, WeightedSafeDrafts.CreatedAddress(Receipt(Event(WeightedSafeDrafts.Factory, WeightedSafeDrafts.Implementation))), ignoreCase: true);
        Assert.Throws<InvalidOperationException>(() => WeightedSafeDrafts.CreatedAddress(Receipt(Event(B, WeightedSafeDrafts.Implementation))));
        Assert.Throws<InvalidOperationException>(() => WeightedSafeDrafts.CreatedAddress(Receipt(Event(WeightedSafeDrafts.Factory, B))));
    }
}
