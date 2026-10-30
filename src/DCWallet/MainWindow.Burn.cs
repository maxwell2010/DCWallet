using System.Numerics;
using Avalonia.Controls;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Tokens;
using Decimal.Web3.Transactions;
namespace DCWallet;
public sealed partial class MainWindow
{
    private void ShowBridge()
    {
        if (!RequireWallet()) return;
        Reset("Мост", "Межсетевые переводы временно недоступны."); SelectNav("Мост");
        page.Children.Add(Card(Text("Поддержка моста готовится. Отправка и завершение межсетевых переводов пока отключены.")));
        Back(ShowAssets);
    }
    private void ShowBurn()
    {
        if (!RequireWallet()) return;
        Reset("Сжигание", "Сожженные средства невозможно восстановить."); SelectNav("Отправить");
        Segments(("Одному получателю", ShowSend), ("Нескольким сразу", ShowMultisend), ("Сжигание", ShowBurn));
        if (signer is null) { page.Children.Add(Text("Для сжигания откройте кошелек с ключом.")); return; }
        BeginCard(); var tokens = tokenData?.Items.ToArray() ?? [];
        target.Children.Add(Text("Монета"));
        var asset = new ComboBox { ItemsSource = new[] { "DEL" }.Concat(tokens.Select(t => t + " · " + t.Contract[..8] + "..." + t.Contract[^4..])).ToArray(), SelectedIndex = 0, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch }; target.Children.Add(asset);
        var amount = Field("Количество для сжигания"); amount.Watermark = "0.0";
        var confirm = new CheckBox { Content = "Я понимаю, что сжигание необратимо" }; target.Children.Add(confirm); EndCard();
        asset.SelectionChanged += (_, _) => Invalidate(); amount.TextChanged += (_, _) => Invalidate(); confirm.IsCheckedChanged += (_, _) => Invalidate();
        Button("Продолжить", async () =>
        {
            if (confirm.IsChecked != true) throw new InvalidOperationException("Подтвердите необратимость сжигания.");
            var current = client; var owner = Address; var generation = epoch; TransactionDraft draft; string symbol;
            if (asset.SelectedIndex == 0)
            {
                var raw = Units.Parse(amount.Text ?? ""); if (raw <= 0 || raw > await current.GetBalanceWeiAsync(owner)) throw new InvalidOperationException("Проверьте количество DEL.");
                draft = TransactionDraft.BurnDel(owner, amount.Text ?? ""); symbol = "DEL";
            }
            else
            {
                if (asset.SelectedIndex < 1 || asset.SelectedIndex > tokens.Length) throw new InvalidOperationException("Выберите токен.");
                var token = tokens[asset.SelectedIndex - 1]; var raw = Units.Parse(amount.Text ?? "", token.Decimals);
                if (raw <= 0) throw new InvalidOperationException("Количество должно быть больше нуля.");
                draft = await new ReservelessTokenService(current, token.Contract).BuildBurnCheckedAsync(owner, raw); symbol = token.Symbol;
            }
            if (epoch != generation || owner != Address || !ReferenceEquals(current, client)) return;
            await Preview(draft, "Сжигание " + symbol, "Безвозвратно: " + amount.Text + " " + symbol);
        }, true);
    }
}
