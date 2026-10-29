using System.Security.Cryptography;
using Decimal.Web3;
using Decimal.Web3.Checks;
namespace DCWallet.Core;
public static class ChecksDeploymentProfile
{
 public const string TestnetContract="0xb6161CAA8Bd7167C2fa67b93b041FF9a94d6a735";
 public const string MainnetContract="0x9c3326594b49AFa24db9b988Cda1258d0dcb4e40";
 public const string RuntimeSha256="7a9a364077d8ba3f03e77cfb09e79599faea344568ffa245903ccdb90691139e";
 public static async Task VerifyAsync(DecimalClient client,string contract)
 {
  var mainnet=client.Network.ChainId==75&&contract.Equals(MainnetContract,StringComparison.OrdinalIgnoreCase);
  var testnet=client.Network.ChainId==202020&&contract.Equals(TestnetContract,StringComparison.OrdinalIgnoreCase);
  if(!mainnet&&!testnet)throw new InvalidOperationException("Для этой сети и адреса Checks нет проверенного профиля.");
  if((await client.Web3.Eth.ChainId.SendRequestAsync()).Value!=client.Network.ChainId)throw new InvalidOperationException("Другая сеть RPC.");
  var implementation=await new ChecksService(client.Web3,contract).GetImplementationAsync();
  var expected=mainnet?"0x0000000000000000000000000000000000000000":"0xbaca5a733002f5323abe4c83c91b87212e7a3ca3";
  if(!implementation.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Checks implementation изменился.");
  // Mainnet is the reviewed direct-runtime deployment, not the testnet proxy. Owner zero is not a behavior test.
  var code=await client.Web3.Eth.GetCode.SendRequestAsync(mainnet?contract:implementation);
  if(code is null||!code.StartsWith("0x",StringComparison.OrdinalIgnoreCase)||code.Length<4||!code[2..].All(Uri.IsHexDigit)||((code.Length-2)%2)!=0)throw new InvalidOperationException("Checks runtime отсутствует или поврежден.");
  if(!Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).Equals(RuntimeSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Checks runtime не соответствует проверенному профилю.");
 }
}
