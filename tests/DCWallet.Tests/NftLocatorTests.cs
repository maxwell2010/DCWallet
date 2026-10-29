using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public class NftLocatorTests
{
 const string Address="0x1111111111111111111111111111111111111111";
 [Fact]public void LocatorCacheStoresOnlyPublicCoordinatesAndDeduplicatesAtomically()
 {
  var dir=Path.Combine(Path.GetTempPath(),"dc-nft-locator-"+Guid.NewGuid());
  try{using var reader=new WalletDataReader(new DecimalClient(DecimalNetwork.Testnet()),dir);Assert.True(reader.RememberNftStake(Address,Address,Address,1,0));Assert.True(reader.RememberNftStake(Address,Address,Address,1,0));var file=Assert.Single(Directory.GetFiles(dir));using var doc=JsonDocument.Parse(File.ReadAllText(file));var hint=Assert.Single(doc.RootElement.EnumerateArray());Assert.Equal("1",hint.GetProperty("TokenId").GetString());Assert.False(hint.TryGetProperty("Amount",out _));Assert.False(hint.TryGetProperty("Balance",out _));Assert.Empty(Directory.GetFiles(dir,"*.tmp"));}finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
 [Fact]public void InvalidLocatorCannotCreateCache(){var dir=Path.Combine(Path.GetTempPath(),"dc-nft-locator-"+Guid.NewGuid());using var reader=new WalletDataReader(new DecimalClient(DecimalNetwork.Testnet()),dir);Assert.Throws<InvalidDataException>(()=>reader.RememberNftStake("../bad",Address,Address,1,0));Assert.False(Directory.Exists(dir));}
 [Fact]public void UnconfiguredCacheReturnsFalse(){using var reader=new WalletDataReader(new DecimalClient(DecimalNetwork.Testnet()));Assert.False(reader.RememberNftStake(Address,Address,Address,1,0));}
}
