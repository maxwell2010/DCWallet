using System.Numerics;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Checks;
using Decimal.Web3.Staking;
using Decimal.Web3.TokenCreation;
using Decimal.Web3.Transactions;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
namespace DCWallet.Tests;
public sealed class LocalForkFactAttribute:FactAttribute{public LocalForkFactAttribute(){if(Environment.GetEnvironmentVariable("DC_LOCAL_FORK_TEST")!="1")Skip="Explicit localhost-only Ganache fork opt-in required";}}
[CollectionDefinition("LocalFork",DisableParallelization=true)]public class LocalForkCollection{}
[Collection("LocalFork")]
public sealed class ForkCompletionTests
{
    const string Url="http://127.0.0.1:18545",Owner="0xb92bd490c8e9e5d9c46d71666418d4e7ae4534e9",Master="0x35119df12afdf848b7ef2536af2411ab0a611c45",Center="0xb8f9c4a26b91a65ac0d3f34e230a4c9ad09aef4a";
    static string ArtifactFolder {get {var root=Path.GetFullPath(AppContext.BaseDirectory);while(!Directory.Exists(Path.Combine(root,"artifacts/fork-qa"))&&Directory.GetParent(root) is {} parent)root=parent.FullName;return Path.Combine(root,"artifacts/fork-qa");}}
    readonly DecimalClient local=new(DecimalNetwork.Testnet() with{RpcUrl=new Uri(Url)});
    static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(60)};
    static async Task<JsonElement> Rpc(string method,params object[] parameters)
    {if(new Uri(Url).Host!="127.0.0.1"||new Uri(Url).Port!=18545)throw new InvalidOperationException();var response=await Http.PostAsync(Url,new StringContent(JsonSerializer.Serialize(new{jsonrpc="2.0",id=1,method,@params=parameters}),System.Text.Encoding.UTF8,"application/json"));using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());if(doc.RootElement.TryGetProperty("error",out var error))throw new InvalidOperationException("Local fork RPC rejected: "+error.GetProperty("message").GetString());return doc.RootElement.GetProperty("result").Clone();}
    async Task Profile()
    {
        Assert.Equal(202020,(await local.Web3.Eth.ChainId.SendRequestAsync()).Value);var version=(await Rpc("web3_clientVersion")).GetString();Assert.Contains("Ganache/v7.9.2",version);
        var root=Path.GetFullPath(AppContext.BaseDirectory);while(!Directory.Exists(Path.Combine(root,"artifacts/fork-qa"))&&Directory.GetParent(root) is {} parent)root=parent.FullName;
        var ready=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"artifacts/fork-qa/server-ready.json")));var number=ready.RootElement.GetProperty("pinnedHead").GetString()!;var block=new BlockParameter(new HexBigInteger(number));var upstream=new DecimalClient(DecimalNetwork.Testnet() with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")});
        var upstreamBlock=await upstream.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(block);var forkBlock=await local.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(block);var canonicalHeaderMatches=upstreamBlock.BlockHash==forkBlock.BlockHash;
        var balance=await upstream.Web3.Eth.GetBalance.SendRequestAsync(Owner,block);Assert.Equal(balance.Value,(await local.Web3.Eth.GetBalance.SendRequestAsync(Owner,block)).Value);
        var upstreamCode=await upstream.Web3.Eth.GetCode.SendRequestAsync(CombinedNftDelegation.Proxy,block);Assert.Equal(upstreamCode,await local.Web3.Eth.GetCode.SendRequestAsync(CombinedNftDelegation.Proxy,block));
        var slot=new HexBigInteger(BigInteger.Parse("0360894A13BA1A3210667C828492DB98DCA3E2076CC3735A920A3CA505D382BBC",System.Globalization.NumberStyles.HexNumber));var storage=await upstream.Web3.Eth.GetStorageAt.SendRequestAsync(CombinedNftDelegation.Proxy,slot,block);Assert.Equal(storage,await local.Web3.Eth.GetStorageAt.SendRequestAsync(CombinedNftDelegation.Proxy,slot,block));
        File.WriteAllText(Path.Combine(root,"artifacts/fork-qa/profile-proof.json"),JsonSerializer.Serialize(new{kind="local Ganache fork, not public network proof",chainId=202020,version,number,canonicalBlockHash=upstreamBlock.BlockHash,localForkBlockHash=forkBlock.BlockHash,canonicalHeaderMatches,balanceMatched=true,codeMatched=true,storageMatched=true,earliestMetadataFallback=true,publicBroadcast=false}));
    }
    async Task<TransactionReceipt> Send(TransactionDraft draft,bool success=true)
    {Assert.Equal(202020,(await local.Web3.Eth.ChainId.SendRequestAsync()).Value);var suggested=await local.Web3.Eth.GasPrice.SendRequestAsync();var latest=await local.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());var price=new HexBigInteger(BigInteger.Max(suggested.Value,(latest.BaseFeePerGas?.Value??0)*2+2000000000));var tx=new{from=draft.From,to=draft.To,data=draft.Data,value=new HexBigInteger(draft.ValueWei).HexValue,gas="0x989680",gasPrice=price.HexValue};var hash=(await Rpc("eth_sendTransaction",tx)).GetString();var receipt=await local.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash);Assert.NotNull(receipt);Assert.Equal(success?BigInteger.One:BigInteger.Zero,receipt.Status.Value);File.AppendAllText(Path.Combine(ArtifactFolder,"receipts.jsonl"),JsonSerializer.Serialize(new{kind="local fork only",receipt.TransactionHash,from=draft.From,to=draft.To,valueWei=draft.ValueWei.ToString(),selector=draft.Data.Length>=10?draft.Data[..10]:draft.Data,status=receipt.Status.Value.ToString(),block=receipt.BlockNumber.Value.ToString(),gasUsed=receipt.GasUsed.Value.ToString(),publicBroadcast=false})+Environment.NewLine);return receipt;}
    [LocalForkFact] public async Task ForkMatchesCanonicalCodeStorageBalanceBeforeAnyLocalMutation()=>await Profile();
    [LocalForkFact] public async Task CheckExpiryRejectsRedeemAfterDueBlockOnActualForkBytecode()
    {
        await Profile();var snapshot=(await Rpc("evm_snapshot")).GetString();try{await Rpc("evm_setAccountBalance",Owner,"0x3635c9adc5dea00000");await ChecksDeploymentProfile.VerifyAsync(local,ChecksDeploymentProfile.TestnetContract);using var secret=new CheckSecret();var svc=new ChecksService(local.Web3,ChecksDeploymentProfile.TestnetContract);var head=(await local.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;var nonce=await CheckLifecycle.GetNonceForAsync(local,ChecksDeploymentProfile.TestnetContract,Owner);var created=await Send(svc.BuildCreateDelDraft(Owner,new[]{secret.Address},Units.Parse("0.001"),head+3,nonce));var hash=CheckLifecycle.CreatedHash(created,ChecksDeploymentProfile.TestnetContract,secret.Address);for(var i=0;i<5;i++)await Rpc("evm_mine");var redeem=svc.BuildRedeemDraft(Owner,new[]{secret.SignRedeem(hash,Owner)},new[]{hash});var rejected=false;try{await local.EstimateFeeAsync(redeem);}catch(DecimalTransactionException e)when(e.Code==DecimalTransactionError.ContractRejected){rejected=true;}if(!rejected){var receipt=await Send(redeem);Assert.NotEqual(1,CheckLifecycle.RedeemStatus(receipt,ChecksDeploymentProfile.TestnetContract,hash));}}finally{await Rpc("evm_revert",snapshot!);}
    }
    [LocalForkFact] public async Task TokenCenterRoleChecksRejectQaAndPermitActualOwnerOnFork()
    {
        await Profile();var snapshot=(await Rpc("evm_snapshot")).GetString();try{const string abi="[{\"type\":\"function\",\"name\":\"owner\",\"inputs\":[],\"outputs\":[{\"type\":\"address\"}]},{\"type\":\"function\",\"name\":\"transferOwnership\",\"inputs\":[{\"type\":\"address\"}],\"outputs\":[]}]";var contract=local.Web3.Eth.GetContract(abi,Center);var actualOwner=await contract.GetFunction("owner").CallAsync<string>();Assert.Equal(Master,actualOwner.ToLowerInvariant());await Rpc("evm_setAccountBalance",Owner,"0x3635c9adc5dea00000");await Rpc("evm_setAccountBalance",Master,"0x3635c9adc5dea00000");var data=contract.GetFunction("transferOwnership").GetData(Owner);await Send(TransactionDraft.ContractCall(Owner,Center,data),false);Assert.Equal(Master,(await contract.GetFunction("owner").CallAsync<string>()).ToLowerInvariant());await Send(TransactionDraft.ContractCall(Master,Center,data));Assert.Equal(Owner,(await contract.GetFunction("owner").CallAsync<string>()).ToLowerInvariant());}finally{await Rpc("evm_revert",snapshot!);}
    }

    [LocalForkFact] public async Task CreatesReserveTokenWithDecodedReceiptAndActualReserveOnFork()
    {
        await Profile();var snapshot=(await Rpc("evm_snapshot")).GetString();try
        {
            await Rpc("evm_setAccountBalance",Owner,new HexBigInteger(Units.Parse("100000")).HexValue);
            var draft=new TokenCreationDrafts(Center).BuildCreateTokenDraft(Owner,"Fork-only disposable reserve","dcqafork",Units.Parse("1000"),Units.Parse("1"),Units.Parse("1000000"),50,"local-fork-only");
            await local.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=Owner,To=Center,Data=draft.Data,Value=new HexBigInteger(draft.ValueWei)});var receipt=await Send(draft);File.WriteAllText(Path.Combine(ArtifactFolder,"token-create-receipt.json"),Newtonsoft.Json.JsonConvert.SerializeObject(receipt,Newtonsoft.Json.Formatting.Indented));var deployedEvent=Assert.Single(receipt.DecodeAllEvents<CurrentTokenDeployed>(), e=>e.Log.Address.Equals(Center,StringComparison.OrdinalIgnoreCase));var deployed=new CreatedToken(deployedEvent.Event.Address,false);Assert.Equal("dcqafork",deployedEvent.Event.Meta.Symbol);Assert.Equal(Owner,deployedEvent.Event.Meta.Creator.ToLowerInvariant());
            var erc=new Decimal.Web3.Tokens.Erc20Service(local,deployed.Address);var metadata=await erc.GetMetadataAsync();Assert.Equal("dcqafork",metadata.Symbol);Assert.Equal(Units.Parse("1000"),await erc.GetBalanceRawAsync(Owner));
            var reserve=new Decimal.Web3.Tokens.ReservableTokenService(local,deployed.Address);Assert.True(await reserve.GetReserveWeiAsync()>0);Assert.Equal(50,await reserve.GetCrrAsync());
        }finally{await Rpc("evm_revert",snapshot!);}
    }
    [LocalForkFact] public async Task DelHoldResetsAfterTimeTravelOnForkWithoutPublicLockup()
    {
        await Profile();var snapshot=(await Rpc("evm_snapshot")).GetString();try
        {
            await Rpc("evm_setAccountBalance",Owner,new HexBigInteger(Units.Parse("1000")).HexValue);var network=await local.ResolveSystemContractsAsync();const string validator="0x05BaBC196B96C22f763f7e30a84E74665A920f98";var delegation=new DelegationDrafts(network,CombinedNftDelegation.Proxy);var stakes=new StakeReader(local.Web3,CombinedNftDelegation.Proxy);
            var head=await local.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());var hold=head.Timestamp.Value+2*365*86400;var prior=(await stakes.GetStakeAsync(validator,Owner,network.WdelContract)).AmountRaw;
            await Send(delegation.BuildDelegateHoldDelDraft(Owner,validator,"0.001",hold));var held=await stakes.GetHoldStakeAsync(validator,Owner,network.WdelContract,hold);Assert.Equal(Units.Parse("0.001"),held.AmountRaw);Assert.Equal(hold,held.HoldTimestamp);
            await Rpc("evm_increaseTime",2*365*86400+60);await Rpc("evm_mine");await Send(delegation.BuildResetHoldDraft(Owner,validator,Owner,network.WdelContract,hold));Assert.Equal(0,(await stakes.GetHoldStakeAsync(validator,Owner,network.WdelContract,hold)).AmountRaw);Assert.Equal(prior+Units.Parse("0.001"),(await stakes.GetStakeAsync(validator,Owner,network.WdelContract)).AmountRaw);
        }finally{await Rpc("evm_revert",snapshot!);}
    }
    [Event("TokenDeployed")]public sealed class CurrentTokenDeployed:IEventDTO
    {[Parameter("address","tokenAddress",1,false)]public string Address{get;set;}="";[Parameter("tuple","meta",2,false)]public TokenCreationMeta Meta{get;set;}=new();}
}
