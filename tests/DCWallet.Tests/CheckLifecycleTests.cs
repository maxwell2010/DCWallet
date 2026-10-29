using DCWallet.Core;
using Decimal.Web3.Checks;
using Nethereum.Signer;
using Xunit;
namespace DCWallet.Tests;
public sealed class CheckLifecycleTests
{
    [Fact] public void EphemeralSignatureBindsHashAndRedeemer()
    {
        using var secret=new CheckSecret();var hash=new byte[32];hash[31]=7;var redeemer="0x1111111111111111111111111111111111111111";
        var signature="0x"+Convert.ToHexString(secret.SignRedeem(hash,redeemer));
        Assert.Equal(secret.Address,new MessageSigner().EcRecover(ChecksService.CalculateRedeemSigningDigest(hash,redeemer),signature));
        hash[31]=8;Assert.NotEqual(secret.Address,new MessageSigner().EcRecover(ChecksService.CalculateRedeemSigningDigest(hash,redeemer),signature));
        secret.Dispose();Assert.Throws<ObjectDisposedException>(()=>secret.SignRedeem(hash,redeemer));
    }
    [Fact] public void OfficialEthersSignMessageVectorMatchesSingleEip191Prefix()
    {
        // Independently generated with ethers6 Wallet.signMessage(getBytes(solidityPackedKeccak256(bytes32,address))).
        var hash=new byte[32];hash[31]=7;const string owner="0x1111111111111111111111111111111111111111";
        Assert.Equal("335bcf7f9bd55928c57ca6dd05139f13a8b5c0dd0fc46b0e26a073e09c266d67",Convert.ToHexString(ChecksService.CalculateRedeemMessageHash(hash,owner)).ToLowerInvariant());
        var digest=ChecksService.CalculateRedeemSigningDigest(hash,owner);
        Assert.Equal("10aba6a1a6e3c38833b343135e09e00b7e294958de1b520bf401c45fde0263d8",Convert.ToHexString(digest).ToLowerInvariant());
        var signature=new MessageSigner().Sign(digest,new EthECKey(new string('1',64)));
        Assert.Equal("b8802aa887fa8b54ddeb64915e0765774851d99f040e5264b9d220e145001fec5d38b9b1e0f379605e0ee8efb5a2d9aae811566b6eb7d7c7303380d6f850944f1b",signature.TrimStart('0','x'));
    }    [Fact] public void ProtectedBearerRoundTripsWithoutCleartextExport() { using var secret=new CheckSecret();var encrypted=secret.ExportProtected(bytes=>bytes.Select(b=>(byte)(b^0xA7)).ToArray());using var restored=CheckSecret.FromProtected(encrypted,bytes=>bytes.Select(b=>(byte)(b^0xA7)).ToArray());Assert.Equal(secret.Address,restored.Address);var hash=new byte[32];hash[31]=1;Assert.Equal(secret.SignRedeem(hash,"0x1111111111111111111111111111111111111111"),restored.SignRedeem(hash,"0x1111111111111111111111111111111111111111")); }
    [Fact] public async Task DeployedNonceViewIncludesCreatorFrom()
    {
        const string owner="0x1111111111111111111111111111111111111111";const string contract="0x2222222222222222222222222222222222222222";
        using var rpc=new FakeRpc{ReadResult=(method,args)=>method=="eth_chainId"?"0x4b":args[0].TryGetProperty("from",out var from)&&from.GetString()==owner?"0x"+new string('0',63)+"6":"0x"+new string('0',64)};
        var client=new Decimal.Web3.DecimalClient(Decimal.Web3.DecimalNetwork.Mainnet() with{RpcUrl=new Uri(rpc.Url)});
        Assert.Equal(6,await CheckLifecycle.GetNonceForAsync(client,contract,owner));
        Assert.Equal(0,await new ChecksService(client.Web3,contract).GetNonceAsync());
    }}
