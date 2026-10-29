using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Xunit;

namespace DCWallet.Tests;
public sealed class CoreTests
{
    private const string Password = "test-password-for-temporary-fixture";
    [Fact] public async Task PasswordRotationIsAtomicAndRejectsOldPassword()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".vault"); var phrase = DecimalWallet.GenerateMnemonic();
        try
        {
            await Vault.CreateAsync(path, phrase, Password);
            await Assert.ThrowsAnyAsync<CryptographicException>(() => Vault.ChangePasswordAsync(path, "wrong", "new-password-for-fixture"));
            Assert.Equal(phrase, await Vault.OpenAsync(path, Password));
            await Assert.ThrowsAsync<ArgumentException>(() => Vault.ChangePasswordAsync(path, Password, "short"));
            Assert.Equal(phrase, await Vault.OpenAsync(path, Password));
            await Vault.ChangePasswordAsync(path, Password, "new-password-for-fixture");
            Assert.Equal(phrase, await Vault.OpenAsync(path, "new-password-for-fixture"));
            await Assert.ThrowsAnyAsync<CryptographicException>(() => Vault.OpenAsync(path, Password));
        }
        finally { File.Delete(path); }
    }
    [Fact] public async Task VaultRoundTripDoesNotPersistPlaintextAndRejectsWrongPassword()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".vault"); var mnemonic = DecimalWallet.GenerateMnemonic(24);
        try
        {
            await Vault.CreateAsync(path, mnemonic, Password);
            Assert.DoesNotContain(mnemonic, await File.ReadAllTextAsync(path));
            Assert.Equal(mnemonic, await Vault.OpenAsync(path, Password));
            await Assert.ThrowsAnyAsync<CryptographicException>(() => Vault.OpenAsync(path, "wrong"));
            await Assert.ThrowsAsync<IOException>(() => Vault.CreateAsync(path, DecimalWallet.GenerateMnemonic(), Password));
            Assert.Equal(mnemonic, await Vault.OpenAsync(path, Password));
        }
        finally { File.Delete(path); }
    }
    [Fact] public async Task VaultRejectsTampering()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".vault");
        try
        {
            await Vault.CreateAsync(path, DecimalWallet.GenerateMnemonic(), Password);
            var e = JsonSerializer.Deserialize<VaultEnvelope>(await File.ReadAllTextAsync(path))!;
            var bytes = Convert.FromBase64String(e.Ciphertext); bytes[0] ^= 1;
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(e with { Ciphertext = Convert.ToBase64String(bytes) }));
            await Assert.ThrowsAnyAsync<CryptographicException>(() => Vault.OpenAsync(path, Password));
        }
        finally { File.Delete(path); }
    }
    [Fact] public async Task VaultRejectsMaliciousKdfCostBeforeDeriving()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new VaultEnvelope(1, int.MaxValue, "", "", "", "")));
            await Assert.ThrowsAsync<InvalidDataException>(() => Vault.OpenAsync(path, Password));
        }
        finally { File.Delete(path); }
    }
    [Fact] public async Task VaultRejectsWeakPasswordAndInvalidSeed()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Vault.CreateAsync("unused", DecimalWallet.GenerateMnemonic(), "short"));
        await Assert.ThrowsAsync<ArgumentException>(() => Vault.CreateAsync("unused", "invalid seed", Password));
    }
    [Fact] public void CatalogContainsAllDraftFamiliesAndNoSigningOrBroadcastMethods()
    {
        Assert.True(OperationCatalog.All.Count > 70);
        foreach (var family in new[] { "Staking", "Nft", "Tokens", "TokenCreation", "TokenEconomy", "Validators", "Checks", "Bridge", "Multisig", "Multisend" }) Assert.Contains(OperationCatalog.All, x => x.Group == family);
        Assert.DoesNotContain(OperationCatalog.All, x => x.Method.Name.Contains("Broadcast") || x.Method.Name.Contains("Sign"));
        Assert.Equal(OperationCatalog.All.Count, OperationCatalog.All.Select(x => x.Id).Distinct().Count());
        foreach (var op in OperationCatalog.All) using (JsonDocument.Parse(OperationCatalog.Template(op, DecimalNetwork.Mainnet(), "0x" + new string('1', 40)))) { }
    }
    [Fact] public async Task CatalogUsesActiveSenderAndPreservesTinyDelAmounts()
    {
        var from = "0x" + new string('1', 40); var to = "0x" + new string('2', 40);
        var op = OperationCatalog.All.Single(x => x.Method.DeclaringType == typeof(TransactionDraft) && x.Method.Name == "Del");
        var json = JsonSerializer.Serialize(new { from = to, to, amountDel = "0.000000000000000001", memo = "hello" });
        var draft = await OperationCatalog.BuildAsync(op, json, new DecimalClient(), from);
        Assert.Equal(from, draft.From); Assert.Equal(to, draft.To); Assert.Equal(BigInteger.One, draft.ValueWei); Assert.Equal("0x68656c6c6f", draft.Data);
    }
    [Fact] public async Task CatalogRejectsSenderSpoofingInsideRawDraft()
    {
        var op = OperationCatalog.All.Single(x => x.Method.DeclaringType == typeof(TransactionDraft) && x.Method.Name == "ContractCall");
        var json = "{\"from\":\"ignored\",\"contract\":\"broken\",\"data\":\"0x\",\"valueWei\":\"0\"}";
        await Assert.ThrowsAsync<ArgumentException>(() => OperationCatalog.BuildAsync(op, json, new DecimalClient(), "0x" + new string('1', 40)));
    }
    [Fact] public void BigIntegersNeverGoThroughFloatingPointAndBytesAreHex()
    {
        var value = BigInteger.Pow(2, 255) + 123;
        Assert.Equal(value, JsonSerializer.Deserialize<BigInteger>(JsonSerializer.Serialize(value, OperationCatalog.Json), OperationCatalog.Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BigInteger>("9007199254740993", OperationCatalog.Json));
        Assert.Equal(new byte[] { 0, 255 }, JsonSerializer.Deserialize<byte[]>("\"0x00ff\"", OperationCatalog.Json));
    }
}
