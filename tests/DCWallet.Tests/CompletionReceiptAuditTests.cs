using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
namespace DCWallet.Tests;
public sealed class CompletionAuditFactAttribute:FactAttribute
{ public CompletionAuditFactAttribute(){if(Environment.GetEnvironmentVariable("DC_COMPLETION_AUDIT")!="1")Skip="Explicit completion read-only receipt audit required.";} }
public sealed class CompletionReceiptAuditTests
{
    [CompletionAuditFact,Trait("Category","TestnetRead")]
    public async Task EveryCompletionHashHasCanonicalReceiptMatchingJournalAndFee()
    {
        var root=Environment.GetEnvironmentVariable("DC_COMPLETION_ROOT")??throw new InvalidOperationException("Explicit completion artifact directory required.");
        var client=new DecimalClient(DecimalNetwork.Testnet()with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")});
        async Task<T> Read<T>(Func<Task<T>> read){await Task.Delay(2000);return await AdvancedTestContext.ReadOnlyRetryAsync(read);}
        Assert.Equal(202020,(await Read(()=>client.Web3.Eth.ChainId.SendRequestAsync())).Value);
        var rows=new List<(string Directory,JsonElement Row)>();
        foreach(var directory in Directory.GetDirectories(root).Order())
        {
            var path=Path.Combine(directory,"report.json");if(!File.Exists(path))continue;
            using var document=JsonDocument.Parse(await File.ReadAllTextAsync(path));var report=document.RootElement;
            Assert.Equal(202020,report.GetProperty("chainId").GetInt32());Assert.Equal(AdvancedTestContext.ExpectedOwner,report.GetProperty("owner").GetString(),StringComparer.OrdinalIgnoreCase);
            foreach(var row in report.GetProperty("cases").EnumerateArray())rows.Add((directory,row.Clone()));
        }
        Assert.NotEmpty(rows);
        var outputs=new List<object>();var total=BigInteger.Zero;var nonces=new HashSet<BigInteger>();var inner=0;var journals=0;
        foreach(var group in rows.GroupBy(r=>r.Row.GetProperty("hash").GetString()!,StringComparer.OrdinalIgnoreCase))
        {
            var hash=group.Key;var expected=group.First().Row;
            var tx=await Read(()=>client.Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash));Assert.NotNull(tx);
            var receipt=await Read(()=>client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash));Assert.NotNull(receipt);
            Assert.Equal(hash,tx.TransactionHash,StringComparer.OrdinalIgnoreCase);Assert.Equal(hash,receipt.TransactionHash,StringComparer.OrdinalIgnoreCase);Assert.Equal(1,receipt.Status.Value);
            Assert.Equal(AdvancedTestContext.ExpectedOwner,tx.From,StringComparer.OrdinalIgnoreCase);Assert.True(nonces.Add(tx.Nonce.Value),"Same QA nonce used by different hashes.");
            var block=await Read(()=>client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new BlockParameter(receipt.BlockNumber)));
            Assert.Equal(receipt.BlockHash,block.BlockHash);Assert.Equal(tx.BlockHash,receipt.BlockHash);
            var fee=receipt.GasUsed.Value*(receipt.EffectiveGasPrice?.Value??tx.GasPrice.Value);
            foreach(var recorded in group)
            {
                var row=recorded.Row;Assert.Equal(BigInteger.Parse(row.GetProperty("nonce").GetString()!),tx.Nonce.Value);
                Assert.Equal(row.GetProperty("to").GetString()??"",tx.To??"",StringComparer.OrdinalIgnoreCase);Assert.Equal(BigInteger.Parse(row.GetProperty("valueWei").GetString()!),tx.Value.Value);
                Assert.True(fee<=Units.Parse(row.GetProperty("maximumFeeDel").GetString()!));
                if(row.TryGetProperty("paidFeeDel",out var paid))Assert.Equal(Units.Parse(paid.GetString()!),fee);
                if(row.TryGetProperty("deploymentRuntimeSha256",out var runtime))
                {
                    Assert.True(string.IsNullOrEmpty(tx.To));Assert.Equal(0,tx.Value.Value);Assert.False(string.IsNullOrEmpty(receipt.ContractAddress));
                    AdvancedTestContext.ValidateDeploymentFixture(tx.Input,runtime.GetString()!);
                    var code=await Read(()=>client.Web3.Eth.GetCode.SendRequestAsync(receipt.ContractAddress));Assert.Equal(runtime.GetString(),Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).ToLowerInvariant());
                }
                var journalPath=Path.Combine(recorded.Directory,hash+".json");Assert.True(File.Exists(journalPath));
                var journal=JsonSerializer.Deserialize<JournalEntry>(await File.ReadAllTextAsync(journalPath))!;
                Assert.Equal(hash,journal.Hash,StringComparer.OrdinalIgnoreCase);Assert.Equal(tx.From,journal.From,StringComparer.OrdinalIgnoreCase);Assert.Equal(tx.To??"",journal.To??"",StringComparer.OrdinalIgnoreCase);Assert.Equal(tx.Value.Value.ToString(),journal.ValueWei);Assert.Equal(202020,journal.ChainId);
                // Mutation only follows a full canonical transaction/receipt match; no sending API is used.
                await File.WriteAllTextAsync(journalPath,JsonSerializer.Serialize(journal with{State="confirmed"}));journals++;
            }
            var label=expected.GetProperty("label").GetString()!;
            if(label.StartsWith("Safe validated contract-owner",StringComparison.Ordinal))
            {
                var bytes=Convert.FromHexString(tx.Input[2..]);
                BigInteger Word(int index)=>new(bytes.AsSpan(4+index*32,32),true,true);
                string Address(int index)=>"0x"+Convert.ToHexString(bytes.AsSpan(4+index*32+12,20)).ToLowerInvariant();
                var offset=checked((int)Word(2));var length=checked((int)new BigInteger(bytes.AsSpan(4+offset,32),true,true));var data="0x"+Convert.ToHexString(bytes.AsSpan(4+offset+32,length)).ToLowerInvariant();
                var proposal=new SafeTransactionDraft(tx.To!,Address(0),Word(1),data,(byte)Word(3),Word(4),Word(5),Word(6),Address(7),Address(8),label.Contains("DelegateCall",StringComparison.Ordinal)?1:0);
                SafeReceiptVerifier.RequireSuccess(receipt,tx.To!,SafeAdvancedTransactions.CalculateHash(proposal,202020));
                Assert.Single(receipt.DecodeAllEvents<SafeReceiptVerifier.SafeExecutionSuccess>(),e=>e.Log.Address.Equals(tx.To,StringComparison.OrdinalIgnoreCase));inner++;
            }
            total+=fee;outputs.Add(new{hash,label,nonce=tx.Nonce.Value.ToString(),to=tx.To??"",valueWei=tx.Value.Value.ToString(),block=receipt.BlockNumber.Value.ToString(),feeDel=Units.Format(fee),verified="canonical-rpc"});
        }
        await File.WriteAllTextAsync(Path.Combine(root,"completion-receipt-audit.json"),JsonSerializer.Serialize(new{chainId=202020,owner=AdvancedTestContext.ExpectedOwner,confirmed=outputs.Count,journalRows=journals,pending=0,safeInnerVerified=inner,totalFeeDel=Units.Format(total),cases=outputs},new JsonSerializerOptions{WriteIndented=true}));
    }
}
