using Avalonia;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;

namespace DCWallet;
public static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--runtime-info")
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
                osArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
                runtimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            }));
            return 0;
        }
        if (args.Length == 2 && args[0] is "--read-only-portfolio" or "--read-only-history")
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8)); var client = new DecimalClient();
                if (args[0] == "--read-only-portfolio")
                {
                    using var reader = new WalletDataReader(client); var tokens = reader.TokensAsync(args[1], timeout.Token); var nfts = reader.NftsAsync(args[1], timeout.Token); var stakes = reader.StakesAsync(args[1], timeout.Token);
                    Task.WhenAll(tokens, nfts, stakes).GetAwaiter().GetResult(); Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Tokens = tokens.Result, Nfts = nfts.Result, Stakes = stakes.Result, broadcasts = 0 }, OperationCatalog.Json));
                }
                else Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new HistoryReader(client).ReadAllAsync(args[1], ct: timeout.Token).GetAwaiter().GetResult(), OperationCatalog.Json));
                return 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e.ToString()); return 1; }
        }
        if (args.Length == 2 && args[0] == "--read-only-smoke")
        {
            try
            {
                ReadOnlySmoke(args[1]).GetAwaiter().GetResult(); return 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
        }
        if (args.Contains("--self-test"))
        {
            var mnemonic = DecimalWallet.GenerateMnemonic();
            var from = DecimalWallet.DeriveAddress(mnemonic);
            var draft = TransactionDraft.Del(from, from, "0.000000000000000001");
            if (draft.ValueWei != 1 || OperationCatalog.All.Count < 50) return 1;
            Console.WriteLine($"DC WALLET self-test OK; {OperationCatalog.All.Count} SDK draft methods; no network requests.");
            return 0;
        }
        BuildApp().StartWithClassicDesktopLifetime(args); return 0;
    }
    public static AppBuilder BuildApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
    private static async Task ReadOnlySmoke(string owner)
    {
        var client = new DecimalClient(DecimalNetwork.Mainnet());
        var balance = await client.GetBalanceWeiAsync(owner);
        var history = await new HistoryReader(client).ReadAsync(owner);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            network = client.Network.Name, chainId = client.Network.ChainId, owner,
            balanceDel = Units.Format(balance), verifiedRows = history.Rows.Count, failedRows = history.Failed,
            sample = history.Rows.Take(2), broadcasts = 0, seedUsed = false
        }, OperationCatalog.Json));
    }
}
