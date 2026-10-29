using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public class ChecksDeploymentProfileTests
{
 [Fact]public async Task RejectsUnknownChainAndCrossNetworkAddressBeforeRpc(){var calls=0;using var rpc=new FakeRpc{ReadResult=(m,p)=>{calls++;return "0x4b";}};await Assert.ThrowsAsync<InvalidOperationException>(()=>ChecksDeploymentProfile.VerifyAsync(new DecimalClient(DecimalNetwork.Custom("fixture",75,rpc.Url)),ChecksDeploymentProfile.TestnetContract));await Assert.ThrowsAsync<InvalidOperationException>(()=>ChecksDeploymentProfile.VerifyAsync(new DecimalClient(DecimalNetwork.Custom("fixture",1,rpc.Url)),ChecksDeploymentProfile.MainnetContract));Assert.Equal(0,calls);}
 [Fact]public async Task RejectsRpcChainMismatch(){using var rpc=new FakeRpc{Chain="0x31524"};await Assert.ThrowsAsync<InvalidOperationException>(()=>ChecksDeploymentProfile.VerifyAsync(new DecimalClient(DecimalNetwork.Custom("fixture",75,rpc.Url)),ChecksDeploymentProfile.MainnetContract));}
 [Fact]public async Task MainnetDirectProfileRequiresZeroImplAndExactRuntimeAtCandidate(){string? codeAddress=null;using var rpc=new FakeRpc{ReadResult=(m,p)=>m switch{"eth_chainId"=>"0x4b","eth_call"=>"0x"+new string('0',64),"eth_getCode"=>Remember(p[0].GetString()),_=>null}};string Remember(string? address){codeAddress=address;return "0x6000";}await Assert.ThrowsAsync<InvalidOperationException>(()=>ChecksDeploymentProfile.VerifyAsync(new DecimalClient(DecimalNetwork.Custom("fixture",75,rpc.Url)),ChecksDeploymentProfile.MainnetContract));Assert.Equal(ChecksDeploymentProfile.MainnetContract,codeAddress,StringComparer.OrdinalIgnoreCase);}
 [Fact]public async Task MainnetRejectsUnexpectedImplementationBeforeCodeQuery(){var codeCalls=0;using var rpc=new FakeRpc{ReadResult=(m,p)=>{if(m=="eth_chainId")return "0x4b";if(m=="eth_call")return "0x"+new string('0',24)+new string('1',40);if(m=="eth_getCode"){codeCalls++;return "0x6000";}return null;}};await Assert.ThrowsAsync<InvalidOperationException>(()=>ChecksDeploymentProfile.VerifyAsync(new DecimalClient(DecimalNetwork.Custom("fixture",75,rpc.Url)),ChecksDeploymentProfile.MainnetContract));Assert.Equal(0,codeCalls);}
}
