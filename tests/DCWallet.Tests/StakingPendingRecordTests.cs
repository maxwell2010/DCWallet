using System.Numerics;
using System.Text.Json;
using Xunit;
namespace DCWallet.Tests;
public class StakingPendingRecordTests
{
 [Fact]public void Erc20RecoveryPreservesExactBaselineWithoutBigIntegerObjectEncoding(){var input=new Erc20StakingNetworkScenario.Pending("100000000000000000001","99999999999999999999999999","1","123","456","0x"+new string('a',64));var json=JsonSerializer.Serialize(input);Assert.DoesNotContain("IsPowerOfTwo",json);var output=JsonSerializer.Deserialize<Erc20StakingNetworkScenario.Pending>(json)!;Assert.Equal(input,output);Assert.Equal(BigInteger.Parse(input.BalanceRaw),output.Baseline.Balance);}
 [Fact]public void NftRecoveryRoundtripsAllReceiptCoordinates(){var input=new Nft721NetworkScenario.Pending("0x"+new string('1',40),"999999999999999999","123","456","0x"+new string('a',64),"0x"+new string('b',64));Assert.Equal(input,JsonSerializer.Deserialize<Nft721NetworkScenario.Pending>(JsonSerializer.Serialize(input)));}
}
