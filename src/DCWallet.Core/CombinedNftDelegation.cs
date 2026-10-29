using System.Numerics;
using System.Security.Cryptography;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
namespace DCWallet.Core;
/// <summary>Explicit deployment profile from current ERC1967 storage and official JS SDK 6790d35 call.ts.</summary>
public static class CombinedNftDelegation
{
    public sealed record Position(string Nft,BigInteger TokenId,BigInteger Amount,byte NftType,string Delegator,string Validator,string ReserveToken,BigInteger ReserveAmount,BigInteger Hold,bool IsActive);
    const string ReadAbi="""[{"type":"function","name":"getNFTStake","inputs":[{"type":"address"},{"type":"uint256"},{"type":"address"},{"type":"address"},{"type":"uint256"}],"outputs":[]}]""";
    public static async Task<Position?> GetStakeAsync(DecimalClient client,string nft,BigInteger id,string owner,string validator,BigInteger hold,BlockParameter? block=null,CancellationToken ct=default)
    {
        _=WalletDataReader.Address(nft);_=WalletDataReader.Address(owner);_=WalletDataReader.Address(validator);
        if(id<0||hold<0||id>=(BigInteger.One<<256)||hold>=(BigInteger.One<<256))throw new ArgumentOutOfRangeException(nameof(id));
        await VerifyAsync(client).WaitAsync(ct);
        var data=client.Web3.Eth.GetContract(ReadAbi,Proxy).GetFunction("getNFTStake").GetData(nft,id,owner,validator,hold);
        var raw=await client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{To=Proxy,Data=data},block??BlockParameter.CreateLatest()).WaitAsync(ct);
        var position=DecodePosition(raw);
        if(position is not null&&(!position.Nft.Equals(nft,StringComparison.OrdinalIgnoreCase)||position.TokenId!=id||!position.Delegator.Equals(owner,StringComparison.OrdinalIgnoreCase)||!position.Validator.Equals(validator,StringComparison.OrdinalIgnoreCase)||position.Hold!=hold))throw new InvalidDataException("NFT stake identity differs from requested position.");
        return position;
    }
    public static Position? DecodePosition(string raw)
    {
        if(raw is null||raw.Length!=642||!raw.StartsWith("0x")||!raw[2..].All(Uri.IsHexDigit))throw new InvalidDataException("NFT stake must contain exactly ten ABI words.");
        BigInteger Uint(int i)=>new(Convert.FromHexString(raw.Substring(2+i*64,64)),true,true);
        string Address(int i){var word=raw.Substring(2+i*64,64);if(word[..24].Any(c=>c!='0'))throw new InvalidDataException("Invalid NFT address padding.");return WalletDataReader.Address("0x"+word[24..]);}
        if(Uint(9)>1||Uint(3)>byte.MaxValue)throw new InvalidDataException("Invalid NFT type/active flag.");
        if(raw[2..].All(c=>c=='0'))return null;
        var result=new Position(Address(0),Uint(1),Uint(2),(byte)Uint(3),Address(4),Address(5),Address(6),Uint(7),Uint(8),Uint(9)==1);
        if(result.NftType is not(2 or 3))throw new InvalidDataException("Position does not contain a supported NFT type.");
        return result;
    }
    public const string Proxy="0x0d8e8ce36dade42682dd8cc6288d6a8d324bac2c", Implementation="0x2f29724dcf9e3fe2864db03c5310cb368aa96f09";
    public const string RuntimeSha256="ddd4687ae3204d3178180ac762c9241cbc30c27a7f984fc130f219f58132d1fc";
    const string Slot="0x360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc";
    const string Abi="""
    [{"type":"function","name":"delegateNFT1155","inputs":[{"type":"address"},{"type":"address"},{"type":"uint256"},{"type":"uint256"}],"outputs":[]},
    {"type":"function","name":"withdrawNFT1155","inputs":[{"type":"address"},{"type":"address"},{"type":"uint256"}],"outputs":[]}]
    """;
    public static async Task VerifyAsync(DecimalClient client)
    {
        if(client.Network.ChainId!=202020 || (await client.Web3.Eth.ChainId.SendRequestAsync()).Value!=202020) throw new InvalidOperationException("NFT combined profile requires testnet202020.");
        var implementation=await client.Web3.Eth.GetStorageAt.SendRequestAsync(Proxy,new HexBigInteger(Slot));
        if(!implementation.EndsWith(Implementation[2..],StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("NFT delegation implementation changed.");
        var code=await client.Web3.Eth.GetCode.SendRequestAsync(Implementation);
        if(!Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).Equals(RuntimeSha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("NFT delegation runtime differs from reviewed profile.");
    }
    public static TransactionDraft Delegate1155(DecimalClient client,string from,string validator,string nft,BigInteger id,BigInteger quantity)
    {
        if(client.Network.ChainId!=202020)throw new NotSupportedException("Combined NFT profile is testnet-only."); _=WalletDataReader.Address(validator); _=WalletDataReader.Address(nft); if(quantity<=0 || quantity >= (BigInteger.One << 256) || id<0 || id >= (BigInteger.One << 256))throw new ArgumentOutOfRangeException(nameof(quantity));
        return TransactionDraft.ContractCall(from,Proxy,client.Web3.Eth.GetContract(Abi,Proxy).GetFunction("delegateNFT1155").GetData(validator,nft,id,quantity));
    }
    public static TransactionDraft Withdraw1155(DecimalClient client,string from,string validator,string nft,BigInteger id)
        { if(client.Network.ChainId!=202020)throw new NotSupportedException("Combined NFT profile is testnet-only."); _=WalletDataReader.Address(validator); _=WalletDataReader.Address(nft); if(id<0 || id >= (BigInteger.One << 256))throw new ArgumentOutOfRangeException(nameof(id)); return TransactionDraft.ContractCall(from,Proxy,client.Web3.Eth.GetContract(Abi,Proxy).GetFunction("withdrawNFT1155").GetData(validator,nft,id)); }
}
