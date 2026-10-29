using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Nft;
using Xunit;
namespace DCWallet.Tests;
public class NativeNftCompatibilityTests
{
 const string Owner="0x1111111111111111111111111111111111111111";
 [Fact]public async Task WorkingLegacySelectorIsPreserved(){using var rpc=new NativeRpc(false,false);var client=new DecimalClient(DecimalNetwork.Custom("fixture",75,rpc.Url));var original=new DecimalNftDrafts(client.Network,Owner).BuildMintErc721ByEthDraft(Owner,Owner,Owner,"ipfs://qa",1);Assert.Same(original,await CurrentNftDrafts.AdaptLegacyNativeDraftAsync(client,original,Owner));Assert.Single(rpc.Estimates);Assert.Equal(0,rpc.Writes);}
 [Fact]public async Task RejectedEthAndAcceptedDelPreserveEveryOtherSignedField(){using var rpc=new NativeRpc(true,false);var client=new DecimalClient(DecimalNetwork.Custom("fixture",75,rpc.Url));var original=new DecimalNftDrafts(client.Network,Owner).BuildMintErc721ByEthDraft(Owner,Owner,Owner,"ipfs://qa",1);var adapted=await CurrentNftDrafts.AdaptLegacyNativeDraftAsync(client,original,Owner);Assert.StartsWith("0xb125b73f",adapted.Data);Assert.Equal(original.Data[10..],adapted.Data[10..]);Assert.Equal(original.From,adapted.From);Assert.Equal(original.To,adapted.To);Assert.Equal(original.ValueWei,adapted.ValueWei);Assert.Equal(2,rpc.Estimates.Count);Assert.Equal(0,rpc.Writes);}
 [Fact]public async Task BothRejectedNeverReturnInventedCompatibleDraft(){using var rpc=new NativeRpc(true,true);var client=new DecimalClient(DecimalNetwork.Custom("fixture",75,rpc.Url));var original=new DecimalNftDrafts(client.Network,Owner).BuildMintErc721ByEthDraft(Owner,Owner,Owner,"ipfs://qa",1);await Assert.ThrowsAsync<NotSupportedException>(()=>CurrentNftDrafts.AdaptLegacyNativeDraftAsync(client,original,Owner));Assert.StartsWith("0x054198c7",original.Data);Assert.Equal(2,rpc.Estimates.Count);Assert.Equal(0,rpc.Writes);}
 [Fact]public async Task WrongSenderAndRpcChainCannotTriggerAdaptation(){using var rpc=new NativeRpc(true,false);var client=new DecimalClient(DecimalNetwork.Custom("fixture",202020,rpc.Url));var original=new DecimalNftDrafts(client.Network,Owner).BuildMintErc721ByEthDraft(Owner,Owner,Owner,"ipfs://qa",1);await Assert.ThrowsAsync<InvalidOperationException>(()=>CurrentNftDrafts.AdaptLegacyNativeDraftAsync(client,original,"0x2222222222222222222222222222222222222222"));await Assert.ThrowsAsync<InvalidOperationException>(()=>CurrentNftDrafts.AdaptLegacyNativeDraftAsync(client,original,Owner));Assert.Empty(rpc.Estimates);Assert.Equal(0,rpc.Writes);}
 sealed class NativeRpc:IDisposable {
  readonly TestHttpListener listener=new();readonly Task loop;public string Url{get;}public List<string> Estimates{get;}=[];public int Writes;
  public NativeRpc(bool rejectEth,bool rejectDel){var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();var port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();Url=$"http://127.0.0.1:{port}/";listener.Prefixes.Add(Url);listener.Start();loop=Task.Run(async()=>{while(listener.IsListening){TestHttpContext context;try{context=await listener.GetContextAsync();}catch{break;}using var doc=JsonDocument.Parse(await new StreamReader(context.Request.InputStream).ReadToEndAsync());var root=doc.RootElement;var method=root.GetProperty("method").GetString();object? result="0x0";var rejected=false;if(method=="eth_chainId")result="0x4b";else if(method=="eth_gasPrice")result="0x1";else if(method=="eth_getBalance")result="0xde0b6b3a7640000";else if(method=="eth_estimateGas"){var data=root.GetProperty("params")[0].GetProperty("data").GetString()!;Estimates.Add(data[..10]);rejected=data.StartsWith("0x054198c7")?rejectEth:rejectDel;result="0x5208";}else if(method=="eth_sendRawTransaction")Writes++;object response=rejected?new{jsonrpc="2.0",id=root.GetProperty("id").Clone(),error=new{code=-32000,message="execution reverted",data="0x"}}:new{jsonrpc="2.0",id=root.GetProperty("id").Clone(),result};var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));context.Response.ContentType="application/json";await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();}});}
  public void Dispose(){listener.Stop();listener.Close();}
 }
}
