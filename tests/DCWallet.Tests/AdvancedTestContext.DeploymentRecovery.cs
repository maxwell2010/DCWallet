using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Nethereum.RPC.Eth.DTOs;
namespace DCWallet.Tests;
public sealed partial class AdvancedTestContext
{
    // This helper is only for idempotent reads. Never pass a broadcast or signing operation here.
    public static async Task<T> ReadOnlyRetryAsync<T>(Func<Task<T>> read)
    {
        for(var attempt=0;;attempt++)
        {
            try{return await read();}
            catch(Exception e) when(attempt<4 && (e is HttpRequestException or TaskCanceledException || e.Message.Contains("429",StringComparison.OrdinalIgnoreCase) || e.Message.Contains("timeout",StringComparison.OrdinalIgnoreCase)))
            {await Task.Delay(TimeSpan.FromSeconds(3*(attempt+1)));}
        }
    }
    public static async Task<string> RecoverExistingDeploymentAsync(DecimalClient client,string hash,string expectedAddress,string initCodeHex,string runtimeHash,string previousDirectory)
    {
        ValidateDeploymentFixture(initCodeHex,runtimeHash);
        if(!System.Text.RegularExpressions.Regex.IsMatch(hash,"^0x[0-9a-fA-F]{64}$") || !System.Text.RegularExpressions.Regex.IsMatch(expectedAddress,"^0x[0-9a-fA-F]{40}$"))throw new ArgumentException("Explicit deployment hash and address required.");
        if((await ReadOnlyRetryAsync(()=>client.Web3.Eth.ChainId.SendRequestAsync())).Value!=202020)throw new InvalidOperationException("Recovery chain mismatch.");
        var reportPath=Path.Combine(previousDirectory,"report.json");var report=JsonNode.Parse(await File.ReadAllTextAsync(reportPath))!;
        if(report["chainId"]!.GetValue<int>()!=202020 || !report["owner"]!.GetValue<string>().Equals(ExpectedOwner,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Recovery report identity mismatch.");
        var matches=report["cases"]!.AsArray().Where(c=>c?["hash"]?.GetValue<string>().Equals(hash,StringComparison.OrdinalIgnoreCase)==true).ToArray();
        if(matches.Length!=1)throw new InvalidOperationException("Recovery hash missing or ambiguous in existing report.");var row=matches[0]!;
        var tx=await ReadOnlyRetryAsync(()=>client.Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash))??throw new InvalidOperationException("Recovery transaction unavailable.");
        var receipt=await ReadOnlyRetryAsync(()=>client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash))??throw new InvalidOperationException("Recovery receipt unavailable.");
        if(!tx.TransactionHash.Equals(hash,StringComparison.OrdinalIgnoreCase) || !receipt.TransactionHash.Equals(hash,StringComparison.OrdinalIgnoreCase) || !tx.From.Equals(ExpectedOwner,StringComparison.OrdinalIgnoreCase) || tx.Nonce.Value.ToString()!=row["nonce"]!.GetValue<string>() || tx.Value.Value!=0 || !string.IsNullOrEmpty(tx.To) || !tx.Input.Equals(initCodeHex,StringComparison.OrdinalIgnoreCase) || receipt.Status?.Value!=1 || !receipt.ContractAddress.Equals(expectedAddress,StringComparison.OrdinalIgnoreCase) || tx.BlockHash!=receipt.BlockHash)throw new InvalidOperationException("Recovery deployment proof mismatch.");
        var block=await ReadOnlyRetryAsync(()=>client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new BlockParameter(receipt.BlockNumber)));
        if(block.BlockHash!=receipt.BlockHash)throw new InvalidOperationException("Recovery block not canonical.");
        var code=await ReadOnlyRetryAsync(()=>client.Web3.Eth.GetCode.SendRequestAsync(expectedAddress));
        if(Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).ToLowerInvariant()!=runtimeHash)throw new InvalidOperationException("Recovery runtime mismatch.");
        var journalPath=Path.Combine(previousDirectory,hash+".json");var entry=JsonSerializer.Deserialize<JournalEntry>(await File.ReadAllTextAsync(journalPath))??throw new InvalidDataException("Missing recovery journal.");
        if(!entry.Hash.Equals(hash,StringComparison.OrdinalIgnoreCase)||!entry.From.Equals(ExpectedOwner,StringComparison.OrdinalIgnoreCase)||entry.ChainId!=202020||entry.ValueWei!="0"||entry.To!="")throw new InvalidOperationException("Recovery journal mismatch.");
        var fee=receipt.GasUsed.Value*(receipt.EffectiveGasPrice?.Value??tx.GasPrice.Value);
        row["state"]="confirmed";row["block"]=receipt.BlockNumber.Value.ToString();row["paidFeeDel"]=Units.Format(fee);row["contractAddress"]=expectedAddress;row["deploymentRuntimeSha256"]=runtimeHash;
        BigInteger total=0;foreach(var item in report["cases"]!.AsArray()){if(item?["paidFeeDel"] is JsonNode amount)total+=Units.Parse(amount.GetValue<string>());}
        report["paidDel"]=Units.Format(total);report["status"]="reconciled-read-only";
        await File.WriteAllTextAsync(reportPath,report.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
        await File.WriteAllTextAsync(journalPath,JsonSerializer.Serialize(entry with{State="confirmed"}));
        await File.WriteAllTextAsync(Path.Combine(previousDirectory,"deployment-recovery-proof.json"),JsonSerializer.Serialize(new{hash,owner=ExpectedOwner,nonce=tx.Nonce.Value.ToString(),address=expectedAddress,block=receipt.BlockNumber.Value.ToString(),runtimeHash,paidFeeDel=Units.Format(fee),verified="canonical-RPC-read-only",resubmitted=false},new JsonSerializerOptions{WriteIndented=true}));
        return expectedAddress;
    }
}
