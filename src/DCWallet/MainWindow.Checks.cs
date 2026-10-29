using System.Numerics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Checks;
using Decimal.Web3.Tokens;
using Decimal.Web3.Wallet;
using Nethereum.RPC.Eth.DTOs;
namespace DCWallet;
public sealed partial class MainWindow
{
    private void ShowChecks()
    {
        if(!RequireWallet())return;
        Reset("Чеки", "Создайте зашифрованный файл чека и передайте его получателю. Пароль чека передайте отдельно. Для погашения получателю нужен DEL на комиссию.");
        SelectNav("Чеки");
        Back(()=>ShowOperations());
        var contract=Field("Контракт Checks",network.ChainId==202020?ChecksDeploymentProfile.TestnetContract:network.ChainId==75?ChecksDeploymentProfile.MainnetContract:"");
        var asset=new ComboBox{ItemsSource=new[]{"DEL","ERC20","ERC20 с permit"},SelectedIndex=0};page.Children.Add(asset);
        var token=Field("Контракт ERC20 (только для токенового чека)");var tokenPicker=AttachTokenPicker(token);page.Children.Add(tokenPicker);
        var amount=Field("Сумма чека");var due=Field("Последний блок погашения (номер блока, не дата)");
        var checkPassword=Field("Отдельный пароль файла чека (12+ символов)",secret:true);
        var checkPasswordAgain=Field("Повторите пароль файла чека",secret:true);
        var walletPassword=Field("Пароль кошелька (только для permit)",secret:true);
        void UpdateAssetFields(){var tokenMode=asset.SelectedIndex>0;((Control)token.Parent!).IsVisible=tokenMode;tokenPicker.IsVisible=tokenMode;((Control)walletPassword.Parent!).IsVisible=asset.SelectedIndex==2;if(asset.SelectedIndex!=2)walletPassword.Text="";}
        asset.SelectionChanged+=(_,_)=>UpdateAssetFields();UpdateAssetFields();
        ToolTip.SetTip(contract,"Профиль mainnet использует проверенный runtime напрямую; testnet использует отдельный прокси. Код и chain ID проверяются при каждой подготовке.");
        ToolTip.SetTip(due,"Срок зависит от высоты блоков сети. После срока контракт может отклонить погашение. Не используйте время Unix.");
        var owner=Address;var version=epoch;var activeClient=client;var chain=network.ChainId;
        void Current(){if(epoch!=version||Address!=owner||client!=activeClient)throw new InvalidOperationException("Кошелек или сеть изменились.");}
        string Fingerprint()=>string.Join("\n",contract.Text,asset.SelectedIndex,token.Text,amount.Text,due.Text);
        void Unchanged(string fingerprint){Current();if(Fingerprint()!=fingerprint)throw new InvalidOperationException("Параметры чека изменились. Повторите подготовку.");}
        async Task<(ChecksService Service,string Contract)> Service(string contractInput)
        {Current();var address=WalletDataReaderAddress(contractInput);await ChecksDeploymentProfile.VerifyAsync(activeClient,address);Current();return(new ChecksService(activeClient.Web3,address),address);}
        var approveToken=Button("Разрешить точную сумму токенового чека",async()=>
        {var fingerprint=Fingerprint();var selected=asset.SelectedIndex;var contractInput=contract.Text??"";var tokenInput=token.Text;var amountInput=amount.Text;if(selected!=1)throw new InvalidOperationException("Разрешение требуется только для ERC20 без permit.");var svc=await Service(contractInput);var erc=new Erc20Service(activeClient,WalletDataReaderAddress(tokenInput));var metadata=await erc.GetMetadataAsync();var raw=Units.Parse(amountInput??"",metadata.Decimals);if(raw<=0)throw new ArgumentException("Введите положительную сумму.");Unchanged(fingerprint);await Preview(erc.BuildApproveDraft(owner,svc.Contract,raw),"Точное разрешение контракту Checks",Units.Format(raw,metadata.Decimals)+" "+metadata.Symbol);});
        approveToken.IsVisible=asset.SelectedIndex==1;asset.SelectionChanged+=(_,_)=>approveToken.IsVisible=asset.SelectedIndex==1;
        Button("Сохранить чек и подготовить создание",async()=>
        {
            if(signer is null)throw new InvalidOperationException("Для создания чека откройте кошелек с ключом.");
            var fingerprint=Fingerprint();var selected=asset.SelectedIndex;var contractInput=contract.Text??"";var tokenInput=token.Text;var amountInput=amount.Text;var dueInput=due.Text;var passwordInput=checkPassword.Text??"";var passwordAgain=checkPasswordAgain.Text;var walletPasswordInput=walletPassword.Text??"";
            var svc=await Service(contractInput);var tokenAddress=selected==0?"DEL":WalletDataReaderAddress(tokenInput);
            var metadata=selected==0?null:await new Erc20Service(activeClient,tokenAddress).GetMetadataAsync();var decimals=metadata?.Decimals??18;var symbol=metadata?.Symbol??"DEL";
            var raw=Units.Parse(amountInput??"",decimals);if(raw<=0)throw new ArgumentException("Введите положительную сумму.");var dueBlock=BigInteger.Parse(dueInput??"");var block=(await activeClient.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;if(dueBlock<=block)throw new ArgumentException("Срок погашения должен быть позже текущего блока.");
            if(passwordInput!=passwordAgain)throw new ArgumentException("Пароли файла чека не совпадают.");
            using var bearer=new CheckSecret();var nonce=await CheckLifecycle.GetNonceForAsync(activeClient,svc.Contract,owner);
            Decimal.Web3.Transactions.TransactionDraft draft;
            if(selected==0)draft=svc.Service.BuildCreateDelDraft(owner,new[]{bearer.Address},raw,dueBlock,nonce);
            else if(selected==1){var erc=new Erc20Service(activeClient,tokenAddress);if(await erc.GetAllowanceRawAsync(owner,svc.Contract)<raw)throw new InvalidOperationException("Сначала разрешите точную сумму.");draft=svc.Service.BuildCreateTokenDraft(owner,new[]{bearer.Address},raw,dueBlock,nonce,tokenAddress);}
            else
            {
                if(signer is null)throw new InvalidOperationException("Для permit нужен кошелек с ключом.");var credential=await Vault.OpenCredentialAsync(VaultPath,walletPasswordInput);walletPassword.Text="";
                try{using var temporary=CreateCredentialSigner(credential);Current();if(!temporary.Address.Equals(owner,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Другой подписант.");var permit=await temporary.SignPermitAsync(activeClient,tokenAddress,svc.Contract,raw,DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());draft=svc.Service.BuildCreateTokenByPermitDraft(owner,new[]{bearer.Address},raw,dueBlock,nonce,tokenAddress,new ChecksPermit(permit.Deadline,permit.V,permit.R,permit.S));}finally{credential=new("Mnemonic","");}
            }
            Current();var simulated=await activeClient.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=owner,To=svc.Contract,Data=draft.Data,Value=new Nethereum.Hex.HexTypes.HexBigInteger(draft.ValueWei)});
            var bytes=Convert.FromHexString(simulated[2..]);if(bytes.Length!=96||new BigInteger(bytes[..32],true,true)!=32||new BigInteger(bytes[32..64],true,true)!=1)throw new InvalidDataException("Контракт не вернул единственный hash чека.");
            var bearerAddress=bearer.Address;var hash="0x"+Convert.ToHexString(bytes[64..96]).ToLowerInvariant();var info=new ChecksPackageInfo(chain.ToString(),svc.Contract,hash,bearer.Address,tokenAddress,raw.ToString(),decimals,dueBlock.ToString());var package=ChecksPackage.Export(bearer,info,passwordInput);
            var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{SuggestedFileName="decimal-check.dccheck",DefaultExtension="dccheck"});if(file is null)return;
            await using(var stream=await file.OpenWriteAsync()){stream.SetLength(0);await stream.WriteAsync(package);}Current();
            if(await CheckLifecycle.GetNonceForAsync(activeClient,svc.Contract,owner)!=nonce)throw new InvalidOperationException("Nonce создателя изменился. Файл не активирован, подготовьте новый чек.");
            Unchanged(fingerprint);checkPassword.Text=checkPasswordAgain.Text="";await Preview(draft,"Создание чека. Файл сохранен, но активируется только после успешной транзакции. Перед передачей сверьте CreatedCheck hash: "+hash,Units.Format(raw,decimals)+" "+symbol+"; до блока "+dueBlock,receiptVerifier:receipt=>{if(!CheckLifecycle.CreatedHash(receipt,svc.Contract,bearerAddress).SequenceEqual(bytes[64..96]))throw new InvalidOperationException("CreatedCheck hash не совпал с сохраненным файлом. Чек не подтвержден.");},receiptSummary:receipt=>"Чек создан и файл активирован: 0x"+Convert.ToHexString(CheckLifecycle.CreatedHash(receipt,svc.Contract,bearerAddress)).ToLowerInvariant());
        },true);
        page.Children.Add(Text("Погашение полученного чека",20));
        var redeemPassword=Field("Пароль полученного файла чека",secret:true);
        Button("Открыть файл и подготовить погашение",async()=>
        {if(signer is null)throw new InvalidOperationException("Для погашения чека откройте кошелек с ключом.");var fingerprint=Fingerprint();var contractInput=contract.Text??"";var passwordInput=redeemPassword.Text??"";var svc=await Service(contractInput);var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{AllowMultiple=false,Title="Полученный чек"});if(files.Count==0)return;await using var stream=await files[0].OpenReadAsync();var buffer=new byte[ChecksPackage.MaxBytes+1];var count=await stream.ReadAtLeastAsync(buffer,buffer.Length,false);var opened=ChecksPackage.Open(buffer[..count],passwordInput,chain,svc.Contract);using var bearer=opened.Secret;redeemPassword.Text="";Current();if((await activeClient.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value>BigInteger.Parse(opened.Info.DueBlock))throw new InvalidOperationException("Срок чека истек.");var hash=Convert.FromHexString(opened.Info.Hash[2..]);var draft=svc.Service.BuildRedeemDraft(owner,new[]{bearer.SignRedeem(hash,owner)},new[]{hash});Unchanged(fingerprint);await Preview(draft,"Погашение чека "+opened.Info.Hash+". Сумма из файла, исполнение проверяется контрактом.",Units.Format(BigInteger.Parse(opened.Info.AmountRaw),opened.Info.Decimals)+" "+opened.Info.Token,receiptVerifier:receipt=>{if(CheckLifecycle.RedeemStatus(receipt,svc.Contract,hash)!=1)throw new InvalidOperationException("Контракт не подтвердил успешное погашение этого чека.");},receiptSummary:_=>"Чек погашен: "+opened.Info.Hash);});
    }
}
