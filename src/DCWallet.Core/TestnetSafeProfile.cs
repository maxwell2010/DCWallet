using System.Numerics;
using System.Security.Cryptography;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;
namespace DCWallet.Core;
/// <summary>Explicit chain202020 deployment; never substitutes a mainnet deployment.</summary>
public static class TestnetSafeProfile
{
    public const string Singleton = "0xe0f30fccae2f9f9c7efee9af044c436841d466ee";
    public const string Factory = "0x4cc406b1713d3df67e739d6f5918b6c98f614a38";
    public const string SingletonSha256 = "f49186973c2c5b4dbcbba188fdc7fffd9c56fbb6ca86fd20aa4e6640987b2b23";
    public const string FactorySha256 = "bb8b0fe03c6fe89ac3d7e20771fb00ead353aecddf5e974d3bdd6c9f4f485b8f";
    public static async Task VerifyAsync(DecimalClient client)
    {
        if (client.Network.ChainId != 202020 || (await client.Web3.Eth.ChainId.SendRequestAsync()).Value != 202020) throw new InvalidOperationException("Safe profile requires testnet202020.");
        foreach(var item in new[]{(Singleton,SingletonSha256),(Factory,FactorySha256)})
        {
            var code=await client.Web3.Eth.GetCode.SendRequestAsync(item.Item1);
            if (code is null || code.Length<=2 || !Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).Equals(item.Item2,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Testnet Safe runtime changed; review deployment profile.");
        }
    }
    public static TransactionDraft Create(string from,IReadOnlyList<SafeMember> members,BigInteger threshold,BigInteger salt,long chainId)
    {
        if(chainId!=202020)throw new InvalidOperationException("Safe profile requires testnet202020.");
        SafeQuorum.Validate(members,threshold);
        if(members.Count>20||salt<0||salt>=(BigInteger.One<<256))throw new ArgumentException("Invalid owners or salt.");
        var setup=new WeightedSafeDrafts.SafeSetup {Owners=members.Select(m=>new WeightedSafeDrafts.WeightedOwner {Owner=m.Owner,Weight=m.Weight}).ToList(),Threshold=threshold};
        var init=new FunctionMessageEncodingService<WeightedSafeDrafts.SafeSetup>(Singleton,from).GetCallData(setup);
        var data=new FunctionMessageEncodingService<WeightedSafeDrafts.SafeCreate>(Factory,from).GetCallData(new(){Singleton=Singleton,Initializer=init,Salt=salt});
        return TransactionDraft.ContractCall(from,Factory,"0x"+Convert.ToHexString(data).ToLowerInvariant());
    }
    public static string CreatedAddress(TransactionReceipt receipt)
    {
        if(receipt.Status?.Value!=1)throw new InvalidOperationException("Safe creation failed.");
        var signature="0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash("ProxyCreation(address,address)");var addresses=new List<string>();
        foreach(var log in receipt.Logs??[])
        {
            var topics=log.Topics?.Select(t=>t.ToString()??"").ToArray()??[];
            if(!log.Address.Equals(Factory,StringComparison.OrdinalIgnoreCase)||topics.Length==0||!topics[0].Equals(signature,StringComparison.OrdinalIgnoreCase))continue;
            string proxy,implementation;
            // Both standard factory event encodings are accepted, with strict word lengths and emitter/singleton binding.
            if(topics.Length==1&&log.Data.Length==130){proxy="0x"+log.Data.Substring(26,40);implementation="0x"+log.Data.Substring(90,40);}
            else if(topics.Length==2&&topics[1].Length==66&&log.Data.Length==66){proxy="0x"+topics[1][26..];implementation="0x"+log.Data[26..];}
            else throw new InvalidOperationException("Malformed Safe factory event.");
            if(implementation.Equals(Singleton,StringComparison.OrdinalIgnoreCase))addresses.Add(proxy);
        }
        if(addresses.Count!=1||!System.Text.RegularExpressions.Regex.IsMatch(addresses[0],"^0x[0-9a-fA-F]{40}$")||addresses[0]==TransactionDraft.BurnAddress)throw new InvalidOperationException("Missing matching Safe factory creation event.");return addresses[0];
    }
}
