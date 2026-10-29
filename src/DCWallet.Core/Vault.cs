using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Decimal.Web3.Wallet;

namespace DCWallet.Core;

public sealed record VaultEnvelope(int Version, int Iterations, string Salt, string Nonce, string Ciphertext, string Tag);
public sealed record VaultCredential(string Kind, string Secret)
{
    public override string ToString()=>"VaultCredential "+Kind;
}
public static class Vault
{
    public static async Task ChangePasswordAsync(string path, string currentPassword, string newPassword)
    {
        var credential = await OpenCredentialAsync(path, currentPassword);
        var temporary = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".vault";
        try
        {
            if(credential.Kind=="Mnemonic")await CreateAsync(temporary, credential.Secret, newPassword);else await CreatePrivateKeyAsync(temporary,credential.Secret,newPassword);
            if (await OpenCredentialAsync(temporary, newPassword) != credential) throw new InvalidDataException("Проверка нового файла кошелька не пройдена.");
            File.Replace(temporary, Path.GetFullPath(path), null);
        }
        finally { credential = new("Mnemonic",""); if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private const int Iterations = 600_000;
    private static readonly byte[] Aad = Encoding.UTF8.GetBytes("DCWallet:v1:BIP39");
    public static Task CreateAsync(string path,string mnemonic,string password) {
        if(!DecimalWallet.IsValidMnemonic(mnemonic))throw new ArgumentException("Неверная seed-фраза BIP39.");
        return CreatePayloadAsync(path,mnemonic.Trim(),password,1);
    }
    public static Task CreatePrivateKeyAsync(string path,string privateKey,string password)=>CreatePayloadAsync(path,JsonSerializer.Serialize(new VaultCredential("PrivateKey",WalletSigningSession.ValidatePrivateKey(privateKey))),password,2);
    private static async Task CreatePayloadAsync(string path, string mnemonic, string password,int version)
    {
        if (password.Length < 12) throw new ArgumentException("Пароль должен содержать не менее 12 символов.");
        var salt = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        var plain = Encoding.UTF8.GetBytes(mnemonic.Trim());
        var cipher = new byte[plain.Length]; var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag, version==1?Aad:Encoding.UTF8.GetBytes("DCWallet:v2:Credential"));
            var envelope = new VaultEnvelope(version, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(nonce), Convert.ToBase64String(cipher), Convert.ToBase64String(tag));
            var full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            // Never overwrite an existing wallet, including when two app instances race.
            var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(envelope));
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(temporary, full, false);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }

    public static async Task<string> OpenAsync(string path,string password) {
        var credential=await OpenCredentialAsync(path,password);
        if(credential.Kind!="Mnemonic")throw new InvalidDataException("Это кошелек приватного ключа, не seed-фразы.");
        return credential.Secret;
    }
    public static async Task<VaultCredential> OpenCredentialAsync(string path, string password)
    {
        if (new FileInfo(path).Length > 16_384) throw new InvalidDataException("Неверный файл кошелька.");
        var e = JsonSerializer.Deserialize<VaultEnvelope>(await File.ReadAllTextAsync(path)) ?? throw new InvalidDataException();
        if (e.Version is not (1 or 2) || e.Iterations != Iterations) throw new InvalidDataException("Неподдерживаемый формат кошелька.");
        var salt = Convert.FromBase64String(e.Salt); var nonce = Convert.FromBase64String(e.Nonce);
        var cipher = Convert.FromBase64String(e.Ciphertext); var tag = Convert.FromBase64String(e.Tag);
        if (salt.Length != 32 || nonce.Length != 12 || tag.Length != 16 || cipher.Length > 4096) throw new InvalidDataException();
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, e.Iterations, HashAlgorithmName.SHA256, 32);
        var plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key, 16); aes.Decrypt(nonce, cipher, tag, plain, e.Version==1?Aad:Encoding.UTF8.GetBytes("DCWallet:v2:Credential"));
            var mnemonic = Encoding.UTF8.GetString(plain);
            if(e.Version==2){var credential=JsonSerializer.Deserialize<VaultCredential>(mnemonic)??throw new InvalidDataException("Неверное содержимое кошелька.");if(credential.Kind!="PrivateKey")throw new InvalidDataException("Неизвестный тип кошелька.");return new("PrivateKey",WalletSigningSession.ValidatePrivateKey(credential.Secret));}
            if (!DecimalWallet.IsValidMnemonic(mnemonic)) throw new InvalidDataException("Неверное содержимое кошелька.");
            return new("Mnemonic",mnemonic);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
}
