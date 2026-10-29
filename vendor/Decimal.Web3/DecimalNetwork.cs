namespace Decimal.Web3;

/// <summary>Network and optional system contract addresses. Custom nodes are opt-in.</summary>
public sealed record DecimalNetwork(
    string Name,
    long ChainId,
    Uri RpcUrl,
    string DelegationContract = "",
    string TokenCenterContract = "",
    string NftCenterContract = "",
    string ContractCenterContract = "",
    string WdelContract = "")
{
    public static DecimalNetwork Mainnet() => new(
        "decimal-mainnet",
        75,
        new Uri("https://node.decimalchain.com/web3/"),
        "0xa16c34ed1c0601c0e749e17ebef19752a15faa01",
        "0x9113ba675aa8f2ef0c068cee2cdabab95b6437fb",
        "0x443cc8ac24630be9257483a09c34ffa81608eace",
        "0xc108715a06f76caa96fa2c943ebf05159c29a87d",
        "0x1c5d8992da64c8d56ea413dd6f723061c29a7c0b");

    public static DecimalNetwork Testnet() => new(
        "decimal-testnet",
        202020,
        new Uri("https://testnet-val.decimalchain.com/web3/"),
        ContractCenterContract: "0xbC96b61F137F28F0Da47Cc4Ef06e5f984B565A2E");

    public static DecimalNetwork Custom(string name, long chainId, string rpcUrl,
        string delegationContract = "", string tokenCenterContract = "", string nftCenterContract = "",
        string contractCenterContract = "", string wdelContract = "")
    {
        if (chainId <= 0) throw new ArgumentOutOfRangeException(nameof(chainId));
        var uri = new Uri(rpcUrl, UriKind.Absolute);
        if (uri.Scheme is not ("https" or "http"))
            throw new ArgumentException("RPC URL must use HTTP or HTTPS", nameof(rpcUrl));
        return new(name, chainId, uri, delegationContract, tokenCenterContract, nftCenterContract,
            contractCenterContract, wdelContract);
    }
}
