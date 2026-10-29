using System.Numerics;
using System.Text.Json;
using Decimal.Web3;
using Decimal.Web3.Transactions;
namespace DCWallet.Core;
/// <summary>Current official combined delegation NFT ABI; callers verify deployment before preview.</summary>
public static class StakingNftDrafts
{
    public static TransactionDraft Delegate(DecimalClient client,string owner,string validator,string nft,BigInteger id,BigInteger amount,bool erc721,BigInteger hold)
    {
        if(amount<=0 || (erc721 && amount!=1))throw new ArgumentOutOfRangeException(nameof(amount));
        if(hold<0 || hold>=(BigInteger.One<<256))throw new ArgumentOutOfRangeException(nameof(hold));
        var args=new List<object>{validator,nft,id};var types=new List<string>{"address","address","uint256"};
        if(!erc721){args.Add(amount);types.Add("uint256");}
        if(hold>0){args.Add(hold);types.Add("uint256");}
        return Build(client,owner,erc721?"delegateNFT"+(hold>0?"Hold":""):"delegateNFT1155"+(hold>0?"Hold":""),types,args);
    }
    public static TransactionDraft Withdraw(DecimalClient client,string owner,string validator,string nft,BigInteger id,bool erc721,BigInteger hold)
    {
        if(hold<0 || hold>=(BigInteger.One<<256))throw new ArgumentOutOfRangeException(nameof(hold));
        var args=new List<object>{validator,nft,id};var types=new List<string>{"address","address","uint256"};
        if(hold>0){args.Add(hold);types.Add("uint256");}
        return Build(client,owner,(erc721?"withdrawNFT":"withdrawNFT1155")+(hold>0?"Hold":""),types,args);
    }
    public static TransactionDraft Transfer(DecimalClient client,string owner,string validator,string nft,BigInteger id,bool erc721,BigInteger hold,string newValidator)
    {
        if(hold<0 || hold>=(BigInteger.One<<256))throw new ArgumentOutOfRangeException(nameof(hold));
        var args=new List<object>{validator,nft,id};var types=new List<string>{"address","address","uint256"};
        if(hold>0){args.Add(hold);types.Add("uint256");}args.Add(newValidator);types.Add("address");
        return Build(client,owner,(erc721?"transferNFT":"transferNFT1155")+(hold>0?"Hold":""),types,args);
    }
    public static TransactionDraft Delegate721ByPermit(DecimalClient client,string owner,string validator,string nft,BigInteger id,BigInteger deadline,byte v,byte[] r,byte[] signatureS)
    {
        if(client.Network.ChainId!=202020)throw new NotSupportedException("Reviewed combined NFT ABI is testnet-only.");
        _=WalletDataReader.Address(owner);_=WalletDataReader.Address(validator);_=WalletDataReader.Address(nft);
        if(id<0||id>=(BigInteger.One<<256)||deadline<=0||deadline>=(BigInteger.One<<256)||v is not(27 or 28)||r is null||r.Length!=32||signatureS is null||signatureS.Length!=32)throw new ArgumentException("Invalid NFT permit fields.");
        const string abi="""[{"type":"function","name":"delegateNFTByPermit","inputs":[{"type":"address"},{"type":"address"},{"type":"uint256"},{"type":"uint256"},{"type":"uint8"},{"type":"bytes32"},{"type":"bytes32"}],"outputs":[]}]""";
        return TransactionDraft.ContractCall(owner,CombinedNftDelegation.Proxy,client.Web3.Eth.GetContract(abi,CombinedNftDelegation.Proxy).GetFunction("delegateNFTByPermit").GetData(validator,nft,id,deadline,v,(byte[])r.Clone(),(byte[])signatureS.Clone()));
    }
    static TransactionDraft Build(DecimalClient client,string owner,string name,List<string> types,List<object> args)
    {
        if(client.Network.ChainId!=202020)throw new NotSupportedException("Reviewed combined NFT ABI is testnet-only.");
        _=WalletDataReader.Address(owner);
        for(var i=0;i<args.Count;i++)if(types[i]=="address")_=WalletDataReader.Address((string)args[i]);else if((BigInteger)args[i]<0||(BigInteger)args[i]>=(BigInteger.One<<256))throw new ArgumentOutOfRangeException(nameof(args));
        var abi=JsonSerializer.Serialize(new[]{new{type="function",name,inputs=types.Select(type=>new{type}).ToArray(),outputs=Array.Empty<object>()}});
        return TransactionDraft.ContractCall(owner,CombinedNftDelegation.Proxy,client.Web3.Eth.GetContract(abi,CombinedNftDelegation.Proxy).GetFunction(name).GetData(args.ToArray()));
    }
}
