using DCWallet.Core;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Xunit;
namespace DCWallet.Tests;
public sealed class SafeReceiptVerifierTests
{
    const string Safe="0x1111111111111111111111111111111111111111";
    const string Other="0x2222222222222222222222222222222222222222";
    static readonly string Hash="0x"+new string('a',64);
    static readonly string OtherHash="0x"+new string('b',64);
    static object Event(string name="ExecutionSuccess",string address=Safe,string? hash=null)=>new {address,topics=new[]{"0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash(name+"(bytes32,uint256)"),hash??Hash},data="0x"+new string('0',64)};
    static TransactionReceipt Receipt(string? status,params object[] logs)=>JsonConvert.DeserializeObject<TransactionReceipt>(JsonConvert.SerializeObject(new{status,logs}))!;
    [Fact]public void MatchingSuccessConfirmsExecution()=>SafeReceiptVerifier.RequireSuccess(Receipt("0x1",Event()),Safe,Hash);
    [Fact]public void WrongEmitterCannotConfirmExecution()=>Assert.Throws<InvalidOperationException>(()=>SafeReceiptVerifier.RequireSuccess(Receipt("0x1",Event(address:Other)),Safe,Hash));
    [Fact]public void DifferentProposalCannotConfirmExecution()=>Assert.Throws<InvalidOperationException>(()=>SafeReceiptVerifier.RequireSuccess(Receipt("0x1",Event(hash:OtherHash)),Safe,Hash));
    [Theory][InlineData("0x0")][InlineData(null)]public void OuterFailureOrUnknownStatusRejectsEvenSuccessEvent(string? status)=>Assert.Throws<InvalidOperationException>(()=>SafeReceiptVerifier.RequireSuccess(Receipt(status,Event()),Safe,Hash));
    [Fact]public void MissingEventDoesNotMeanSuccess()=>Assert.Throws<InvalidOperationException>(()=>SafeReceiptVerifier.RequireSuccess(Receipt("0x1"),Safe,Hash));
    [Fact]public void FailureDominatesSuccess()
    {
        var error=Assert.Throws<InvalidOperationException>(()=>SafeReceiptVerifier.RequireSuccess(Receipt("0x1",Event(),Event("ExecutionFailure")),Safe,Hash));Assert.Contains("ошибку внутреннего",error.Message);
    }
    [Fact]public void FailureOfUnrelatedProposalDoesNotOverrideSuccess()=>SafeReceiptVerifier.RequireSuccess(Receipt("0x1",Event(),Event("ExecutionFailure",hash:OtherHash)),Safe,Hash);
}
