using System.Numerics;
using Decimal.Web3;
using Decimal.Web3.Tokens;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;
namespace DCWallet.Core;
/// <summary>Reviewed Testnet-only Permit boundary. No automatic acceptance of changed deployments.</summary>
public static class DesktopPermitPolicy
{
    const string Slot="0x360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc";
    public static async Task ValidateAsync(DecimalClient client,string owner,string token,string spender,BigInteger amount)
    {
        if(client.Network.ChainId!=202020||(await client.Web3.Eth.ChainId.SendRequestAsync()).Value!=202020)throw new NotSupportedException("Permit доступен только в проверенном профиле Decimal Testnet. Используйте отдельное разрешение точной суммы.");
        if(amount<=0)throw new ArgumentException("Введите положительную сумму.");
        async Task Code(string address,string expected){var code=await client.Web3.Eth.GetCode.SendRequestAsync(address);if(new Sha3Keccack().CalculateHashFromHex(code)!=expected)throw new InvalidOperationException("Контракт Permit изменился. Операция остановлена.");}
        async Task Proxy(string address,string implementation,string proxyHash,string hash){await Code(address,proxyHash);var slot=await client.Web3.Eth.GetStorageAt.SendRequestAsync(address,new HexBigInteger(Slot));if(!slot.Equals("0x"+new string('0',24)+implementation[2..],StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Реализация Permit изменилась.");await Code(implementation,hash);}
        const string center="0xb8f9c4a26b91a65ac0d3f34e230a4c9ad09aef4a";
        await Proxy(center,"0x5f0de38983be6151a68b07363a0fb61897b50109","11661ddf372d5c42c5e018e0ef2d7eaa36de07de2c97f8a937360dbb8d3c7563","72a5e90add7e2e4060a699aaebf45e54180bfe033a8ba3a27424a5003fba2fa5");
        if(spender.Equals(CombinedNftDelegation.Proxy,StringComparison.OrdinalIgnoreCase))await Proxy(spender,CombinedNftDelegation.Implementation,"47c827859b09ad5488ec21a128f98d1d5a7a786be4fde7f08c962d7aa7756b77","768280a3b02be3b6e033d1b7333daa4c5c2cbc0cf7bcc12cc8bde4d465fcf57b");
        else if(!spender.Equals(center,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Получатель Permit не входит в проверенный профиль.");
        // This pinned reserve proxy runtime embeds TokenCenter as its immutable beacon.
        // Accepting a different runtime would require a separately reviewed deployment profile.
        await Code(token,"f772463a5d4611357774db16a2ddeaf62ca1e27c5033927c77763048026c1819");
        const string abi="[{\"name\":\"implementation\",\"type\":\"function\",\"inputs\":[],\"outputs\":[{\"type\":\"address\"}]},{\"name\":\"tokens\",\"type\":\"function\",\"inputs\":[{\"type\":\"string\"}],\"outputs\":[{\"type\":\"address\"}]}]";
        var contract=client.Web3.Eth.GetContract(abi,center);var implementation=await contract.GetFunction("implementation").CallAsync<string>();if(!implementation.Equals("0x8e215123f9cf7d2c7d99457c5096bea82a868cbe",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Реализация резервного токена изменилась.");
        await Code(implementation,"67ef16857177e434b860586efd9ff7cf2c57c1adf4ba50bd9310f42f9c8c2cf1");
        var service=new Erc20Service(client,token);var symbol=await service.GetSymbolAsync();var mapped=await contract.GetFunction("tokens").CallAsync<string>(symbol);if(!mapped.Equals(token,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Токен не подтвержден реестром сети.");
        if(await service.GetAllowanceRawAsync(owner,spender)!=0||await service.GetBalanceRawAsync(owner)<amount||await new ReservableTokenService(client,token).GetReserveWeiAsync()<=0)throw new InvalidOperationException("Баланс, резерв или разрешение изменились. Подготовьте операцию заново.");
    }
}
