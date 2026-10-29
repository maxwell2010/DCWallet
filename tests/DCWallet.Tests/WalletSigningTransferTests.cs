using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Xunit;
namespace DCWallet.Tests;
public sealed class WalletSigningTransferTests
{
 [Fact]public async Task PrivateKeySessionUsesSameOneAttemptJournalContract(){using var rpc=new FakeRpc();using var signer=WalletSigningSession.FromPrivateKey(new string('0',63)+"1",75);var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));try{var session=new TransferSession(new DecimalClient(DecimalNetwork.Custom("fixture",75,rpc.Url)),directory);var transfer=await session.PrepareAsync(TransactionDraft.Del(signer.Address,signer.Address,"0.001"),signer);Assert.Equal(0,rpc.Broadcasts);await Assert.ThrowsAnyAsync<Exception>(()=>session.SendAsync(transfer));Assert.Equal(1,rpc.Broadcasts);await Assert.ThrowsAsync<InvalidOperationException>(()=>session.SendAsync(transfer));Assert.Equal(1,rpc.Broadcasts);var json=await File.ReadAllTextAsync(Path.Combine(directory,transfer.Signed.Hash+".json"));Assert.Contains("unknown-check-hash",json);Assert.DoesNotContain(transfer.Signed.RawHex,json);}finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}}
}
