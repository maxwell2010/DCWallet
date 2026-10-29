using System.Numerics;
using System.Security.Cryptography;
using DCWallet.Core;
namespace DCWallet;
public sealed partial class MainWindow
{
    private void ShowSafeCreation()
    {
        if (!RequireWallet()) return;
        Reset("Создать общий счет Decimal Safe", "Веса участников определяют необходимое число голосов. Проверьте адреса и доступный вам набор ключей.");
        var members = Field("Владельцы: одна строка адрес:вес (1-1000)", Address + ":1", multiline: true);
        var threshold = Field("Необходимый суммарный вес", "1");
        var salt = Field("Уникальный salt nonce", new BigInteger(Guid.NewGuid().ToByteArray(), true).ToString());
        Button("Подготовить создание Safe", async () =>
        {
            var version = epoch; var owner = Address; var chain = network.ChainId;
            if (chain is not (75 or 202020) || (await client.Web3.Eth.ChainId.SendRequestAsync()).Value != chain) throw new InvalidOperationException("Создание Safe доступно для Decimal mainnet75 и testnet202020.");
            if (chain == 202020) await TestnetSafeProfile.VerifyAsync(client);
            foreach (var (address, expected) in chain == 75 ? new[] { (WeightedSafeDrafts.Implementation, WeightedSafeDrafts.ImplementationSha256), (WeightedSafeDrafts.Factory, WeightedSafeDrafts.FactorySha256) } : Array.Empty<(string, string)>())
            {
                var code = await client.Web3.Eth.GetCode.SendRequestAsync(address);
                if (string.IsNullOrEmpty(code) || !Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Код фабрики или реализации Safe отличается от проверенного профиля. Создание остановлено.");
            }
            var rows = (members.Text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(line => { var pair = line.Split(':'); if (pair.Length != 2) throw new ArgumentException("Формат владельца: адрес:вес"); return new SafeMember(pair[0].Trim(), BigInteger.Parse(pair[1].Trim())); }).ToArray();
            var quorum = BigInteger.Parse(threshold.Text ?? "0"); var draft = chain == 75 ? WeightedSafeDrafts.Create(owner, rows, quorum, BigInteger.Parse(salt.Text ?? "0"), chain) : TestnetSafeProfile.Create(owner, rows, quorum, BigInteger.Parse(salt.Text ?? "0"), chain);
            if (version != epoch || owner != Address || chain != network.ChainId) return;
            await Preview(draft, "Создание Decimal Safe: порог " + quorum + "; " + string.Join("; ", rows.Select(m => m.Owner + " вес " + m.Weight)), "Новый общий счет Safe", receiptSummary: receipt => "Создан Safe: " + (chain == 75 ? WeightedSafeDrafts.CreatedAddress(receipt) : TestnetSafeProfile.CreatedAddress(receipt)));
        }, true);
    }
}
