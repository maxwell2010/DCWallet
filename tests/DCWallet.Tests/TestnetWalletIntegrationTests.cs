using System.Numerics;
using System.Text;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Nft;
using Decimal.Web3.Staking;
using Decimal.Web3.TokenEconomy;
using Decimal.Web3.Tokens;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.Tests;
public sealed class TestnetWalletFactAttribute:FactAttribute
{
    public TestnetWalletFactAttribute() { if(Environment.GetEnvironmentVariable("DC_TESTNET_WALLET_WRITE")!="1") Skip="Existing isolated testnet stand: explicit write opt-in required"; }
}
public class TestnetWalletIntegrationTests
{
    const string Owner="0xb92bd490c8e9e5d9c46d71666418d4e7ae4534e9", QaToken="0xc310f25e3429efc5ede11a908682f0a31cfc92ce", Nft="0x918e742d8dd0de7e7930fe0489cdbce1701fb29d", Reserve="0x37748f3e857a71e4334125a935052ce1e603b7b5", Validator="0x05BaBC196B96C22f763f7e30a84E74665A920f98";
    [TestnetWalletFact,Trait("Category","TestnetWrite")]
    public async Task WalletPipelineSequentialBoundedNetworkTransactions()
    {
        var root=Environment.GetEnvironmentVariable("DC_TESTNET_WALLET_ARTIFACTS")??throw new InvalidOperationException("Artifact path required"); Directory.CreateDirectory(root);
        var resume=Environment.GetEnvironmentVariable("DC_TESTNET_WALLET_RESUME_REPORT");
        var run="run-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ"); var journal=resume is null?Path.Combine(root,run):Path.GetDirectoryName(Path.GetFullPath(resume))!; Directory.CreateDirectory(journal);
        var report=Path.Combine(journal,"report.json"); var rows=new List<Dictionary<string,object?>>(); BigInteger reserved=0,paid=0; var totalValue=BigInteger.Zero;
        if(resume is not null) { using var previous=JsonDocument.Parse(await File.ReadAllTextAsync(resume)); Assert.Equal(202020,previous.RootElement.GetProperty("chainId").GetInt32());Assert.Equal(Owner,previous.RootElement.GetProperty("owner").GetString()); foreach(var item in previous.RootElement.GetProperty("cases").EnumerateArray()){Assert.Equal("confirmed",item.GetProperty("state").GetString()); rows.Add(JsonSerializer.Deserialize<Dictionary<string,object?>>(item.GetRawText())!);} reserved=Units.Parse(previous.RootElement.GetProperty("reservedDel").GetString()!);paid=Units.Parse(previous.RootElement.GetProperty("paidDel").GetString()!);totalValue=Units.Parse(previous.RootElement.GetProperty("totalValueDel").GetString()!); }
        void Save(string status)=>File.WriteAllText(report,JsonSerializer.Serialize(new{chainId=202020,owner=Owner,status,feeLimitPerCaseDel="0.5",totalBudgetDel="5",reservedDel=Units.Format(reserved),paidDel=Units.Format(paid),totalValueDel=Units.Format(totalValue),cases=rows},new JsonSerializerOptions{WriteIndented=true}));
        var path=Environment.GetEnvironmentVariable("DC_EXISTING_STAND_ENV")??throw new InvalidOperationException("Stand path missing"); string? phrase=null,expected=null;
        foreach(var line in File.ReadLines(path)) { var i=line.IndexOf('='); if(i<1)continue; var key=line[..i].Trim(); if(key is not("DECIMAL_TESTNET_TEST_MNEMONIC" or "DECIMAL_TESTNET_TEST_EXPECTED_ADDRESS"))continue;var value=line[(i+1)..].Trim();if(value.Length>=2&&(value[0]=='"'&&value[^1]=='"'||value[0]=='\''&&value[^1]=='\''))value=value[1..^1];if(key=="DECIMAL_TESTNET_TEST_MNEMONIC")phrase=value;else expected=value; }
        phrase=string.Join(' ',(phrase??"").Normalize(NormalizationForm.FormKD).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)); Assert.True(DecimalWallet.IsValidMnemonic(phrase),"Invalid isolated testnet seed"); Assert.Equal(Owner,expected?.ToLowerInvariant());
        using var signer=DecimalWallet.CreateTransactionSigner(phrase,202020); phrase=null; Assert.Equal(Owner,signer.Address.ToLowerInvariant());
        var client=new DecimalClient(DecimalNetwork.Testnet() with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")}); Assert.Equal(202020,(await client.Web3.Eth.ChainId.SendRequestAsync()).Value);
        var network=await client.ResolveSystemContractsAsync(); var session=new TransferSession(client,journal); Save("running");
        async Task<TransactionResult> Send(string label,TransactionDraft draft)
        {
            Assert.Equal(Owner,draft.From.ToLowerInvariant()); Assert.InRange(draft.ValueWei,BigInteger.Zero,Units.Parse("0.01"));
            var row=new Dictionary<string,object?>{["case"]=label,["to"]=draft.To,["valueWei"]=draft.ValueWei.ToString(),["state"]="estimating"};rows.Add(row);Save("running");
            var before=await client.GetBalanceWeiAsync(Owner); row["beforeDel"]=Units.Format(before);
            var preliminary=await client.EstimateFeeAsync(draft); Assert.True(preliminary.MaximumFeeWei<=Units.Parse("0.5"),"Per-case fee budget exceeded before signing");Assert.True(reserved+totalValue+preliminary.MaximumFeeWei+draft.ValueWei<=Units.Parse("5"),"Total budget exceeded before signing");
            var prepared=await session.PrepareAsync(draft,signer); Assert.True(prepared.Quote.MaximumFeeWei<=Units.Parse("0.5")); Assert.True(reserved+totalValue+prepared.Quote.MaximumFeeWei+draft.ValueWei<=Units.Parse("5"));
            reserved+=prepared.Quote.MaximumFeeWei;totalValue+=draft.ValueWei;row["hash"]=prepared.Signed.Hash;row["estimatedFeeDel"]=prepared.Quote.EstimatedFeeDel;row["maximumFeeDel"]=prepared.Quote.MaximumFeeDel;row["state"]="signed-local-hash-recorded";Save("running");
            try { var pending=await session.SendAsync(prepared);Assert.Equal(prepared.Signed.Hash,pending.Hash);row["state"]="submitted";Save("running");
                var receipt=await client.WaitForReceiptAsync(pending.Hash,TimeSpan.FromSeconds(75),TimeSpan.FromSeconds(3));row["state"]=receipt.Success==true?"confirmed":"reverted";row["block"]=receipt.BlockNumber?.ToString();row["paidFeeDel"]=receipt.PaidFeeDel;paid+=receipt.PaidFeeWei??0;row["afterDel"]=Units.Format(await client.GetBalanceWeiAsync(Owner));Save("running");Assert.True(receipt.Success==true,"Testnet transaction reverted; stop run");return receipt;
            } catch { row["state"]="unknown-or-failed-stop-no-resend";Save("stopped");throw; }
        }
        try {
            if(resume is not null)
            {
                var cleanupToken=new Erc20Service(client,Reserve);var allowance=await cleanupToken.GetAllowanceRawAsync(Owner,Reserve);
                if(allowance>0) { await Send("revoke remaining test-only reserve allowance",cleanupToken.BuildApproveDraft(Owner,Reserve,0));Assert.Equal(BigInteger.Zero,await cleanupToken.GetAllowanceRawAsync(Owner,Reserve)); }
                var withdrawal=rows.Single(r=>r["case"]?.ToString()=="withdraw only new 0.001 DEL stake");
                var requests=await WithdrawalReceiptReader.ReadAsync(client.Web3,network.DelegationContract,withdrawal["hash"]!.ToString()!);var request=Assert.Single(requests);Assert.Equal(Owner,request.Stake.Delegator.ToLowerInvariant());Assert.Equal(Units.Parse("0.001"),request.Stake.AmountRaw);
                withdrawal["withdrawalIndex"]=request.StakeIndex.ToString();withdrawal["unfreezeTimestamp"]=request.UnfreezeTimestamp.ToString();Save("waiting-maturity");
                var wait=request.UnfreezeTimestamp-DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if(wait>600) { Save("passed-with-withdrawal-awaiting-maturity-over-10min"); return; }
                while(DateTimeOffset.UtcNow.ToUnixTimeSeconds()<request.UnfreezeTimestamp) await Task.Delay(TimeSpan.FromSeconds(15));
                var reader=new FrozenStakeReader(client.Web3,network.DelegationContract);var before=await reader.GetFrozenStakeAsync(request.StakeIndex);Assert.True(before.IsMatured(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));Assert.Equal(Owner,before.Stake.Delegator.ToLowerInvariant());
                var nativeBefore=await client.GetBalanceWeiAsync(Owner);var completed=await Send("complete newly matured test-only withdrawal",new DelegationDrafts(network,network.DelegationContract).BuildCompleteDraft(Owner,new[]{request.StakeIndex}));
                Assert.Equal(0,(await reader.GetFrozenStakeAsync(request.StakeIndex)).FreezeStatus);var nativeAfter=await client.GetBalanceWeiAsync(Owner);Assert.True(nativeAfter>=nativeBefore+request.Stake.AmountRaw-completed.PaidFeeWei!.Value,"Completion must return the newly withdrawn stake less fee");
                Save("passed");return;
            }
            await Send("DEL memo self",TransactionDraft.Del(Owner,Owner,"0.000000000000000001","DC WALLET pipeline QA"));
            var erc20=new Erc20Service(client,QaToken);var tokenBefore=await erc20.GetBalanceRawAsync(Owner);Assert.True(tokenBefore>0);
            await Send("QA ERC20 self one raw",erc20.BuildTransferDraft(Owner,Owner,BigInteger.One));Assert.Equal(tokenBefore,await erc20.GetBalanceRawAsync(Owner));
            var nft=new Erc1155Service(client);var nftBefore=await nft.BalanceOfAsync(Nft,Owner,1);Assert.True(nftBefore>0);
            await Send("QA ERC1155 self id1 amount1",nft.SafeTransferFrom(Owner,Nft,Owner,1,1));Assert.Equal(nftBefore,await nft.BalanceOfAsync(Nft,Owner,1));
            var mintService=new ReservelessTokenService(client,QaToken);
            await Send("QA mint one raw",await mintService.BuildMintCheckedAsync(Owner,Owner,1));Assert.Equal(tokenBefore+1,await erc20.GetBalanceRawAsync(Owner));
            await Send("QA burn newly minted one raw",await mintService.BuildBurnCheckedAsync(Owner,1));Assert.Equal(tokenBefore,await erc20.GetBalanceRawAsync(Owner));
            var reserveToken=new Erc20Service(client,Reserve);var reserveService=new ReservableTokenService(client,Reserve);var oldReserveBalance=await reserveToken.GetBalanceRawAsync(Owner);var input=Units.Parse("0.001");var buyOutput=await reserveService.CalculateBuyOutputRawAsync(input);Assert.True(buyOutput>0);var economy=new TokenEconomyDrafts(network.TokenCenterContract);
            await Send("reserve buy 0.001 DEL",economy.BuildBuyTokenForExactDelDraft(Owner,Reserve,input,buyOutput*98/100,Owner));
            var bought=await reserveToken.GetBalanceRawAsync(Owner)-oldReserveBalance;Assert.True(bought>0);
            var sellOutput=await reserveService.CalculateSellOutputWeiAsync(bought);Assert.True(sellOutput>0);
            await Send("reserve approve exact new amount",reserveToken.BuildApproveDraft(Owner,Reserve,bought));
            await Send("reserve sell only new amount",economy.BuildSellExactTokensForDelDraft(Owner,Reserve,bought,sellOutput*98/100,Owner));Assert.Equal(oldReserveBalance,await reserveToken.GetBalanceRawAsync(Owner));
            var stakeReader=new StakeReader(client.Web3,network.DelegationContract);var beforeStake=await stakeReader.GetStakeAsync(Validator,Owner,network.WdelContract);var delegation=new DelegationDrafts(network,network.DelegationContract);
            await Send("delegate 0.001 DEL",delegation.BuildDelegateDelDraft(Owner,Validator,"0.001"));var afterStake=await stakeReader.GetStakeAsync(Validator,Owner,network.WdelContract);Assert.Equal(beforeStake.AmountRaw+Units.Parse("0.001"),afterStake.AmountRaw);
            await Send("withdraw only new 0.001 DEL stake",delegation.BuildWithdrawDraft(Owner,Validator,network.WdelContract,Units.Parse("0.001")));Assert.Equal(beforeStake.AmountRaw,(await stakeReader.GetStakeAsync(Validator,Owner,network.WdelContract)).AmountRaw);
            Save("passed");
        } catch { Save("stopped");throw; }
    }
}
