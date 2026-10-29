using Xunit;
namespace DCWallet.Tests;
public sealed class AdvancedNetworkFactAttribute : FactAttribute
{
    public AdvancedNetworkFactAttribute() { if (Environment.GetEnvironmentVariable("DC_ADVANCED_TESTNET_WRITE") != "1") Skip = "Explicit isolated advanced testnet write opt-in required"; }
}
public sealed class AdvancedNetworkTests
{
    [AdvancedNetworkFact, Trait("Category", "TestnetWrite")]
    public async Task ExistingStandAdvancedLifecycle()
    {
        using var context = await AdvancedTestContext.CreateAsync();
        try
        {
            foreach (var scenario in (Environment.GetEnvironmentVariable("DC_ADVANCED_SCENARIOS") ?? "safe,checks").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (scenario == "nft721-complete")
                {
                    var previous = Environment.GetEnvironmentVariable("DC_STAKING_RECOVERY_DIRECTORY") ?? throw new InvalidOperationException("Public staking recovery directory required.");
                    await Nft721NetworkScenario.CompleteAsync(context, System.Text.Json.JsonSerializer.Deserialize<Nft721NetworkScenario.Pending>(await File.ReadAllTextAsync(Path.Combine(previous, "nft721-staking-pending.json")))!);
                    continue;
                }
                if (scenario is "swap-convert" or "swap-convert-permit") { await SwapConvertNetworkScenario.RunAsync(context, scenario == "swap-convert-permit"); continue; }
                if (scenario == "nft721-owned-resume")
                {
                    await Nft721NetworkScenario.ResumeOwnedMintAsync(context,
                        Environment.GetEnvironmentVariable("DC_QA_NFT721_COLLECTION") ?? throw new InvalidOperationException("QA NFT721 collection required."), 1,
                        Environment.GetEnvironmentVariable("DC_QA_NFT721_CREATION_HASH") ?? throw new InvalidOperationException("QA creation hash required."));
                    continue;
                }
                if (scenario == "erc20-staking-begin") { await Erc20StakingNetworkScenario.BeginAsync(context); continue; }
                if (scenario == "nft721-resume")
                {
                    await Nft721NetworkScenario.ResumeCreatedCollectionAsync(context,
                        Environment.GetEnvironmentVariable("DC_QA_NFT721_COLLECTION") ?? throw new InvalidOperationException("QA NFT721 collection required."),
                        Environment.GetEnvironmentVariable("DC_QA_NFT721_CREATION_HASH") ?? throw new InvalidOperationException("QA creation hash required."));
                    continue;
                }
                if (scenario == "staking-begin")
                {
                    var beforeCases = context.CaseCount;
                    try { await Erc20StakingNetworkScenario.BeginAsync(context); }
                    catch (Exception e) when (context.CaseCount == beforeCases)
                    { context.Note("ERC20 staking preflight blocked without sending", new { errorType = e.GetType().Name, writes = 0 }); }
                    await Nft721NetworkScenario.BeginAsync(context);
                    continue;
                }
                if (scenario == "staking-complete")
                {
                    var previous = Environment.GetEnvironmentVariable("DC_STAKING_RECOVERY_DIRECTORY") ?? throw new InvalidOperationException("Public staking recovery directory required.");
                    var tokenFile = Path.Combine(previous, "erc20-staking-pending.json");
                    if (File.Exists(tokenFile)) await Erc20StakingNetworkScenario.CompleteAsync(context, System.Text.Json.JsonSerializer.Deserialize<Erc20StakingNetworkScenario.Pending>(await File.ReadAllTextAsync(tokenFile))!);
                    var nftFile = Path.Combine(previous, "nft721-staking-pending.json");
                    if (File.Exists(nftFile)) await Nft721NetworkScenario.CompleteAsync(context, System.Text.Json.JsonSerializer.Deserialize<Nft721NetworkScenario.Pending>(await File.ReadAllTextAsync(nftFile))!);
                    continue;
                }
                if (scenario == "staking-resume-after-complete")
                {
                    var previous = Environment.GetEnvironmentVariable("DC_STAKING_RECOVERY_DIRECTORY") ?? throw new InvalidOperationException("Public staking recovery directory required.");
                    await Erc20StakingNetworkScenario.SellRestoredDeltaAsync(context, System.Text.Json.JsonSerializer.Deserialize<Erc20StakingNetworkScenario.Pending>(await File.ReadAllTextAsync(Path.Combine(previous, "erc20-staking-pending.json")))!);
                    await Nft721NetworkScenario.CompleteAsync(context, System.Text.Json.JsonSerializer.Deserialize<Nft721NetworkScenario.Pending>(await File.ReadAllTextAsync(Path.Combine(previous, "nft721-staking-pending.json")))!);
                    continue;
                }
                if (scenario is "checks-token" or "checks-token-permit")
                {
                    await ChecksTokenNetworkScenario.RunAsync(context, scenario == "checks-token-permit");
                    continue;
                }
                var name = scenario switch { "safe" => "SafeNetworkScenario", "safe-advanced" => "SafeAdvancedNetworkScenario", "quorum" => "WeightedQuorumNetworkScenario", "checks" => "ChecksNetworkScenario", "nft" => "NftNetworkScenario", _ => throw new ArgumentException("Unknown advanced testnet scenario.") };
                var type = typeof(AdvancedNetworkTests).Assembly.GetType("DCWallet.Tests." + name) ?? throw new InvalidOperationException("Scenario implementation unavailable.");
                await (Task)type.GetMethod("RunAsync")!.Invoke(null, [context])!;
            }
            context.Save("passed");
        }
        catch (Exception error) { context.Note("Campaign stopped", new { errorType = error.GetBaseException().GetType().Name }); context.Save("stopped"); throw new InvalidOperationException("Advanced isolated testnet verification stopped; inspect the public campaign report. Secret-bearing RPC errors are withheld."); }
    }
}
