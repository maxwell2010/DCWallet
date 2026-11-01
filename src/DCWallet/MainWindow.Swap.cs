using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Staking;
using Decimal.Web3.Tokens;
using Decimal.Web3.TokenEconomy;
using Decimal.Web3.Transactions;
using Decimal.Web3.Nft;

namespace DCWallet;
public sealed partial class MainWindow
{
    private void ShowSwap()
    {
        if (!RequireWallet()) return;
        SelectNav("Обмен"); Reset("Обмен", "Покупка и продажа резервных токенов Decimal. Котировку кошелек получает из контракта.");
        BeginCard(); target.Children.Add(Text("Направление")); var direction = new ComboBox { ItemsSource = new[] { "DEL → Токен", "Токен → DEL", "Токен → Токен" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch }; target.Children.Add(direction);
        var token = Field("Контракт резервного токена"); token.Watermark = "0x..."; target.Children.Add(AttachTokenPicker(token)); var amount = Field("Сколько отдать"); amount.Watermark = "0.0";
        var tokenOut = Field("Получить резервный токен"); var outputPicker = AttachTokenPicker(tokenOut); target.Children.Add(outputPicker); ((Control)tokenOut.Parent!).IsVisible = outputPicker.IsVisible = false;
        var slippage = Field("Допустимое проскальзывание, %", "1");
        var quote = Text("Укажите токен и сумму, затем обновите котировку.", 13, Muted); target.Children.Add(quote);
        BigInteger quotedAmount = 0, quotedMinimum = 0; string quotedToken = "", quotedOutput = "", quotedCenter = ""; int quotedDecimals = 0;
        var ownerEpoch = epoch; TransactionDraft? swapDraft = null; string? quoteInputs = null, quoteSummary = null; DateTimeOffset quotedAt = default;
        string Inputs() => string.Join('|', direction.SelectedIndex, token.Text, tokenOut.Text, amount.Text, slippage.Text, Address, network.ChainId);
        direction.SelectionChanged += (_, _) => { swapDraft = null; Invalidate(); ((Control)tokenOut.Parent!).IsVisible = outputPicker.IsVisible = direction.SelectedIndex == 2; };
        Button("Обновить цену", async () =>
        {
            swapDraft = null; quoteInputs = null;
            var requestedInputs = Inputs(); var requestedDirection = direction.SelectedIndex; var requestedAmount = amount.Text; var requestedToken = token.Text?.Trim() ?? "";
            var quoteClient = client; var quoteOwner = Address; var quoteEpoch = epoch;
            var requestedOutput = tokenOut.Text?.Trim() ?? "";
            if (!decimal.TryParse(slippage.Text, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var percent) || percent < 0 || percent > 20) throw new ArgumentException("Проскальзывание должно быть от 0 до 20%.");
            var at = requestedDirection == 2 ? new Nethereum.RPC.Eth.DTOs.BlockParameter(await quoteClient.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()) : null;
            var tokenAddress = requestedToken; var metadata = await new Erc20Service(quoteClient, tokenAddress).GetMetadataAsync(at);
            var service = new ReservableTokenService(quoteClient, tokenAddress); var input = Units.Parse(requestedAmount ?? "", requestedDirection == 0 ? 18 : metadata.Decimals);
            if (input <= 0) throw new ArgumentException("Введите сумму больше нуля.");
            var outputDecimals = requestedDirection == 0 ? metadata.Decimals : 18; var outputSymbol = requestedDirection == 0 ? metadata.Symbol : "DEL";
            System.Numerics.BigInteger output;
            if (requestedDirection == 2)
            {
                if (requestedOutput.Equals(tokenAddress, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Выберите два разных токена.");
                var outputMeta = await new Erc20Service(quoteClient, requestedOutput).GetMetadataAsync(at);
                var intermediate = await service.CalculateSellOutputWeiAsync(input, at);
                output = await new ReservableTokenService(quoteClient, requestedOutput).CalculateBuyOutputRawAsync(intermediate, at);
                outputDecimals = outputMeta.Decimals; outputSymbol = outputMeta.Symbol;
            }
            else output = requestedDirection == 0 ? await service.CalculateBuyOutputRawAsync(input) : await service.CalculateSellOutputWeiAsync(input);
            if (output <= 0) throw new InvalidOperationException("Контракт вернул нулевую котировку.");
            var basisPoints = (int)(percent * 100); var minimum = output * (10000 - basisPoints) / 10000;
            if (requestedDirection == 2 && minimum <= 0) throw new InvalidOperationException("Сумма обмена слишком мала для ненулевого минимума получения.");
            var center = await quoteClient.ResolveSystemContractAsync("token-center"); var drafts = new TokenEconomyDrafts(center);
            if (requestedInputs != Inputs() || quoteEpoch != epoch || quoteOwner != Address || !ReferenceEquals(quoteClient, client)) throw new InvalidOperationException("Параметры изменились во время расчета. Обновите цену.");
            swapDraft = requestedDirection == 0 ? drafts.BuildBuyTokenForExactDelDraft(quoteOwner, tokenAddress, input, minimum, quoteOwner) : requestedDirection == 1 ? drafts.BuildSellExactTokensForDelDraft(quoteOwner, tokenAddress, input, minimum, quoteOwner) : drafts.BuildConvertDraft(quoteOwner, tokenAddress, requestedOutput, input, minimum, quoteOwner);
            quotedAmount = input; quotedMinimum = minimum; quotedToken = tokenAddress; quotedOutput = requestedOutput; quotedCenter = center; quotedDecimals = metadata.Decimals;
            quoteInputs = requestedInputs; quotedAt = DateTimeOffset.UtcNow;
            var exactQuote = $"Получите примерно {Units.Format(output, outputDecimals)} {outputSymbol}\nМинимум: {Units.Format(minimum, outputDecimals)}";
            quote.Text = $"Получите примерно {AmountDisplay.Compact(output, outputDecimals)} {outputSymbol}\nМинимум (≈): {AmountDisplay.Compact(minimum, outputDecimals)}"; ToolTip.SetTip(quote, exactQuote);
            quoteSummary = "Отдаете: " + requestedAmount + " " + (requestedDirection == 0 ? "DEL" : metadata.Symbol) + "\n" + exactQuote; SetConnection(true); status.Text = "Котировка обновлена. Перед отправкой потребуется подтверждение.";
        }); EndCard();
        var approveConvert = Button("Разрешить точную сумму конвертации", async () =>
        {
            if (direction.SelectedIndex != 2) throw new InvalidOperationException("Разрешение требуется только для конвертации токенов.");
            var fingerprint = Inputs(); var active = client; var owner = Address; var current = epoch;
            var service = new Erc20Service(active, token.Text?.Trim() ?? ""); var raw = Units.Parse(amount.Text ?? "", await service.GetDecimalsAsync());
            if (raw <= 0) throw new ArgumentException("Введите положительную сумму.");
            var center = await active.ResolveSystemContractAsync("token-center");
            async Task ValidateApproval()
            {
                if (fingerprint != Inputs() || current != epoch || owner != Address || !ReferenceEquals(active, client)) throw new InvalidOperationException("Параметры изменились.");
                if (await service.GetAllowanceRawAsync(owner, center) != 0) throw new InvalidOperationException("Сначала явно отзовите прежнее разрешение, затем разрешите точную сумму.");
                if (fingerprint != Inputs() || current != epoch || owner != Address || !ReferenceEquals(active, client)) throw new InvalidOperationException("Параметры изменились.");
            }
            await ValidateApproval();
            await Preview(service.BuildApproveDraft(owner, center, raw), "Разрешение Token Center на точную сумму конвертации", amount.Text + " токенов", beforeSend: ValidateApproval);
        }); approveConvert.IsVisible = false;
        var revokeConvert = Button("Отозвать прежнее разрешение конвертации", async () =>
        {
            if (direction.SelectedIndex != 2) throw new InvalidOperationException("Отзыв доступен только для конвертации токенов.");
            var fingerprint = Inputs(); var active = client; var owner = Address; var current = epoch;
            var service = new Erc20Service(active, token.Text?.Trim() ?? ""); var center = await active.ResolveSystemContractAsync("token-center");
            if (await service.GetAllowanceRawAsync(owner, center) == 0) throw new InvalidOperationException("Разрешение уже равно нулю.");
            if (fingerprint != Inputs() || current != epoch || owner != Address || !ReferenceEquals(active, client)) throw new InvalidOperationException("Параметры изменились.");
            await Preview(service.BuildApproveDraft(owner, center, BigInteger.Zero), "Отзыв разрешения Token Center", "Разрешение станет равным нулю.");
        }); revokeConvert.IsVisible = false;
        direction.SelectionChanged += (_, _) => approveConvert.IsVisible = revokeConvert.IsVisible = direction.SelectedIndex == 2;
        Button("Продолжить обмен", async () =>
        {
            if (swapDraft is null || quoteInputs != Inputs() || epoch != ownerEpoch || DateTimeOffset.UtcNow - quotedAt > TimeSpan.FromSeconds(45)) throw new InvalidOperationException("Обновите цену для выбранных параметров.");
            if (direction.SelectedIndex != 2) { await Preview(swapDraft, "Обмен резервного токена", quoteSummary); return; }
            var fingerprint = Inputs(); var selected = swapDraft; var active = client; var owner = Address; var current = epoch;
            var tokenAddress = quotedToken; var outputAddress = quotedOutput; var center = quotedCenter; var raw = quotedAmount; var minimum = quotedMinimum; var decimals = quotedDecimals;
            var service = new Erc20Service(active, tokenAddress);
            void Current()
            {
                if (fingerprint != Inputs() || current != epoch || owner != Address || !ReferenceEquals(active, client) || !ReferenceEquals(selected, swapDraft) || DateTimeOffset.UtcNow - quotedAt > TimeSpan.FromSeconds(45))
                    throw new InvalidOperationException("Параметры или котировка изменились. Обновите цену.");
            }
            async Task ValidatePrice()
            {
                Current();
                if (!center.Equals(await active.ResolveSystemContractAsync("token-center"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Контракт обмена изменился.");
                var at = new Nethereum.RPC.Eth.DTOs.BlockParameter(await active.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync());
                if (await service.GetDecimalsAsync(at) != decimals || await service.GetBalanceRawAsync(owner, at) < raw) throw new InvalidOperationException("Проверьте баланс и точность токена.");
                var intermediate = await new ReservableTokenService(active, tokenAddress).CalculateSellOutputWeiAsync(raw, at);
                var output = await new ReservableTokenService(active, outputAddress).CalculateBuyOutputRawAsync(intermediate, at);
                if (output < minimum) throw new InvalidOperationException("Цена ухудшилась ниже показанного минимума. Обновите цену.");
                Current();
            }
            await ValidatePrice(); var allowance = await service.GetAllowanceRawAsync(owner, center); Current();
            if (allowance > 0 && allowance < raw) throw new InvalidOperationException("Сначала явно отзовите прежнее разрешение, затем повторите расчет.");
            async Task ValidateAllowance()
            {
                await ValidatePrice(); var fresh = await service.GetAllowanceRawAsync(owner, center); Current();
                if (allowance == 0 ? fresh != 0 : fresh < raw) throw new InvalidOperationException("Разрешение изменилось. Подготовьте обмен заново.");
            }
            if (allowance >= raw) { await Preview(selected, "Обмен резервного токена", quoteSummary, beforeSend: ValidateAllowance); return; }
            await PreviewWithPermit(tokenAddress, center, raw, "Обмен токенов через Permit", quoteSummary ?? "",
                signature => new TokenEconomyDrafts(center).BuildConvertWithPermitDraft(owner, tokenAddress, outputAddress, raw, minimum, owner, signature.Deadline, signature.V, signature.R, signature.S),
                ValidateAllowance, ShowSwap);
        }, true);
        Button("Другие операции обмена", () => { ShowOperations("TokenEconomy"); return Task.CompletedTask; });
    }
}
