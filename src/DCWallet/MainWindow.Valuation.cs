using System.Collections.Concurrent;
using Avalonia.Controls;
using DCWallet.Core;
using Decimal.Web3;
namespace DCWallet;
public sealed partial class MainWindow
{
    private CancellationTokenSource? valuationCancellation;
    private readonly Dictionary<string, TokenDelEstimate?> tokenValues = new(StringComparer.OrdinalIgnoreCase);
    private async Task ReadTokenValuesAsync(DataSet<TokenBalance> snapshot)
    {
        valuationCancellation?.Cancel(); valuationCancellation?.Dispose(); valuationCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var ct = valuationCancellation.Token;
        var owner = Address; var version = epoch; var activeClient = client;
        var results = new ConcurrentDictionary<string, TokenDelEstimate?>();
        try
        {
            await Parallel.ForEachAsync(snapshot.Items, new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = ct }, async (holding, token) =>
            {
                try { results[holding.Contract] = await new TokenDelValuation(activeClient).ReadAsync(holding, snapshot.Block, token); }
                catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { results[holding.Contract] = null; }
            });
            if (ct.IsCancellationRequested || owner != Address || version != epoch || !ReferenceEquals(activeClient, client) || !ReferenceEquals(snapshot, tokenData)) return;
            tokenValues.Clear(); foreach (var result in results) tokenValues[result.Key] = result.Value; NotifyPortfolio();
        }
        catch (OperationCanceledException) { }
        catch { /* Valuation is optional and never hides a verified token balance. */ }
    }
    private TextBlock TokenValuationLine(TokenBalance holding)
    {
        var known = tokenValues.TryGetValue(holding.Contract, out var estimate) && estimate?.Block == tokenData?.Block;
        var value = Text(known ? "≈ " + AmountDisplay.Compact(estimate!.Wei) + " DEL" : tokenValues.ContainsKey(holding.Contract) ? "Нет котировки в DEL" : "Оценка в DEL...", 13, Muted);
        ToolTip.SetTip(value, known ? "Оценка продажи всего баланса в блоке " + estimate!.Block + ": " + Units.Format(estimate.Wei) + " DEL. Без комиссии. Цена может измениться." : "Для оценки нужен резервный токен с доступной котировкой продажи. Безрезервные токены могут не иметь оценки в DEL.");
        return value;
    }
}
