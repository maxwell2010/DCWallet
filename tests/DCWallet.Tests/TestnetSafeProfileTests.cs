using System.Numerics;
using DCWallet.Core;
using Xunit;
namespace DCWallet.Tests;
public class TestnetSafeProfileTests
{
    const string Owner="0x1111111111111111111111111111111111111111";
    [Fact] public void ExplicitTestnetCreationHasWeightedSetupAndNoPayment()
    {
        var draft=TestnetSafeProfile.Create(Owner,[new(Owner,2)],2,77,202020);
        Assert.Equal(TestnetSafeProfile.Factory,draft.To);Assert.Equal(BigInteger.Zero,draft.ValueWei);
        Assert.StartsWith("0x1688f0b9",draft.Data);
        Assert.Contains(TestnetSafeProfile.Singleton[2..],draft.Data);
    }
    [Fact] public void RejectsOtherChainsAndInvalidQuorum()
    {
        Assert.Throws<InvalidOperationException>(()=>TestnetSafeProfile.Create(Owner,[new(Owner,1)],1,77,75));
        Assert.Throws<ArgumentException>(()=>TestnetSafeProfile.Create(Owner,[new(Owner,1)],2,77,202020));
    }
    [Theory][InlineData(true)][InlineData(false)] public void CreationReceiptSupportsBothFactoryLayouts(bool indexed)
    {
        var signature="0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash("ProxyCreation(address,address)");
        var proxy="0x"+Owner[2..].PadLeft(64,'0');var singleton=TestnetSafeProfile.Singleton[2..].PadLeft(64,'0');
        var log=new {address=TestnetSafeProfile.Factory,topics=indexed?new[]{signature,proxy}:new[]{signature},data=indexed?"0x"+singleton:proxy+singleton};
        var receipt=Newtonsoft.Json.JsonConvert.DeserializeObject<Nethereum.RPC.Eth.DTOs.TransactionReceipt>(Newtonsoft.Json.JsonConvert.SerializeObject(new {status="0x1",logs=new[]{log}}))!;
        Assert.Equal(Owner,TestnetSafeProfile.CreatedAddress(receipt));
    }
}
