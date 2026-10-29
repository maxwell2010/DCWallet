using System.Numerics;
using Decimal.Web3;
using Decimal.Web3.Tokens;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
namespace DCWallet.Core;
public sealed record TokenDelEstimate(BigInteger Block, BigInteger Wei);
public sealed class TokenDelValuation(DecimalClient client)
{
    public async Task<TokenDelEstimate> ReadAsync(TokenBalance holding, BigInteger block, CancellationToken ct = default)
    {
        if (holding.Raw <= 0 || block < 0) throw new ArgumentOutOfRangeException(nameof(holding));
        if ((await client.Web3.Eth.ChainId.SendRequestAsync().WaitAsync(ct)).Value != client.Network.ChainId) throw new InvalidOperationException("RPC другой сети.");
        var at = new BlockParameter(new HexBigInteger(block));
        var before = await client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(at).WaitAsync(ct) ?? throw new InvalidOperationException("Блок недоступен.");
        if (before.Number?.Value != block) throw new InvalidOperationException("RPC вернул другой блок.");
        var reserve = new ReservableTokenService(client, holding.Contract);
        var crr = await reserve.GetCrrAsync(at).WaitAsync(ct); if (crr < 1 || crr > 100) throw new NotSupportedException("Нет резервной оценки.");
        var available = await reserve.GetReserveWeiAsync(at).WaitAsync(ct); if (available <= 0) throw new NotSupportedException("Нет резерва DEL.");
        var quote = await reserve.CalculateSellOutputWeiAsync(holding.Raw, at).WaitAsync(ct); if (quote < 0 || quote > available) throw new InvalidDataException("Некорректная котировка.");
        var after = await client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(at).WaitAsync(ct);
        if (after?.BlockHash != before.BlockHash || string.IsNullOrEmpty(before.BlockHash)) throw new InvalidOperationException("Блок изменился во время оценки.");
        return new(block, quote);
    }
}
