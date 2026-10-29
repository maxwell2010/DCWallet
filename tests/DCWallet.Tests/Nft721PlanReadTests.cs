using System.Text.Json;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public sealed class Nft721PlanReadFactAttribute:FactAttribute{public Nft721PlanReadFactAttribute(){if(Environment.GetEnvironmentVariable("DC_NFT721_PLAN_READ")!="1")Skip="Explicit read-only NFT721 plan opt-in required";}}
public class Nft721PlanReadTests
{
 [Nft721PlanReadFact]public async Task CreationEstimateRequiresNoSignerOrFunding(){var client=new DecimalClient(DecimalNetwork.Testnet() with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")});var plan=await Nft721FixturePlan.StageCreationAsync(client,AdvancedTestContext.ExpectedOwner).WaitAsync(TimeSpan.FromSeconds(55));var root=Environment.GetEnvironmentVariable("DC_NFT721_PLAN_ARTIFACTS")??Path.Combine(Path.GetTempPath(),"dc-nft721-plan");Directory.CreateDirectory(root);await File.WriteAllTextAsync(Path.Combine(root,"nft721-create-plan.json"),JsonSerializer.Serialize(new{chainId=202020,plan.AbiVariant,plan.Draft.From,plan.Draft.To,plan.Draft.Data,valueWei=plan.Draft.ValueWei.ToString(),maximumFeeDel=plan.Quote.MaximumFeeDel,broadcast=false}));Assert.True(plan.Quote.CanAfford);}
}
