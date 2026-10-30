using System.Numerics;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DCWallet.Core;
using Decimal.Web3;

namespace DCWallet;
public sealed partial class MainWindow
{
    private bool historyLoading;
    private long historyGeneration;
    private Action? historyChanged;
    private Action? journalChanged;
    private readonly Dictionary<string, HistoryRow> manuallyVerifiedHistory = new(StringComparer.OrdinalIgnoreCase);
    private void ShowHistory()
    {
        if (!RequireWallet()) return;
        Reset("История", "Переводы и операции вашего кошелька."); SelectNav("История"); SetPageWidth(800);
        var key = network.ChainId + ":" + Address.ToLowerInvariant(); if (historyOwner != key) { historyData = null; historyOwner = key; manuallyVerifiedHistory.Clear(); }
        var summary = Text("История еще не загружена.", 13, Muted); page.Children.Add(summary);
        var search = Field("Поиск по хэшу, адресу, токену или операции"); search.Watermark = "Начните вводить...";
        var list = new StackPanel { Spacing = 12 }; page.Children.Add(list); var limit = 50;
        void Render()
        {
            list.Children.Clear();
            if (historyData is null) { summary.Text = historyLoading ? "Загружаем историю..." : "История еще не загружена."; return; }
            summary.Text = historyLoading ? $"Загружено операций: {historyData.Rows.Count}. Загрузка продолжается..." : $"Операций: {historyData.Rows.Count}";
            ToolTip.SetTip(summary, $"Записей в каталоге: {historyData.Total}. Требуют дополнительной проверки: {historyData.Failed}. " + (historyData.Complete ? "Загрузка завершена." : "История может дополняться при обновлении."));
            var query = search.Text?.Trim() ?? "";
            var filtered = historyData.Rows.Where(r => query.Length == 0 || (r.Hash + " " + r.From + " " + r.To + " " + r.Kind + " " + string.Join(' ', r.Movements?.Select(m => m.Symbol) ?? [])).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (var row in filtered.Take(limit))
            {
                var content = new StackPanel { Spacing = 7 };
                var rowTitle = Text(row.Kind + " · " + (row.Verification.StartsWith("indexer", StringComparison.Ordinal) ? "Данные архива" : HistorySafeOutcome.DisplayStatus(row)), 16);
                if (row.Verification != "rpc") ToolTip.SetTip(rowTitle, row.Verification.StartsWith("indexer", StringComparison.Ordinal) ? "Сведения из архива Decimal. Подтверждение через RPC пока недоступно." : "Квитанция подтверждена. Связь с кошельком указана архивом.");
                content.Children.Add(rowTitle); content.Children.Add(Text($"{row.Timestamp.LocalDateTime:g} · Блок {row.Block}", 12, Muted));
                foreach (var move in row.Movements ?? [])
                {
                    var direction = move.To.Equals(Address, StringComparison.OrdinalIgnoreCase) && !move.From.Equals(Address, StringComparison.OrdinalIgnoreCase) ? "+" : move.From.Equals(Address, StringComparison.OrdinalIgnoreCase) && !move.To.Equals(Address, StringComparison.OrdinalIgnoreCase) ? "-" : "";
                    var amount = Text(direction + (move.Decimals is { } decimals ? AmountDisplay.Compact(move.Raw, decimals) : move.Amount) + " " + move.Symbol + (move.TokenId is { } id ? " · NFT #" + id : ""), 15); ToolTip.SetTip(amount, "Точно: " + move.Amount + " " + move.Symbol); content.Children.Add(amount);
                }
                content.Children.Add(Text("От: " + row.From + "\nКому: " + row.To + "\nКомиссия отправителя: " + Units.Format(row.FeeWei) + " DEL", 12, Muted));
                if (row.Memo.Length > 0) content.Children.Add(Text("Сообщение: " + row.Memo, 13));

                var hash = MakeButton(row.Hash.Length > 25 ? row.Hash[..18] + "..." + row.Hash[^8..] : row.Hash, () => { OpenUrl("https://explorer.mintcandy.ru/#tx/" + row.Hash); return Task.CompletedTask; }); content.Children.Add(hash); list.Children.Add(Card(content, 14));
            }
            if (filtered.Length > limit) list.Children.Add(MakeButton("Показать следующие 50", () => { limit += 50; Render(); return Task.CompletedTask; }));
        }
        historyChanged = Render; search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { limit = 50; Render(); } }; Render();
        Button("Загрузить / обновить всю историю", () => { if (!historyLoading) _ = ReadFullHistoryAsync(); return Task.CompletedTask; }, true);
        Button("Остановить загрузку", () => { historyCancellation?.Cancel(); return Task.CompletedTask; });
        var knownHash = Field("Проверить известный хэш транзакции 0x...");
        Button("Проверить хэш через RPC", async () =>
        {
            var owner = Address; var version = epoch; var activeClient = client;
            var row = await new HistoryReader(activeClient).VerifyAsync(knownHash.Text?.Trim() ?? "", owner);
            if (version != epoch || owner != Address || !ReferenceEquals(client, activeClient)) return;
            if (row is null) throw new InvalidOperationException("RPC не подтвердил транзакцию этого адреса. Проверьте хэш и выбранную сеть.");
            manuallyVerifiedHistory[row.Hash] = row;
            var rows = (historyData?.Rows ?? []).Where(r => !r.Hash.Equals(row.Hash, StringComparison.OrdinalIgnoreCase)).Append(row).OrderByDescending(r => r.Block).ToArray();
            historyData = new(rows, historyData?.NextOffset ?? 0, historyData?.Failed ?? 0, false, Math.Max(rows.Length, historyData?.Total ?? 0), false, historyData?.Issues ?? ["Проверены известные хэши. Полнота истории не подтверждена."]); Render();
        });
        Button("Экспорт истории в CSV", async () =>
        {
            if (historyData is null) throw new InvalidOperationException("Сначала загрузите историю.");
            var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "dc-wallet-history.csv" }); if (destination is null) return;
            await using var stream = await destination.OpenWriteAsync(); stream.SetLength(0); var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(HistoryReader.Csv(historyData.Rows))).ToArray(); await stream.WriteAsync(bytes); status.Text = "История сохранена. CSV содержит отметки источника и проверки.";
        });
        if (Directory.Exists(JournalDirectory))
        {
            var pending = new StackPanel { Spacing = 8 };
            void ReadJournal()
            {
                pending.Children.Clear();
                foreach (var file in Directory.GetFiles(JournalDirectory, "*.json").Take(200))
                {
                    try
                    {
                        if (new FileInfo(file).Length > 16384 || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                        var entry = JsonSerializer.Deserialize<JournalEntry>(File.ReadAllText(file)); if (entry is null || entry.ChainId != network.ChainId || !entry.From.Equals(Address, StringComparison.OrdinalIgnoreCase)) continue;
                        var state = entry.State switch { "submitted" => "Отправлена, ожидает проверки", "sending" => "Отправка начата", "unknown-check-hash" => "Результат отправки неизвестен", "confirmed" => "Включена в блок", "reverted" => "Ошибка исполнения", _ => "Неизвестный статус" };
                        pending.Children.Add(Text(state + " · " + entry.Hash, 12, Muted));
                    }
                    catch { }
                }
            }
            journalChanged = ReadJournal; ReadJournal();
            if (pending.Children.Count > 0) page.Children.Add(new Expander { Header = "Локальный журнал отправок", Content = pending });
        }
        if (automaticLoading && historyData is null && !historyLoading && network.ChainId is 75 or 202020) Dispatcher.UIThread.Post(async () => await ReadFullHistoryAsync());
    }
    private async Task ReadFullHistoryAsync()
    {
        if (historyLoading) return; historyLoading = true; var generation = ++historyGeneration; historyCancellation?.Dispose(); historyCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(15)); var ct = historyCancellation.Token;
        var owner = Address; var version = epoch; var chain = network.ChainId; var activeClient = client;
        IReadOnlyList<HistoryRow> localRows = [];
        HistoryPage Merge(HistoryPage part)
        {
            var rows = part.Rows.Concat(localRows).Concat(manuallyVerifiedHistory.Values).GroupBy(r => r.Hash, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).OrderByDescending(r => r.Block).ToArray();
            return part with { Rows = rows, Total = Math.Max(rows.Length, part.Total) };
        }
        var progress = new Progress<HistoryPage>(part => { if (historyGeneration == generation && !ct.IsCancellationRequested && epoch == version && Address == owner && network.ChainId == chain) { historyData = Merge(part); historyChanged?.Invoke(); } });
        try
        {
            var journal = await new JournalReconciler(activeClient).ReconcileAsync(JournalDirectory, owner, ct); localRows = journal.Rows;
            if (historyGeneration == generation && epoch == version && Address == owner && network.ChainId == chain)
            {
                historyData = Merge(new([], 0, 0, false, 0, false, journal.Issues)); journalChanged?.Invoke(); historyChanged?.Invoke();
            }
            var result = await new HistoryReader(activeClient).ReadAllAsync(owner, progress, ct);
            if (historyGeneration == generation && epoch == version && Address == owner && network.ChainId == chain) historyData = Merge(result);
        }
        catch (OperationCanceledException) { if (historyGeneration == generation && epoch == version) status.Text = "Загрузка остановлена. Уже загруженные записи сохранены на экране."; }
        catch (Exception e) { if (historyGeneration == generation && epoch == version) status.Text = "Не удалось завершить историю: " + e.Message; }
        finally { if (historyGeneration == generation) { historyLoading = false; if (epoch == version) historyChanged?.Invoke(); } }
    }
}
