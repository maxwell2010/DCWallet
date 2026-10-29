using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Bridge;
using Decimal.Web3.Checks;
using Decimal.Web3.Multisend;
using Decimal.Web3.Nft;
using Decimal.Web3.Staking;
using Decimal.Web3.Tokens;
using Decimal.Web3.Transactions;
using Xunit;

namespace DCWallet.Tests;
public class DraftEncodingTests
{
    private const string From = "0x1111111111111111111111111111111111111111", To = "0x2222222222222222222222222222222222222222", Contract = "0x3333333333333333333333333333333333333333";
    [Fact] public void TokenTransferApproveAndTransferFromMatchStandardSelectorsAndAmounts()
    {
        using var rpc = new FakeRpc(); var client = new DecimalClient(DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) });
        var token = new Erc20Service(client, Contract); var huge = (BigInteger.One << 255) + 123;
        foreach (var (draft, selector) in new[] {
            (token.BuildTransferDraft(From,To,huge), "0xa9059cbb"),
            (token.BuildApproveDraft(From,To,huge), "0x095ea7b3"),
            (token.BuildTransferFromDraft(From,To,From,huge), "0x23b872dd") })
        {
            Assert.StartsWith(selector,draft.Data); Assert.EndsWith(huge.ToString("x").TrimStart('0'), draft.Data);
            Assert.Equal(Contract,draft.To); Assert.Equal(BigInteger.Zero,draft.ValueWei);
        }
        Assert.Equal(0,rpc.Broadcasts);
    }
    [Fact] public void NftTransfersMatchErc721AndErc1155Selectors()
    {
        var client = new DecimalClient(); var erc721 = new Erc721Service(client); var erc1155 = new Erc1155Service(client);
        Assert.StartsWith("0x23b872dd", erc721.TransferFrom(From,Contract,To,7).Data);
        Assert.StartsWith("0x42842e0e", erc721.SafeTransferFrom(From,Contract,To,7).Data);
        Assert.StartsWith("0xf242432a", erc1155.SafeTransferFrom(From,Contract,To,7,9).Data);
        Assert.StartsWith("0x2eb2c2d6", erc1155.SafeBatchTransferFrom(From,Contract,To,new BigInteger[]{7,8},new BigInteger[]{9,10}).Data);
        Assert.Throws<ArgumentException>(() => erc1155.SafeBatchTransferFrom(From,Contract,To,new BigInteger[]{7},new BigInteger[]{9,10}));
        Assert.Throws<ArgumentOutOfRangeException>(() => erc1155.SafeTransferFrom(From,Contract,To,7,0));
    }
    [Fact] public void PayableDraftFamiliesSetExactValueAndRejectOverflow()
    {
        var bridge = new BridgeDrafts(Contract);
        Assert.Equal(new BigInteger(123 + 7), bridge.BuildTransferNative(From,To,123,7,1,0).ValueWei);
        Assert.Equal(new BigInteger(7), bridge.BuildTransferToken(From,Contract,To,123,7,1,0).ValueWei);
        Assert.Throws<ArgumentOutOfRangeException>(() => bridge.BuildTransferNative(From,To,(BigInteger.One << 256)-1,1,1,0));
        var checks = new ChecksService(new DecimalClient().Web3,Contract);
        Assert.Equal(new BigInteger(246), checks.BuildCreateDelDraft(From,new[]{From,To},123,100,0).ValueWei);
        var staking = new DelegationDrafts(DecimalNetwork.Mainnet(),Contract);
        Assert.Equal(BigInteger.One,staking.BuildDelegateDelDraft(From,To,"0.000000000000000001").ValueWei);
        var multisend = MultisendDel.Build(From,new[]{new DelRecipient(To,"0.000000000000000001"),new DelRecipient(From,"0.000000000000000002")},aggregateContract:Contract);
        Assert.Equal(new BigInteger(3),multisend.ValueWei); Assert.Equal(Contract,multisend.To);
    }
}
