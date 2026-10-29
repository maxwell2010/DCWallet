using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace DCWallet.Core;
public sealed record ChecksPackageInfo(string ChainId,string Contract,string Hash,string Signer,string Token,string AmountRaw,int Decimals,string DueBlock);
public sealed record ChecksPackageEnvelope(int Version,ChecksPackageInfo Info,string Salt,string Nonce,string Ciphertext,string Tag);
/// <summary>Password encrypted bearer authority, separate from the wallet seed and encrypted with authenticated metadata.</summary>
public static class ChecksPackage
{
    public const int MaxBytes=8192;
    private static byte[] Aad(ChecksPackageInfo info)=>Encoding.UTF8.GetBytes("DCWallet:check:v1:"+JsonSerializer.Serialize(info));
    private static void Validate(ChecksPackageInfo info)
    {
        if(!BigInteger.TryParse(info.ChainId,out var chain)||chain<=0||chain>(BigInteger.One<<256)-1)throw new InvalidDataException("Неверная сеть чека.");
        _=WalletDataReader.Address(info.Contract);_=WalletDataReader.Address(info.Signer);if(info.Token!="DEL")_=WalletDataReader.Address(info.Token);
        if(info.Hash.Length!=66||!info.Hash.StartsWith("0x")||Convert.FromHexString(info.Hash[2..]).Length!=32)throw new InvalidDataException("Неверный hash чека.");
        if(!BigInteger.TryParse(info.AmountRaw,out var amount)||amount<=0||amount>(BigInteger.One<<256)-1||info.Decimals is <0 or >255||!BigInteger.TryParse(info.DueBlock,out var due)||due<0||due>(BigInteger.One<<256)-1)throw new InvalidDataException("Неверные параметры чека.");
    }
    public static byte[] Export(CheckSecret secret,ChecksPackageInfo info,string password)
    {
        Validate(info);if(!secret.Address.Equals(info.Signer,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Подписант чека не совпадает.");
        if(password.Length<12)throw new ArgumentException("Для файла чека задайте отдельный пароль не короче 12 символов.");
        var salt=RandomNumberGenerator.GetBytes(32);var nonce=RandomNumberGenerator.GetBytes(12);var tag=new byte[16];
        var key=Rfc2898DeriveBytes.Pbkdf2(password,salt,600000,HashAlgorithmName.SHA256,32);
        try { var cipher=secret.ExportProtected(plain=>{var result=new byte[plain.Length];using var aes=new AesGcm(key,16);aes.Encrypt(nonce,plain,result,tag,Aad(info));return result;});return JsonSerializer.SerializeToUtf8Bytes(new ChecksPackageEnvelope(1,info,Convert.ToBase64String(salt),Convert.ToBase64String(nonce),Convert.ToBase64String(cipher),Convert.ToBase64String(tag))); }
        finally {CryptographicOperations.ZeroMemory(key);}
    }
    public static (ChecksPackageInfo Info,CheckSecret Secret) Open(byte[] bytes,string password,BigInteger expectedChain,string expectedContract)
    {
        if(bytes.Length>MaxBytes)throw new InvalidDataException("Слишком большой файл чека.");
        var e=JsonSerializer.Deserialize<ChecksPackageEnvelope>(bytes)??throw new InvalidDataException("Неверный файл чека.");Validate(e.Info);
        if(e.Version!=1||e.Info.ChainId!=expectedChain.ToString()||!e.Info.Contract.Equals(expectedContract,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Чек относится к другой сети или контракту.");
        var salt=Convert.FromBase64String(e.Salt);var nonce=Convert.FromBase64String(e.Nonce);var tag=Convert.FromBase64String(e.Tag);var cipher=Convert.FromBase64String(e.Ciphertext);
        if(salt.Length!=32||nonce.Length!=12||tag.Length!=16||cipher.Length!=32)throw new InvalidDataException("Неверный файл чека.");
        var key=Rfc2898DeriveBytes.Pbkdf2(password,salt,600000,HashAlgorithmName.SHA256,32);
        try { var secret=CheckSecret.FromProtected(cipher,data=>{var plain=new byte[32];try{using var aes=new AesGcm(key,16);aes.Decrypt(nonce,data,tag,plain,Aad(e.Info));return plain;}catch{CryptographicOperations.ZeroMemory(plain);throw;}});if(!secret.Address.Equals(e.Info.Signer,StringComparison.OrdinalIgnoreCase)){secret.Dispose();throw new InvalidDataException("Подписант чека не совпадает.");}return(e.Info,secret); }
        finally {CryptographicOperations.ZeroMemory(key);}
    }
}
