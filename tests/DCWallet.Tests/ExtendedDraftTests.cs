using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Decimal.Web3.Nft;
using Decimal.Web3.Staking;
using Decimal.Web3.TokenCreation;
using Decimal.Web3.TokenEconomy;
using Decimal.Web3.Validators;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
namespace DCWallet.Tests;
public class ExtendedDraftTests
{
    private const string From="0x1111111111111111111111111111111111111111", To="0x2222222222222222222222222222222222222222", Contract="0x3333333333333333333333333333333333333333";
    private static void Selector(string signature,string data) => Assert.StartsWith("0x"+Sha3Keccack.Current.CalculateHash(signature)[..8],data);
    [Fact] public void HoldAndPermitMatchConsoleDelegationAbi()
    {
        var d=new DelegationDrafts(DecimalNetwork.Mainnet(),Contract);
        var hold=d.BuildDelegateHoldDelDraft(From,To,"1",2000000000);
        Selector("delegateHoldDEL(address,uint256)",hold.Data); Assert.Equal(Units.Parse("1"),hold.ValueWei);
        Selector("delegateHold(address,address,uint256,uint256)",d.BuildDelegateHoldDraft(From,To,Contract,7,2000000000).Data);
        Selector("delegateByPermit(address,address,uint256,uint256,uint8,bytes32,bytes32)",d.BuildDelegateByPermitDraft(From,To,Contract,7,2000000000,27,new byte[32],new byte[32]).Data);
        Assert.Throws<ArgumentException>(()=>d.BuildDelegateByPermitDraft(From,To,Contract,7,1,27,new byte[31],new byte[32]));
    }
    [Fact] public void CreationAndEconomyKeepDeploymentTargetAndPayableReserve()
    {
        var creation=new TokenCreationDrafts(Contract);
        var token=creation.BuildCreateTokenDraft(From,"Fixture Token","FIXTURE",100,0,100000,50,"",123);
        Assert.Equal(Contract,token.To); Assert.Equal(new BigInteger(123),token.ValueWei); Assert.True(token.Data.Length>10);
        var reservless=creation.BuildCreateReservelessTokenDraft(From,"Fixture Token","FIXTURE",true,true,100,100000,"");
        Assert.Equal(BigInteger.Zero,reservless.ValueWei); Assert.NotEqual(token.Data[..10],reservless.Data[..10]);
        var nft=new DecimalNftDrafts(DecimalNetwork.Mainnet(),Contract,Contract);
        var nft721=nft.BuildCreateErc721Draft(From,From,"FIXTURE","Fixture","ipfs://fixture",false);
        var nft1155=nft.BuildCreateErc1155Draft(From,From,"FIXTURE","Fixture","ipfs://fixture",false);
        Assert.Equal(Contract,nft721.To); Assert.NotEqual(nft721.Data[..10],nft1155.Data[..10]);
        var economy=new TokenEconomyDrafts(Contract);
        var buy=economy.BuildBuyDraft(From,Contract,123,7,To); Assert.Equal(new BigInteger(123),buy.ValueWei);
        Selector("buy(uint256,address)",buy.Data);
        Selector("sell(uint256,uint256,address)",economy.BuildSellDraft(From,Contract,7,3,To).Data);
    }
    [Fact] public void ValidatorManagementUsesConfiguredMaster()
    {
        var validator=new ValidatorService(new DecimalClient(),Contract);
        var pause=validator.BuildPauseSelfDraft(From); Assert.Equal(Contract,pause.To); Selector("pauseSelf()",pause.Data);
        Selector("unpauseSelf()",validator.BuildUnpauseSelfDraft(From).Data);
        Selector("removeValidator(address)",validator.BuildRemoveValidatorDraft(From,To).Data);
    }
    [Fact] public void SafeTypedSignaturesAreBoundToChainNonceAndExecution()
    {
        var key=new EthECKey("0000000000000000000000000000000000000000000000000000000000000001");
        var safe=SafeTransactionDrafts.BuildErc20Transfer(Contract,To,From,123,0);
        var signature=SafeTransactions.SignTypedData(safe,75,key);
        Assert.NotEqual(SafeTransactions.CalculateHash(safe,75),SafeTransactions.CalculateHash(safe,202020));
        Assert.NotEqual(SafeTransactions.CalculateHash(safe,75),SafeTransactions.CalculateHash(safe with { Nonce=1 },75));
        var packed=SafeTransactions.PackEoaSignatures(safe,75,new[]{signature}); Assert.Equal(132,packed.Length);
        Assert.Throws<ArgumentException>(()=>SafeTransactions.PackEoaSignatures(safe,202020,new[]{signature}));
        Assert.Throws<ArgumentException>(()=>SafeTransactions.PackEoaSignatures(safe,75,new[]{signature,signature}));
        var execution=SafeTransactions.BuildExecutionDraft(From,safe,75,new[]{signature});
        Assert.Equal(Contract,execution.To); Selector("execTransaction(address,uint256,bytes,uint8,uint256,uint256,uint256,address,address,bytes)",execution.Data);
    }
}
