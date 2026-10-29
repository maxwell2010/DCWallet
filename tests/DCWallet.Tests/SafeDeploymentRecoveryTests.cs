using System.Text.Json;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public sealed class DeploymentRecoveryFactAttribute:FactAttribute
{
    public DeploymentRecoveryFactAttribute(){if(Environment.GetEnvironmentVariable("DC_SAFE_DEPLOYMENT_RECOVERY")!="1")Skip="Explicit read-only deployment reconciliation required.";}
}
public class SafeDeploymentRecoveryTests
{
    [DeploymentRecoveryFact]
    public async Task ExistingDeploymentIsReconciledWithoutSigningOrSending()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"SafeQaFixtures","SafeQa.compiled.json")));
        var client=new DecimalClient(DecimalNetwork.Testnet() with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")});
        await AdvancedTestContext.RecoverExistingDeploymentAsync(client,
            Environment.GetEnvironmentVariable("DC_SAFE_QA_OWNER_DEPLOYMENT_HASH")!,
            Environment.GetEnvironmentVariable("DC_SAFE_QA_OWNER_CONTRACT")!,
            fixture.RootElement.GetProperty("contracts").GetProperty("QaSignatureOwner").GetProperty("creationBytecode").GetString()!,
            SafeAdvancedNetworkScenario.OwnerRuntimeSha256,
            Environment.GetEnvironmentVariable("DC_SAFE_QA_PREVIOUS_DIRECTORY")!);
    }
}
