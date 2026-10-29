namespace DCWallet.Core;
/// <summary>Deployment-specific block, not a claim that another contract shares the old NFT ABI.</summary>
public static class NftDelegationSupport
{
    public static async Task RequireDelegationEnabledAsync(Decimal.Web3.DecimalClient client, string contract)
    {
        RequireSupported(client.Network.ChainId, contract);
        if (client.Network.ChainId != 75) return;
        _ = WalletDataReader.Address(contract);
        if ((await client.Web3.Eth.ChainId.SendRequestAsync()).Value != 75) throw new InvalidOperationException("RPC вернул другую сеть.");
        const string abi = """[{"type":"function","name":"paused","inputs":[],"outputs":[{"type":"bool"}]}]""";
        var raw = await client.Web3.Eth.Transactions.Call.SendRequestAsync(new Nethereum.RPC.Eth.DTOs.CallInput { To = contract, Data = client.Web3.Eth.GetContract(abi, contract).GetFunction("paused").GetData() });
        if (raw is null || raw.Length != 66 || !raw.StartsWith("0x") || !raw[2..].All(Uri.IsHexDigit)) throw new InvalidDataException("Не удалось проверить состояние NFT-стейкинга.");
        var paused = new System.Numerics.BigInteger(Convert.FromHexString(raw[2..]), true, true);
        if (paused != 0 && paused != 1) throw new InvalidDataException("Контракт вернул неизвестное состояние паузы.");
        if (paused == 1) throw new InvalidOperationException("NFT-стейкинг этого контракта приостановлен в сети. Делегирование и новое разрешение для него сейчас не подготавливаются.");
    }
    public const string DeprecatedTestnetProxy = "0x07e2ad4dfc91412de09e33e4650254948b21a20c";
    public const string CombinedTestnetProxy = "0x0d8e8ce36dade42682dd8cc6288d6a8d324bac2c";
    public static void RequireSupported(long chainId, string delegationContract)
    {
        if (chainId != 202020) return;
        if (delegationContract.Equals(DeprecatedTestnetProxy, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Контракт NFT-стейкинга testnet помечен сетью как deprecated. Требуется обновление SDK под новую реализацию DecimalDelegation; замена адреса без нового ABI недопустима.");
        throw new NotSupportedException("ABI NFT-стейкинга текущего развертывания testnet не подтвержден. Требуется миграция SDK и проверка чтения/делегирования для этой реализации.");
    }
}
