using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Nethereum.RPC.Eth.DTOs;

namespace DCWallet.Tests;
public sealed partial class AdvancedTestContext : IDisposable
{
    public const string ExpectedOwner = "0xb92bd490c8e9e5d9c46d71666418d4e7ae4534e9";
    public DecimalClient Client { get; }
    public DecimalNetwork Network { get; private set; }
    public DecimalTransactionSigner Signer { get; }
    public WalletSigningSession WalletSigner { get; }
    public string Owner => ExpectedOwner;
    public string DirectoryPath { get; }
    private string? phrase;
    private readonly FileStream exclusive;
    private readonly TransferSession session;
    private readonly List<object> cases = [], notes = [];
    private BigInteger reserved, paid, values;
    private bool stopped;
    public int CaseCount => cases.Count;
    private AdvancedTestContext(string mnemonic, string root)
    {
        Directory.CreateDirectory(root);
        exclusive = new FileStream(Path.Combine(root, "nonce.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        DirectoryPath = Path.Combine(root, "run-" + DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(DirectoryPath); phrase = mnemonic;
        Signer = DecimalWallet.CreateTransactionSigner(mnemonic, 202020);
        WalletSigner = WalletSigningSession.FromMnemonic(mnemonic, 202020);
        if (!Signer.Address.Equals(ExpectedOwner, StringComparison.OrdinalIgnoreCase)) { Dispose(); throw new InvalidOperationException("Designated isolated testnet owner does not match."); }
        Network = DecimalNetwork.Testnet() with { RpcUrl = new Uri("https://202020.rpc.thirdweb.com") }; Client = new(Network);
        session = new(Client, DirectoryPath); Save("preflight");
    }
    public static async Task<AdvancedTestContext> CreateAsync()
    {
        if (Environment.GetEnvironmentVariable("DC_ADVANCED_TESTNET_WRITE") != "1") throw new InvalidOperationException("Explicit advanced testnet opt-in required.");
        var path = Environment.GetEnvironmentVariable("DC_EXISTING_STAND_ENV") ?? throw new InvalidOperationException("Designated isolated stand configuration required.");
        string? mnemonic = null, expected = null;
        foreach (var line in File.ReadLines(path))
        {
            var index = line.IndexOf('='); if (index <= 0) continue; var name = line[..index].Trim();
            if (name is not ("DECIMAL_TESTNET_TEST_MNEMONIC" or "DECIMAL_TESTNET_TEST_EXPECTED_ADDRESS")) continue;
            var value = line[(index + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\'')) value = value[1..^1];
            if (name == "DECIMAL_TESTNET_TEST_MNEMONIC") mnemonic = value; else expected = value;
        }
        mnemonic = string.Join(' ', (mnemonic ?? "").Normalize(NormalizationForm.FormKD).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (!DecimalWallet.IsValidMnemonic(mnemonic) || !ExpectedOwner.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Isolated stand identity validation failed.");
        var context = new AdvancedTestContext(mnemonic, Environment.GetEnvironmentVariable("DC_ADVANCED_TESTNET_ARTIFACTS") ?? throw new InvalidOperationException("Public artifact directory required."));
        try
        {
            if ((await context.Client.Web3.Eth.ChainId.SendRequestAsync()).Value != 202020) throw new InvalidOperationException("Wrong RPC chain.");
            context.Network = await context.Client.ResolveSystemContractsAsync(); return context;
        }
        catch { context.Dispose(); throw; }
    }
    public SafeSignature SignSafe(SafeTransactionDraft transaction) => SafeAdvancedTransactions.Sign(transaction, 202020, new Nethereum.HdWallet.Wallet(phrase ?? throw new ObjectDisposedException(nameof(AdvancedTestContext)), "", Nethereum.HdWallet.Wallet.DEFAULT_PATH).GetEthereumKey(0));
    public SafeSignature SignSafe(SafeTransactionDraft transaction, int index) => SafeAdvancedTransactions.Sign(transaction, 202020, new Nethereum.HdWallet.Wallet(phrase ?? throw new ObjectDisposedException(nameof(AdvancedTestContext)), "", Nethereum.HdWallet.Wallet.DEFAULT_PATH).GetEthereumKey(index));
    public string OwnerAt(int index) => DecimalWallet.DeriveAddress(phrase ?? throw new ObjectDisposedException(nameof(AdvancedTestContext)), index);
    public byte[] Protect(byte[] plaintext)
    {
        if (plaintext.Length is < 1 or > 65536) throw new ArgumentException("Invalid protected test payload size.");
        var salt = RandomNumberGenerator.GetBytes(32); var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var cipher = new byte[plaintext.Length];
        var key = Key(salt); try { using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plaintext, cipher, tag, "DCQA202020-v1"u8); }
        finally { CryptographicOperations.ZeroMemory(key); }
        return "DCQA1"u8.ToArray().Concat(salt).Concat(nonce).Concat(tag).Concat(cipher).ToArray();
    }
    public byte[] Unprotect(byte[] payload)
    {
        if (payload.Length is < 66 or > 65601 || !payload.AsSpan(0, 5).SequenceEqual("DCQA1"u8)) throw new InvalidDataException("Invalid protected test payload.");
        var key = Key(payload.AsSpan(5, 32).ToArray()); var plaintext = new byte[payload.Length - 65];
        try { using var aes = new AesGcm(key, 16); aes.Decrypt(payload.AsSpan(37, 12), payload.AsSpan(65), payload.AsSpan(49, 16), plaintext, "DCQA202020-v1"u8); return plaintext; }
        catch { CryptographicOperations.ZeroMemory(plaintext); throw; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    private byte[] Key(byte[] salt) => Rfc2898DeriveBytes.Pbkdf2((phrase ?? throw new ObjectDisposedException(nameof(AdvancedTestContext))) + "\nDC-WALLET-ISOLATED-TESTNET-202020", salt, 600000, HashAlgorithmName.SHA256, 32);
    public void Note(string label, object publicMetadata) { notes.Add(new { label, details = publicMetadata }); Save("running"); }
    public void Save(string status) => File.WriteAllText(Path.Combine(DirectoryPath, "report.json"), JsonSerializer.Serialize(new { chainId = 202020, owner = Owner, status, reservedDel = Units.Format(reserved), paidDel = Units.Format(paid), valueDel = Units.Format(values), feeLimitDel = "0.5", totalBudgetDel = "5", cases, notes }, new JsonSerializerOptions { WriteIndented = true }));
    public async Task<TransactionReceipt> SendAsync(string label, TransactionDraft draft, bool reviewedNftCreation = false, bool reviewedNft721Delegation = false)
    {
        if (stopped) throw new InvalidOperationException("Campaign stopped; no broadcast retry allowed.");
        var perCaseLimit = Units.Parse("0.5");
        if (reviewedNft721Delegation)
        {
            var selector = "0x" + Nethereum.Util.Sha3Keccack.Current.CalculateHash("delegateNFT(address,address,uint256)")[..8];
            if (reviewedNftCreation || draft.ValueWei != 0 || !draft.To.Equals(CombinedNftDelegation.Proxy, StringComparison.OrdinalIgnoreCase) || !draft.Data.StartsWith(selector, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Extended fee cap applies only to reviewed QA NFT721 delegation.");
            await CombinedNftDelegation.VerifyAsync(Client);
            perCaseLimit = Units.Parse("0.7");
            Note("Reviewed NFT721 delegation fee exception", new { maximumFeeDel = "0.7", aggregateBudgetDel = "5", contractAddress = draft.To });
        }
        if (reviewedNftCreation)
        {
            var selector = "0x" + Nethereum.Util.Sha3Keccack.Current.CalculateHash("createDRC721((address,string,string,string,bool,bool))")[..8];
            if (draft.ValueWei != 0 || !draft.To.Equals(Nft721FixturePlan.Center, StringComparison.OrdinalIgnoreCase) || !draft.Data.StartsWith(selector, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Extended fee cap applies only to reviewed zero-value QA NFT721 creation.");
            perCaseLimit = Units.Parse("0.6");
            Note("Reviewed NFT721 creation fee exception", new { maximumFeeDel = "0.6", aggregateBudgetDel = "5", contractAddress = draft.To });
        }
        if (!draft.From.Equals(Owner, StringComparison.OrdinalIgnoreCase) || draft.ValueWei < 0 || draft.ValueWei > Units.Parse("0.01")) throw new InvalidOperationException("Isolated testnet per-case value/owner restriction.");
        if ((await Client.Web3.Eth.ChainId.SendRequestAsync()).Value != 202020) throw new InvalidOperationException("Wrong RPC chain.");
        var quote = await Client.EstimateFeeAsync(draft); CheckBudget(quote.MaximumFeeWei, draft.ValueWei, perCaseLimit);
        var prepared = await session.PrepareAsync(draft, WalletSigner); CheckBudget(prepared.Quote.MaximumFeeWei, draft.ValueWei, perCaseLimit);
        var nonce = (await Client.Web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(Owner, BlockParameter.CreatePending())).Value;
        if (nonce != prepared.Signed.Nonce) { session.Clear(); throw new InvalidOperationException("Pending testnet nonce changed before send."); }
        reserved += prepared.Quote.MaximumFeeWei; values += draft.ValueWei;
        var row = new Dictionary<string, object?> { ["label"] = label, ["hash"] = prepared.Signed.Hash, ["nonce"] = nonce.ToString(), ["to"] = draft.To, ["valueWei"] = draft.ValueWei.ToString(), ["maximumFeeDel"] = prepared.Quote.MaximumFeeDel, ["state"] = "signed-local-hash-recorded" };
        cases.Add(row); Save("running");
        try
        {
            await session.SendAsync(prepared); row["state"] = "submitted"; Save("running");
            var result = await ReadOnlyRetryAsync(()=>Client.WaitForReceiptAsync(prepared.Signed.Hash, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(4)));
            if (result.Success is null) { stopped = true; row["state"] = "unknown-stop-no-resend"; Save("stopped"); throw new InvalidOperationException("Confirmation unavailable; no resend."); }
            row["state"] = result.Success == true ? "confirmed" : "reverted"; row["block"] = result.BlockNumber?.ToString(); row["paidFeeDel"] = result.PaidFeeDel; paid += result.PaidFeeWei ?? 0; Save("running");
            if (result.Success != true) { stopped = true; throw new InvalidOperationException("Testnet execution reverted; campaign stopped."); }
            return await Client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(prepared.Signed.Hash) ?? throw new InvalidOperationException("Confirmed receipt temporarily unavailable.");
        }
        catch { stopped = true; Save("stopped"); throw new InvalidOperationException("Advanced testnet case stopped. Check public local hash and encrypted recovery files; no automatic resend."); }
    }
    private void CheckBudget(BigInteger fee, BigInteger value, BigInteger? perCaseLimit = null)
    {
        if (fee > (perCaseLimit ?? Units.Parse("0.5")) || reserved + values + fee + value > Units.Parse("5")) throw new InvalidOperationException("Isolated testnet fee budget exceeded before broadcast.");
    }
    public void Dispose() { phrase = null; Signer?.Dispose(); WalletSigner?.Dispose(); exclusive?.Dispose(); }
}
