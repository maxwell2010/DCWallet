using DCWallet.Core;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
namespace DCWallet.Tests;
public sealed class ChecksPackageTests
{
    private static ChecksPackageInfo Info(CheckSecret s)=>new("202020","0xb6161caa8bd7167c2fa67b93b041ff9a94d6a735","0x"+new string('a',64),s.Address,"DEL","1000000000000000",18,"99999999");
    [Fact] public void RoundtripKeepsSignerAndSignsRedeemerBoundDigest()
    {using var s=new CheckSecret();var info=Info(s);var bytes=ChecksPackage.Export(s,info,"a separate check password");var opened=ChecksPackage.Open(bytes,"a separate check password",202020,info.Contract);using var imported=opened.Secret;Assert.Equal(s.Address,imported.Address);Assert.Equal(s.SignRedeem(Convert.FromHexString(info.Hash[2..]),info.Contract),imported.SignRedeem(Convert.FromHexString(info.Hash[2..]),info.Contract));}
    [Fact] public void WrongPasswordAndMetadataTamperingFailAuthentication()
    {using var s=new CheckSecret();var info=Info(s);var bytes=ChecksPackage.Export(s,info,"a separate check password");Assert.ThrowsAny<CryptographicException>(()=>ChecksPackage.Open(bytes,"bad",202020,info.Contract));var envelope=JsonSerializer.Deserialize<ChecksPackageEnvelope>(bytes)!;var changed=JsonSerializer.SerializeToUtf8Bytes(envelope with{Info=info with{AmountRaw="2"}});Assert.ThrowsAny<CryptographicException>(()=>ChecksPackage.Open(changed,"a separate check password",202020,info.Contract));}
    [Fact] public void WrongNetworkAndUnboundedInputRejected()
    {using var s=new CheckSecret();var info=Info(s);var bytes=ChecksPackage.Export(s,info,"a separate check password");Assert.Throws<InvalidDataException>(()=>ChecksPackage.Open(bytes,"a separate check password",75,info.Contract));Assert.Throws<InvalidDataException>(()=>ChecksPackage.Open(new byte[8193],"",202020,info.Contract));Assert.Throws<ArgumentException>(()=>ChecksPackage.Export(s,info,"short"));}
}
