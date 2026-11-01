using System.Security.Cryptography;
using DCWallet.Core;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.Tests;
public sealed class PasswordlessVaultTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ExplicitDisableAndReenablePreserveCredential(bool privateKey)
    {
        var path=Path.Combine(Path.GetTempPath(),"dc-passwordless-"+Guid.NewGuid()+".vault");
        const string password="synthetic-test-password";
        try{
            if(privateKey)await Vault.CreatePrivateKeyAsync(path,new string('1',64),password);else await Vault.CreateAsync(path,DecimalWallet.GenerateMnemonic(),password);
            var original=await Vault.OpenCredentialAsync(path,password);var bytes=await File.ReadAllBytesAsync(path);
            await Assert.ThrowsAnyAsync<CryptographicException>(()=>Vault.SetPasswordProtectionAsync(path,"wrong", "",true));Assert.Equal(bytes,await File.ReadAllBytesAsync(path));
            await Vault.SetPasswordProtectionAsync(path,password,"",true);Assert.True(Vault.IsPasswordless(path));Assert.Equal(original,await Vault.OpenCredentialAsync(path,""));
            using(var signer=privateKey?WalletSigningSession.FromPrivateKey(original.Secret,202020):WalletSigningSession.FromMnemonic(original.Secret,202020)){Assert.NotEmpty(signer.SignLegacyTransaction(0,"0x1111111111111111111111111111111111111111",0,1,21000,"0x"));Assert.NotEmpty(signer.SignDigest(new byte[32]));}
            var disabled=await File.ReadAllBytesAsync(path);await Assert.ThrowsAsync<ArgumentException>(()=>Vault.SetPasswordProtectionAsync(path,"","short",false));Assert.Equal(disabled,await File.ReadAllBytesAsync(path));
            await Vault.SetPasswordProtectionAsync(path,"","new-synthetic-password",false);Assert.False(Vault.IsPasswordless(path));
            await Assert.ThrowsAnyAsync<CryptographicException>(()=>Vault.OpenCredentialAsync(path,""));Assert.Equal(original,await Vault.OpenCredentialAsync(path,"new-synthetic-password"));
        }finally{File.Delete(path);}
    }
    [Fact]public async Task EditingFlagDoesNotBypassProtectedVault()
    {
        var path=Path.Combine(Path.GetTempPath(),"dc-mode-tamper-"+Guid.NewGuid()+".vault");
        try{await Vault.CreateAsync(path,DecimalWallet.GenerateMnemonic(),"synthetic-test-password");var text=await File.ReadAllTextAsync(path);await File.WriteAllTextAsync(path,text.Replace("\"Passwordless\":false","\"Passwordless\":true"));await Assert.ThrowsAnyAsync<CryptographicException>(()=>Vault.OpenCredentialAsync(path,""));}
        finally{File.Delete(path);}
    }
}
