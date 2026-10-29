using System.Numerics;
using Avalonia.Controls;
using Avalonia.Layout;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisend;
using Decimal.Web3.Tokens;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Avalonia.Platform.Storage;
using System.Text;

namespace DCWallet;
public sealed partial class MainWindow
{
    private void ShowMultisend()
    {
        if (!RequireWallet()) return;
        Reset("Несколько получателей", "Одна транзакция для выбранных получателей. Суммы и список будут показаны перед подтверждением."); Back(ShowSend);
        if (signer is null) { page.Children.Add(Text("Для рассылки откройте кошелек с ключом.")); return; }
        var asset = new ComboBox { ItemsSource = new[] { "DEL", "ERC20" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch }; page.Children.Add(asset);
        var token = Field("Контракт ERC20"); var picker = AttachTokenPicker(token); target.Children.Add(picker);
        var aggregate = Field("Контракт DEL-рассылки", network.ChainId == 75 ? "0x7b23EB47587cA6482FC16cB3D9D426EC64D4B5FC" : "");
        var aggregateGroup=(Control)aggregate.Parent!;page.Children.Remove(aggregateGroup);
        var advanced=new Expander {Header="Дополнительные настройки DEL-рассылки",Content=aggregateGroup,HorizontalAlignment=HorizontalAlignment.Stretch};page.Children.Add(advanced);
        var list = new StackPanel { Spacing = 10 }; page.Children.Add(list); var entries = new List<(TextBox Address, TextBox Amount)>();
        void Add(string? initialAddress=null,string? initialAmount=null)
        {
            if (entries.Count >= 500) throw new InvalidOperationException("Не более 500 получателей.");
            var address = new TextBox { Watermark = "Получатель 0x...",Text=initialAddress }; var amount = new TextBox { Watermark = "Сумма",Text=initialAmount }; var entry = (address, amount); entries.Add(entry);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*,Auto"), ColumnSpacing = 8 }; row.Children.Add(address); Grid.SetColumn(amount, 1); row.Children.Add(amount);
            var remove = MakeButton("Убрать", () => { entries.Remove(entry); list.Children.Remove(row); Invalidate(); return Task.CompletedTask; }); Grid.SetColumn(remove, 2); row.Children.Add(remove); list.Children.Add(row);
            address.TextChanged += (_, _) => Invalidate(); amount.TextChanged += (_, _) => Invalidate(); Invalidate();
        }
        Add(); Add(); Button("Добавить получателя", () => { Add(); return Task.CompletedTask; });
        PayoutImport? imported=null;
        var mode=new ComboBox{ItemsSource=new[]{"Заменить текущую таблицу","Добавить к текущей таблице"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};page.Children.Add(mode);
        var importInfo=Text("CSV/TSV/XLSX: адрес и сумма, до 500 строк и 256 КБ. В XLSX читается первый лист; длинные суммы храните текстом.",12,Muted);page.Children.Add(importInfo);
        var preview=new TextBox{IsReadOnly=true,AcceptsReturn=true,MinHeight=90,MaxHeight=180,IsVisible=false};page.Children.Add(preview);
        Avalonia.Controls.Button? apply=null; apply=MakeButton("Применить загруженную таблицу",()=>
        {
            if(imported is not{Valid:true})throw new InvalidOperationException("Исправьте ошибки таблицы перед применением.");
            var empty=entries.All(e=>string.IsNullOrWhiteSpace(e.Address.Text)&&string.IsNullOrWhiteSpace(e.Amount.Text));
            if(mode.SelectedIndex==1&&!empty&&entries.Count+imported.Rows.Count>500)throw new InvalidOperationException("После добавления будет больше 500 строк. Выберите замену таблицы.");
            if(mode.SelectedIndex==0||empty){entries.Clear();list.Children.Clear();}
            foreach(var row in imported.Rows)Add(row.Address,row.Amount);
            imported=null;preview.IsVisible=false;apply!.IsEnabled=false;status.Text="Таблица получателей применена.";return Task.CompletedTask;
        });apply.IsEnabled=false;
        Button("Загрузить таблицу",async()=>
        {
            var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title="Таблица получателей",AllowMultiple=false,FileTypeFilter=new[]{new FilePickerFileType("Таблицы"){Patterns=new[]{"*.csv","*.tsv","*.xlsx"}}}});if(files.Count==0)return;
            await using var stream=await files[0].OpenReadAsync();var buffer=new byte[PayoutTable.MaxBytes+1];var count=await stream.ReadAtLeastAsync(buffer,buffer.Length,false);
            imported=PayoutTable.Read(buffer[..count],files[0].Name);apply.IsEnabled=imported.Valid;preview.IsVisible=true;
            preview.Text=imported.Valid?$"Проверено строк: {imported.Rows.Count}. Нажмите «Применить» для изменения таблицы.\n"+string.Join('\n',imported.Rows.Take(10).Select(r=>r.Address+" · "+r.Amount)):"Таблица не применена.\n"+string.Join('\n',imported.Errors.Take(20));
        });page.Children.Add(apply);
        Button("Сохранить таблицу CSV/XLSX",async()=>
        {
            var parsed=PayoutTable.Validate(entries.Select(e=>new[]{e.Address.Text??"",e.Amount.Text??""}));if(!parsed.Valid)throw new InvalidOperationException(string.Join('\n',parsed.Errors));
            var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{SuggestedFileName="dc-wallet-recipients.xlsx",DefaultExtension="xlsx",FileTypeChoices=new[]{new FilePickerFileType("Excel XLSX"){Patterns=new[]{"*.xlsx"}},new FilePickerFileType("CSV"){Patterns=new[]{"*.csv"}}}});if(file is null)return;
            await using var stream=await file.OpenWriteAsync();stream.SetLength(0);var bytes=file.Name.EndsWith(".xlsx",StringComparison.OrdinalIgnoreCase)?PayoutTable.Xlsx(parsed.Rows):new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(PayoutTable.Csv(parsed.Rows))).ToArray();await stream.WriteAsync(bytes);status.Text="Таблица сохранена без округления сумм.";
        });
        var memo = Field("Сообщение (необязательно)");
        var permit = new CheckBox { Content = "Подписать EIP-2612 permit для этой рассылки (если токен поддерживает)" }; page.Children.Add(permit);
        var password = Field("Пароль кошелька для permit", secret: true);
        Avalonia.Controls.Button? approval=null;
        void Visibility()
        {
            ((Control)token.Parent!).IsVisible = picker.IsVisible = permit.IsVisible = asset.SelectedIndex == 1;
            advanced.IsVisible = asset.SelectedIndex == 0;
            if(approval is not null)approval.IsVisible=asset.SelectedIndex==1;
            ((Control)password.Parent!).IsVisible = asset.SelectedIndex == 1 && permit.IsChecked == true; Invalidate();
        }
        asset.SelectionChanged += (_, _) => Visibility(); permit.IsCheckedChanged += (_, _) => Visibility(); Visibility();
        async Task<(Erc20Service Service, IReadOnlyList<Erc20Recipient> Recipients, BigInteger Total, string Symbol)> TokenInputs()
        {
            var service = new Erc20Service(client, token.Text?.Trim() ?? ""); var metadata = await service.GetMetadataAsync();
            var recipients = entries.Select(e => new Erc20Recipient(e.Address.Text?.Trim() ?? "", Units.Parse(e.Amount.Text ?? "", metadata.Decimals))).ToArray();
            var total = recipients.Aggregate(BigInteger.Zero, (value, recipient) => value + recipient.AmountRaw);
            if (total <= 0 || recipients.Any(r => r.AmountRaw <= 0)) throw new ArgumentException("Введите положительные суммы для каждого получателя.");
            return (service, recipients, total, metadata.Symbol);
        }
        approval=Button("Разрешить точную сумму ERC20-рассылки", async () =>
        {
            if (asset.SelectedIndex != 1) throw new InvalidOperationException("Для DEL разрешение не требуется.");
            var inputs = await TokenInputs(); var deployment = SafeMultisendDeployment.ForNetwork(network); var service = new CandySafeMultisend(client); await service.VerifyDeploymentAsync();
            await Preview(inputs.Service.BuildApproveDraft(Address, deployment.Address, inputs.Total), "Разрешение проверенному контракту рассылки. После исполнения вернитесь к рассылке.", Units.Format(inputs.Total, await inputs.Service.GetDecimalsAsync()) + " " + inputs.Symbol);
        });
        approval.IsVisible=asset.SelectedIndex==1;
        Button("Подготовить рассылку", async () =>
        {
            if (entries.Count == 0) throw new ArgumentException("Добавьте хотя бы одного получателя.");
            TransactionDraft draft; string summary;
            if (asset.SelectedIndex == 0)
            {
                var recipients = entries.Select(e => new DelRecipient(e.Address.Text?.Trim() ?? "", e.Amount.Text ?? "")).ToArray();
                if (recipients.Any(r => Units.Parse(r.AmountDel) <= 0)) throw new ArgumentException("Суммы должны быть положительными.");
                if (recipients.Length > 1)
                {
                    if ((await client.Web3.Eth.ChainId.SendRequestAsync()).Value != network.ChainId) throw new InvalidOperationException("Другая сеть RPC.");
                    var code = await client.Web3.Eth.GetCode.SendRequestAsync(aggregate.Text?.Trim() ?? ""); if (string.IsNullOrEmpty(code) || code == "0x") throw new InvalidOperationException("Контракт рассылки не найден в выбранной сети.");
                }
                draft = MultisendDel.Build(Address, recipients, memo.Text, aggregate.Text?.Trim()); summary = Units.Format(draft.ValueWei) + " DEL";
            }
            else
            {
                var inputs = await TokenInputs(); var service = new CandySafeMultisend(client); await service.VerifyDeploymentAsync();
                if (inputs.Total > await inputs.Service.GetBalanceRawAsync(Address)) throw new InvalidOperationException("Недостаточно токенов.");
                if (permit.IsChecked == true)
                {
                    var owner = Address; var version = epoch; var phrase = await Vault.OpenCredentialAsync(VaultPath, password.Text ?? ""); password.Text = "";
                    try
                    {
                        using var temporarySigner = CreateCredentialSigner(phrase);
                        if (version != epoch || !temporarySigner.Address.Equals(owner, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Кошелек изменился.");
                        var signature = await temporarySigner.SignPermitAsync(client, inputs.Service.TokenAddress,SafeMultisendDeployment.ForNetwork(network).Address, inputs.Total, DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());
                        if (version != epoch) throw new InvalidOperationException("Кошелек заблокирован.");
                        draft = service.BuildTokenWithPermitDraft(Address, inputs.Service.TokenAddress, inputs.Recipients, signature, memo.Text);
                    }
                    finally { phrase = new("Mnemonic",""); }
                }
                else
                {
                    if (inputs.Total > await inputs.Service.GetAllowanceRawAsync(Address, SafeMultisendDeployment.ForNetwork(network).Address)) throw new InvalidOperationException("Сначала разрешите точную сумму или выберите permit.");
                    draft = service.BuildTokenDraft(Address, inputs.Service.TokenAddress, inputs.Recipients, memo.Text);
                }
                summary = Units.Format(inputs.Total, await inputs.Service.GetDecimalsAsync()) + " " + inputs.Symbol;
            }
            summary += "\n" + string.Join('\n', entries.Select(e => e.Address.Text + " · " + e.Amount.Text)); await Preview(draft, "Рассылка: получателей " + entries.Count, summary);
        }, true);
    }
}
