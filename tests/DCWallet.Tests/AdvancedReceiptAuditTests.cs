using System.Numerics;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Decimal.Web3.Nft;
using Decimal.Web3.Staking;
using Xunit;
namespace DCWallet.Tests;
public sealed class AdvancedAuditFactAttribute:FactAttribute
{public AdvancedAuditFactAttribute(){if(Environment.GetEnvironmentVariable("DC_ADVANCED_AUDIT")!="1")Skip="Explicit read-only advanced receipt audit opt-in required";}}
public class AdvancedReceiptAuditTests
{
    [AdvancedAuditFact,Trait("Category","TestnetRead")]
    public async Task PreviouslyRedeemedCheckPublicCalldataIsRejectedWithoutSending()
    {
        var client=new DecimalClient(DecimalNetwork.Testnet()with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")});
        Assert.Equal(202020,(await client.Web3.Eth.ChainId.SendRequestAsync()).Value);
        var tx=await client.Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync("0xd09425ea6118d00080cfc3440189249b541dfc272b66c94c4730b924ea9206c1");
        Assert.Equal("0xb92bd490c8e9e5d9c46d71666418d4e7ae4534e9",tx.From,StringComparer.OrdinalIgnoreCase);Assert.Equal("0xb6161CAA8Bd7167C2fa67b93b041FF9a94d6a735",tx.To,StringComparer.OrdinalIgnoreCase);
        var rejected=false;
        for(var retry=0;;retry++)
        {
            await Task.Delay(2000);
            try{await client.Web3.Eth.Transactions.Call.SendRequestAsync(new Nethereum.RPC.Eth.DTOs.CallInput{From=tx.From,To=tx.To,Data=tx.Input,Value=new Nethereum.Hex.HexTypes.HexBigInteger(0)});break;}
            catch(Exception e)when(e.ToString().Contains("429")&&retry<5){await Task.Delay(8000);}
            catch(Nethereum.JsonRpc.Client.RpcResponseException e)when(e.Message.Contains("revert",StringComparison.OrdinalIgnoreCase)){rejected=true;break;}
        }
        Assert.True(rejected,"Redeemed check replay was not rejected by the current contract.");
        var root=Environment.GetEnvironmentVariable("DC_ADVANCED_ROOT")??throw new InvalidOperationException("Public report root required.");
        await File.WriteAllTextAsync(Path.Combine(root,"checks-replay-audit.json"),JsonSerializer.Serialize(new{chainId=202020,replayedTransaction=tx.TransactionHash,ethCallRejected=true,broadcast=false}));
    }
    [AdvancedAuditFact,Trait("Category","TestnetRead")]
    public async Task EveryAdvancedWriteHasCanonicalReceiptAndConfirmedJournal()
    {
        var root=Environment.GetEnvironmentVariable("DC_ADVANCED_ROOT")??throw new InvalidOperationException("Public advanced report root required.");
        const string owner="0xb92bd490c8e9e5d9c46d71666418d4e7ae4534e9",safe="0x5b0e88c7e447c7ccf00b7298280ade5594be2e71";
        var client=new DecimalClient(DecimalNetwork.Testnet()with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")});var history=new HistoryReader(client,false);
        async Task<T> Read<T>(Func<Task<T>> action){for(var retry=0;;retry++){await Task.Delay(2000);try{return await action();}catch(Exception e)when(retry<5&&e.ToString().Contains("429")){await Task.Delay(TimeSpan.FromSeconds(8*(retry+1)));}}}
        var hashes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var nonces=new HashSet<BigInteger>();var total=BigInteger.Zero;var output=new List<object>();var dirs=new List<string>();
        foreach(var dir in Directory.GetDirectories(root).OrderBy(d=>d))
        {
            var file=Path.Combine(dir,"report.json");if(!File.Exists(file))continue;
            using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(file));var report=doc.RootElement;Assert.Equal(202020,report.GetProperty("chainId").GetInt32());Assert.Equal(owner,report.GetProperty("owner").GetString());
            var count=0;var runFee=BigInteger.Zero;
            foreach(var item in report.GetProperty("cases").EnumerateArray())
            {
                if(item.GetProperty("state").GetString()!="confirmed")continue;
                var hash=item.GetProperty("hash").GetString()!;Assert.True(hashes.Add(hash),"Repeated confirmed hash across advanced runs.");count++;
                var row=await Read(()=>history.VerifyAsync(hash,owner));Assert.NotNull(row);Assert.True(row.Success);Assert.Equal("rpc",row.Verification);
                Assert.Equal(owner,row.From,StringComparer.OrdinalIgnoreCase);Assert.Equal(item.GetProperty("to").GetString(),row.To,StringComparer.OrdinalIgnoreCase);Assert.Equal(BigInteger.Parse(item.GetProperty("valueWei").GetString()!),row.ValueWei);
                Assert.Equal(Units.Parse(item.GetProperty("paidFeeDel").GetString()!),row.FeeWei);Assert.True(row.FeeWei<=Units.Parse(item.GetProperty("maximumFeeDel").GetString()!));runFee+=row.FeeWei;
                var tx=await Read(()=>client.Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash));Assert.True(nonces.Add(tx.Nonce.Value));Assert.Equal(BigInteger.Parse(item.GetProperty("nonce").GetString()!),tx.Nonce.Value);
                var receipt=await Read(()=>client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash));var label=item.GetProperty("label").GetString()!;
                if(label=="Safe EOA execute return 0.0004 DEL"||label=="Safe approved_hash execute return 0.0006 DEL")
                {
                    var first=label.Contains("EOA");var proposal=SafeTransactionDrafts.BuildDel(safe,owner,Units.Parse(first?"0.0004":"0.0006"),first?0:1);SafeReceiptVerifier.RequireSuccess(receipt,safe,SafeTransactions.CalculateHash(proposal,202020));
                }
                if(label=="Safe quorum execute two EOA signatures")
                {
                    var quorumSafe=item.GetProperty("to").GetString()!;var proposal=SafeTransactionDrafts.BuildDel(quorumSafe,owner,1,0);
                    SafeReceiptVerifier.RequireSuccess(receipt,quorumSafe,SafeTransactions.CalculateHash(proposal,202020));
                }
                if(label=="checks redeem new check")Assert.Equal(1,CheckLifecycle.RedeemStatus(receipt,"0xb6161CAA8Bd7167C2fa67b93b041FF9a94d6a735",Convert.FromHexString("473360412a69c5f4aa9f7ac8de8a0bf15feeb64b5da98c39da538e08f8ee8604")));
                output.Add(new{label,hash,nonce=tx.Nonce.Value.ToString(),feeDel=Units.Format(row.FeeWei),verification=row.Verification});
            }
            Assert.Equal(Units.Parse(report.GetProperty("paidDel").GetString()!),runFee);total+=runFee;if(count>0)dirs.Add(dir);
        }
        Assert.Equal(13,hashes.Count);Assert.True(total<Units.Parse("5"));
        var frozen=await Read(()=>new FrozenStakeReader(client.Web3,CombinedNftDelegation.Proxy).GetFrozenStakeAsync(226));Assert.Equal(0,frozen.FreezeStatus);
        var balance=await Read(()=>new Erc1155Service(client).BalanceOfAsync("0x918e742d8dd0de7e7930fe0489cdbce1701fb29d",owner,1));Assert.Equal(BigInteger.One,balance);
        var journalRows=0;
        foreach(var dir in dirs)
        {
            var reconciliation=await Read(()=>new JournalReconciler(client,TimeSpan.FromSeconds(2)).ReconcileAsync(dir,owner));
            for(var retry=0;reconciliation.Pending>0&&retry<3;retry++){await Task.Delay(15000);reconciliation=await Read(()=>new JournalReconciler(client,TimeSpan.FromSeconds(3)).ReconcileAsync(dir,owner));}
            Assert.Empty(reconciliation.Issues);Assert.Equal(0,reconciliation.Pending);journalRows+=reconciliation.Rows.Count;
        }
        Assert.Equal(13,journalRows);
        await File.WriteAllTextAsync(Path.Combine(root,"advanced-receipt-audit.json"),JsonSerializer.Serialize(new{chainId=202020,owner,confirmed=hashes.Count,totalFeeDel=Units.Format(total),journalRows,pending=0,nftFrozen226Status=0,nftBalance="1",safeInnerSuccess=true,checksRedeemedStatus=1,cases=output},OperationCatalog.Json));
    }
}
