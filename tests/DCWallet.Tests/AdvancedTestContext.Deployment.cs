using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
namespace DCWallet.Tests;
public sealed partial class AdvancedTestContext
{
    public static void ValidateDeploymentFixture(string initCodeHex,string expectedRuntimeSha256)
    {
        if(initCodeHex is null || !initCodeHex.StartsWith("0x",StringComparison.Ordinal) || initCodeHex.Length is <4 or >8192) throw new ArgumentException("Bounded QA initcode required.");
        var hash=Convert.ToHexString(SHA256.HashData(Convert.FromHexString(initCodeHex[2..]))).ToLowerInvariant();
        var expected=hash switch {
            "44abb99125b751f9343e03678280fa11cc7287241a5f79adf4a57687741248e3" => SafeAdvancedNetworkScenario.OwnerRuntimeSha256,
            "aafba9be5da366f766e207e707c865bd7e8b60afbc7ed8b5607bbe7948d029ed" => SafeAdvancedNetworkScenario.DelegateRuntimeSha256,
            _ => throw new InvalidOperationException("Initcode not on the explicit two-fixture QA allowlist.")
        };
        if(expectedRuntimeSha256!=expected) throw new InvalidOperationException("QA runtime/initcode pair mismatch.");
    }
    public async Task<string> DeployAsync(string label,string initCodeHex,string expectedRuntimeSha256)
    {
        ValidateDeploymentFixture(initCodeHex,expectedRuntimeSha256);
        if(stopped || Owner!=ExpectedOwner || Environment.GetEnvironmentVariable("DC_ADVANCED_TESTNET_WRITE")!="1") throw new InvalidOperationException("QA deployment campaign unavailable.");
        if((await Client.Web3.Eth.ChainId.SendRequestAsync()).Value!=202020) throw new InvalidOperationException("Wrong deployment chain.");
        var account=new Nethereum.HdWallet.Wallet(phrase ?? throw new ObjectDisposedException(nameof(AdvancedTestContext)),"",Nethereum.HdWallet.Wallet.DEFAULT_PATH).GetAccount(0);
        if(!account.Address.Equals(ExpectedOwner,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Wrong deployment owner.");
        var input=new TransactionInput { From=Owner,To=null,Data=initCodeHex,Value=new HexBigInteger(0) };
        var estimate=(await Client.Web3.Eth.Transactions.EstimateGas.SendRequestAsync(input)).Value;
        var gasLimit=(estimate*120+99)/100;
        var gasPrice=(await Client.Web3.Eth.GasPrice.SendRequestAsync()).Value;
        if(gasLimit<=0 || gasPrice<=0)throw new InvalidOperationException("Invalid deployment gas quote.");
        var maximumFee=gasLimit*gasPrice;CheckBudget(maximumFee,0);
        if((await Client.Web3.Eth.GetBalance.SendRequestAsync(Owner)).Value<maximumFee)throw new InvalidOperationException("QA deployment balance insufficient.");
        var nonce=(await Client.Web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(Owner,BlockParameter.CreatePending())).Value;
        input.Nonce=new HexBigInteger(nonce);input.Gas=new HexBigInteger(gasLimit);input.GasPrice=new HexBigInteger(gasPrice);
        var raw=new AccountOfflineTransactionSigner().SignTransaction(account,input,202020);
        var signed=new SignedTransaction(raw,Owner,"",nonce,gasLimit,gasPrice);
        if((await Client.Web3.Eth.ChainId.SendRequestAsync()).Value!=202020 || (await Client.Web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(Owner,BlockParameter.CreatePending())).Value!=nonce) throw new InvalidOperationException("Deployment chain/nonce changed before submission.");
        reserved+=maximumFee;
        var row=new Dictionary<string,object?> { ["label"]=label,["hash"]=signed.Hash,["nonce"]=nonce.ToString(),["to"]="",["valueWei"]="0",["maximumFeeDel"]=Units.Format(maximumFee),["deploymentRuntimeSha256"]=expectedRuntimeSha256,["state"]="signed-local-hash-recorded" };
        cases.Add(row);Save("running");
        var entry=new JournalEntry(signed.Hash,Owner,"","0",202020,"sending",DateTimeOffset.UtcNow);
        var journal=Path.Combine(DirectoryPath,signed.Hash+".json");
        await File.WriteAllTextAsync(journal,JsonSerializer.Serialize(entry));
        try
        {
            var submitted=await Client.BroadcastAsync(signed);
            if(!submitted.Hash.Equals(signed.Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Deployment RPC hash mismatch.");
            row["state"]="submitted";Save("running");await File.WriteAllTextAsync(journal,JsonSerializer.Serialize(entry with{State="submitted"}));
            var result=await ReadOnlyRetryAsync(()=>Client.WaitForReceiptAsync(signed.Hash,TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(4)));
            if(result.Success is null) throw new InvalidOperationException("Deployment receipt unavailable; no resend.");
            row["state"]=result.Success==true?"confirmed":"reverted";row["block"]=result.BlockNumber?.ToString();row["paidFeeDel"]=result.PaidFeeDel;paid+=result.PaidFeeWei??0;Save("running");
            await File.WriteAllTextAsync(journal,JsonSerializer.Serialize(entry with{State=result.Success==true?"confirmed":"reverted"}));
            if(result.Success!=true)throw new InvalidOperationException("QA deployment reverted.");
            var receipt=await ReadOnlyRetryAsync(()=>Client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(signed.Hash))??throw new InvalidOperationException("Deployment receipt missing.");
            var address=receipt.ContractAddress;
            if(!System.Text.RegularExpressions.Regex.IsMatch(address??"","^0x[0-9a-fA-F]{40}$"))throw new InvalidOperationException("No created contract address.");
            var code=await Client.Web3.Eth.GetCode.SendRequestAsync(address);
            if(Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).ToLowerInvariant()!=expectedRuntimeSha256)throw new InvalidOperationException("Created runtime does not match reviewed QA fixture.");
            row["contractAddress"]=address;Save("running");return address!;
        }
        catch
        {
            stopped=true;
            if((string?)row["state"] is "signed-local-hash-recorded" or "submitted") {row["state"]="unknown-stop-no-resend";await File.WriteAllTextAsync(journal,JsonSerializer.Serialize(entry with{State="unknown-check-hash"}));}
            Save("stopped");throw new InvalidOperationException("QA deployment stopped. Inspect recorded public hash; never automatically resend.");
        }
    }
}
