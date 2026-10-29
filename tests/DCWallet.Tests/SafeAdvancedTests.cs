using System.Numerics;
using DCWallet.Core;
using Decimal.Web3.Multisig;
using Nethereum.Signer;
using Xunit;
namespace DCWallet.Tests;
public class SafeAdvancedTests
{
    const string A="0x1111111111111111111111111111111111111111", B="0x2222222222222222222222222222222222222222";
    static SafeTransactionDraft Tx(byte op=0)=>new(A,B,0,"0x12345678",op,0,0,0,SafeTransactionDraft.ZeroAddress,SafeTransactionDraft.ZeroAddress,7);
    [Fact] public void CallHashMatchesIndependentExistingSdk() => Assert.Equal(SafeTransactions.CalculateHash(Tx(),202020),SafeAdvancedTransactions.CalculateHash(Tx(),202020));
    [Fact] public void DelegateHashMatchesIndependentEthersVector() => Assert.Equal("0x5678d85ee2b56b53d86fff5176b949abc1eb5d03621036db538ec01467024758",SafeAdvancedTransactions.CalculateHash(Tx(1),202020));
    [Fact] public void DelegateSignatureBindsOperationChainNonceAndData()
    {
        var key=EthECKey.GenerateKey(); var tx=Tx(1); var sig=SafeAdvancedTransactions.Sign(tx,202020,key);
        Assert.Equal(65,SafeAdvancedTransactions.Pack(tx,202020,[sig],new HashSet<string>()).Length);
        foreach(var other in new[]{tx with { Operation=0 },tx with { Nonce=8 },tx with { Data="0x" }}) Assert.Throws<ArgumentException>(()=>SafeAdvancedTransactions.Pack(other,202020,[sig],new HashSet<string>()));
        Assert.Throws<ArgumentException>(()=>SafeAdvancedTransactions.Pack(tx,75,[sig],new HashSet<string>()));
    }
    [Fact] public void ContractOffsetsAreRelativeToPackedStartAndBodiesRetainExactLength()
    {
        var packed=SafeAdvancedTransactions.Pack(Tx(),75,[new(B,"contract:0xaabbcc"),new(A,"contract:0xdd")],new HashSet<string>());
        Assert.Equal(130,new BigInteger(packed.AsSpan(32,32),true,true));
        Assert.Equal(163,new BigInteger(packed.AsSpan(97,32),true,true));
        Assert.Equal(0,packed[64]); Assert.Equal(0,packed[129]);
        Assert.Equal(1,new BigInteger(packed.AsSpan(130,32),true,true)); Assert.Equal(0xdd,packed[162]);
        Assert.Equal(3,new BigInteger(packed.AsSpan(163,32),true,true)); Assert.Equal(new byte[]{0xaa,0xbb,0xcc},packed[195..]);
    }
    [Fact] public void ApprovedOwnerMustBeVerifiedAndDuplicatesRejected()
    {
        Assert.Throws<ArgumentException>(()=>SafeAdvancedTransactions.Pack(Tx(),75,[new(A,"approved_hash")],new HashSet<string>()));
        Assert.Equal(1,SafeAdvancedTransactions.Pack(Tx(),75,[new(A,"approved_hash")],new HashSet<string>{A})[64]);
        Assert.Throws<ArgumentException>(()=>SafeAdvancedTransactions.Pack(Tx(),75,[new(A,"contract:0xaa"),new(A,"contract:0xbb")],new HashSet<string>()));
    }
    [Fact] public void InvalidOperationNativeDelegateValueAndEmptyContractSignatureRejected()
    {
        Assert.Throws<ArgumentException>(()=>SafeAdvancedTransactions.CalculateHash(Tx(2),75));
        Assert.Throws<ArgumentException>(()=>SafeAdvancedTransactions.CalculateHash(Tx(1) with {ValueWei=1},75));
        Assert.Throws<ArgumentException>(()=>SafeAdvancedTransactions.Pack(Tx(),75,[new(A,"contract:0x")],new HashSet<string>()));
    }
}
