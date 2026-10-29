using System.Numerics;
using Avalonia.Controls;
using DCWallet.Core;
using Decimal.Web3.Nft;
using Decimal.Web3.Staking;
namespace DCWallet;
public sealed partial class MainWindow
{
    private void ShowCombinedNftWorkspace()
        => ShowCombinedNftWorkspaceFor(null);
    private void ShowCombinedNftWorkspaceFor(WalletStake? preset)
    {
        if(!RequireWallet())return;
        Reset("NFT-стейкинг testnet", "Управление ERC721/1155 через актуальный DecimalDelegation. Данные можно указать вручную, даже когда каталог NFT недоступен."); Back(ShowStakePortfolio);
        if(network.ChainId!=202020){page.Children.Add(Text("Этот профиль доступен только в Decimal testnet."));return;}
        if(signer is null){page.Children.Add(Text("Для управления NFT-стейкингом откройте кошелек с ключом."));return;}
        var collection=Field("Контракт NFT-коллекции",preset?.Token);target.Children.Add(AttachNftPicker(collection));
        var standard=new ComboBox { ItemsSource=new[]{"ERC1155","ERC721"},SelectedIndex=preset?.TokenType==2?1:0 };page.Children.Add(standard);
        var hold=Field("Hold: Unix timestamp (0 без заморозки)",preset?.Hold.ToString()??"0");
        var nftId=Field("Номер NFT в коллекции", preset?.TokenId.ToString()??"0"); var validator=Field("Валидатор",preset?.Validator);target.Children.Add(AttachValidatorPicker(validator));
        ToolTip.SetTip(collection, "Полный адрес NFT-коллекции, а не адрес токена ее DEL-резерва.");
        ToolTip.SetTip(nftId, "Целый номер NFT внутри коллекции. Допустим номер 0.");
        var activeInfo=Text("Активную NFT-позицию можно проверить по контракту независимо от каталога.",13,Muted);page.Children.Add(activeInfo);
        async Task<CombinedNftDelegation.Position?> ReadActivePosition()
        {
            var contract=WalletDataReaderAddress(collection.Text);var id=WalletDataReaderQuantity(nftId.Text??"");var validatorAddress=WalletDataReaderAddress(validator.Text);
            var version=epoch;var owner=Address;var activeClient=client;
            var position=await CombinedNftDelegation.GetStakeAsync(activeClient,contract,id,owner,validatorAddress,WalletDataReaderQuantity(hold.Text??"0"));
            if(version!=epoch||owner!=Address||!ReferenceEquals(activeClient,client))throw new InvalidOperationException("Кошелек изменился.");
            activeInfo.Text=position?.IsActive==true?$"Активно: {position.Amount} × NFT #{position.TokenId}\nКоллекция: {position.Nft}\nРезерв (отдельно): {position.ReserveAmount} минимальных единиц {position.ReserveToken}":"Активная NFT-позиция по этим параметрам не найдена.";
            if(position?.IsActive==true){using var locatorReader=new WalletDataReader(activeClient,Path.Combine(dataDirectory,"discovery"));_=locatorReader.RememberNftStake(owner,validatorAddress,contract,id,WalletDataReaderQuantity(hold.Text??"0"));}
            return position;
        }
        Button("Проверить активную NFT-позицию",async()=>{await ReadActivePosition();});
        Button("Перейти к делегированию указанного NFT",()=>{
            var contract=WalletDataReaderAddress(collection.Text);var id=WalletDataReaderQuantity(nftId.Text??"");
            ShowCombinedNftStaking(new NftBalance(contract,id,standard.SelectedIndex==1?"ERC721":"ERC1155",standard.SelectedIndex==1?"ERC721":"ERC1155",BigInteger.Zero,""));return Task.CompletedTask;
        });
        page.Children.Add(Text("Отзыв возвращает всю позицию указанного NFT у этого валидатора. Сначала создается заявка, затем нужно дождаться разморозки и завершить ее ниже. Количество DEL-резерва не является количеством NFT.",13,Muted));
        Button("Подготовить отзыв всей NFT-позиции",async()=>{
            var contract=WalletDataReaderAddress(collection.Text);var id=WalletDataReaderQuantity(nftId.Text??"");var validatorAddress=WalletDataReaderAddress(validator.Text);
            var version=epoch;var owner=Address;var activeClient=client;await CombinedNftDelegation.VerifyAsync(activeClient);
            var position=await ReadActivePosition();if(position?.IsActive!=true||position.NftType!=(standard.SelectedIndex==1?2:3))throw new InvalidOperationException("Активная позиция ERC1155 не найдена.");
            if(version!=epoch||owner!=Address||!ReferenceEquals(activeClient,client))return;
            await Preview(StakingNftDrafts.Withdraw(activeClient,owner,position.Validator,position.Nft,position.TokenId,position.NftType==2,position.Hold),"Отзыв всей NFT-позиции",$"Коллекция: {contract}\nNFT #{id}\nВалидатор: {validatorAddress}\nКоличество: {position.Amount} NFT (вся позиция, не DEL-резерв)");
        },true);
        var newValidator=Field("Новый валидатор для переноса позиции");target.Children.Add(AttachValidatorPicker(newValidator));
        Button("Подготовить перенос всей NFT-позиции",async()=>{
            var destination=WalletDataReaderAddress(newValidator.Text);var position=await ReadActivePosition();
            if(position?.IsActive!=true)throw new InvalidOperationException("Активная позиция не найдена.");
            if(position.Validator.Equals(destination,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Выберите другого валидатора.");
            await Preview(StakingNftDrafts.Transfer(client,Address,position.Validator,position.Nft,position.TokenId,position.NftType==2,position.Hold,destination),"Перенос всей NFT-позиции",$"{position.Amount} × NFT #{position.TokenId}\nНовый валидатор: {destination}\nHold: {position.Hold}");
        });
        page.Children.Add(Text("Завершение отзыва",19));
        var index=Field("Номер заявки на отзыв");var receiptHash=Field("Хэш транзакции создания заявки (необязательно)");
        var details=Text("Номер можно прочитать из квитанции отзыва или указать вручную.",13,Muted);page.Children.Add(details);
        Button("Найти заявку по квитанции",async()=>{
            var hash=receiptHash.Text?.Trim()??"";if(hash.Length!=66||!hash.StartsWith("0x",StringComparison.OrdinalIgnoreCase)||!hash[2..].All(Uri.IsHexDigit))throw new ArgumentException("Введите хэш транзакции: 0x и 64 hex-символа.");
            var version=epoch;var owner=Address;var activeClient=client;await CombinedNftDelegation.VerifyAsync(activeClient);
            var requests=await WithdrawalReceiptReader.ReadAsync(activeClient.Web3,CombinedNftDelegation.Proxy,hash);
            var owned=requests.Where(r=>r.Stake.Delegator.Equals(owner,StringComparison.OrdinalIgnoreCase)&&r.Stake.TokenType is 2 or 3).ToArray();
            if(owned.Length!=1)throw new InvalidOperationException("В этой квитанции должна быть ровно одна заявка NFT вашего счета. Иначе укажите номер заявки вручную.");
            if(version!=epoch||owner!=Address||!ReferenceEquals(activeClient,client))return;
            index.Text=owned[0].StakeIndex.ToString();collection.Text=owned[0].Stake.Token;nftId.Text=owned[0].Stake.TokenId.ToString();validator.Text=owned[0].Stake.Validator;
            details.Text=$"Заявка №{owned[0].StakeIndex}: NFT #{owned[0].Stake.TokenId}, количество {owned[0].Stake.AmountRaw}. Срок: {StakeDate(owned[0].UnfreezeTimestamp)}.";
        });
        async Task<FrozenStake> ReadOwnedRequest()
        {
            var requestIndex=WalletDataReaderQuantity(index.Text??"");var owner=Address;var version=epoch;var activeClient=client;
            await CombinedNftDelegation.VerifyAsync(activeClient);var request=await new FrozenStakeReader(activeClient.Web3,CombinedNftDelegation.Proxy).GetFrozenStakeAsync(requestIndex);
            if(version!=epoch||owner!=Address||!ReferenceEquals(activeClient,client))throw new InvalidOperationException("Кошелек изменился.");
            if(!request.Stake.Delegator.Equals(owner,StringComparison.OrdinalIgnoreCase)||request.Stake.TokenType is not (2 or 3)||!request.IsActiveWithdrawal)throw new InvalidOperationException("Активная NFT-заявка этого счета не найдена.");
            details.Text=$"Заявка №{request.Index}: NFT #{request.Stake.TokenId}, количество {request.Stake.AmountRaw}.\nКоллекция: {request.Stake.Token}\nСрок разморозки: {StakeDate(request.UnfreezeTimestamp)}.";return request;
        }
        Button("Проверить заявку через контракт",async()=>{await ReadOwnedRequest();});
        Button("Завершить размороженный отзыв NFT",async()=>{
            var request=await ReadOwnedRequest();if(!request.IsMatured(DateTimeOffset.UtcNow.ToUnixTimeSeconds()))throw new InvalidOperationException("Срок разморозки этой заявки еще не наступил.");
            await Preview(new DelegationDrafts(network,CombinedNftDelegation.Proxy).BuildCompleteDraft(Address,new[]{request.Index}),"Завершение отзыва NFT №"+request.Index,request.Stake.AmountRaw+" × NFT #"+request.Stake.TokenId+"\n"+request.Stake.Token);
        },true);
    }
    private static string WalletDataReaderAddress(string? value)
    {
        var address=value?.Trim()??"";
        if(address.Length!=42||!address.StartsWith("0x",StringComparison.OrdinalIgnoreCase)||!address[2..].All(Uri.IsHexDigit)||address[2..].All(c=>c=='0'))throw new ArgumentException("Введите полный ненулевой адрес: 0x и 40 hex-символов.");
        return address;
    }
    // The targeted path is enabled only for the actual tested ERC1155 deployment profile.
    private void ShowCombinedNftStaking(NftBalance nft)
    {
        if(!RequireWallet())return;
        Reset("Делегировать NFT",nft.Collection+" · #"+nft.TokenId);Back(ShowNfts);
        if(network.ChainId!=202020||nft.Standard is not ("ERC1155" or "ERC721"))throw new NotSupportedException("Эта форма предназначена для актуального NFT-профиля testnet.");
        if(signer is null){page.Children.Add(Text("Для делегирования откройте кошелек с ключом."));return;}
        page.Children.Add(Text("NFT будет передан в контракт стейкинга DecimalDelegation. Возврат выполняется через заявку отзыва и разморозку.",13,Muted));
        var validator=Field("Валидатор");target.Children.Add(AttachValidatorPicker(validator));var amount=Field("Количество NFT","1");
        amount.IsEnabled=nft.Standard!="ERC721";var hold=Field("Hold: Unix timestamp (0 без заморозки)","0");
        ToolTip.SetTip(hold,"Положительный Unix timestamp блокирует позицию до указанного срока. Укажите 0 для обычного делегирования.");
        ToolTip.SetTip(validator,"Адрес валидатора Decimal. Выберите адрес в списке или вставьте полный 0x-адрес.");
        ToolTip.SetTip(amount,"Целое количество единиц NFT внутри выбранного номера коллекции. Это количество NFT, а не сумма его резерва в DEL.");
        if(nft.Standard=="ERC721"){
            var advanced=new Expander{Header="NFT permit - подпись из совместимого кошелька",IsExpanded=false};
            var panel=new StackPanel{Spacing=8};panel.Children.Add(Text("Поддержка permit зависит от конкретной коллекции. Не используйте ERC20 permit или подпись другого домена. Обычное разрешение выше не требуется для этого пути.",12,Muted));
            var deadline=new TextBox{Watermark="Deadline: Unix timestamp"};var v=new TextBox{Watermark="v: 27 или 28"};var r=new TextBox{Watermark="r: 32 байта hex"};var sigS=new TextBox{Watermark="s: 32 байта hex"};panel.Children.Add(deadline);panel.Children.Add(v);panel.Children.Add(r);panel.Children.Add(sigS);
            var submit=MakeButton("Подготовить делегирование NFT с permit",async()=>{
                var owner=Address;var version=epoch;var activeClient=client;var validatorAddress=WalletDataReaderAddress(validator.Text);
                if(WalletDataReaderQuantity(hold.Text??"0")!=0)throw new InvalidOperationException("Этот permit-путь не поддерживает Hold. Укажите 0 или используйте обычное разрешение.");
                var expiry=WalletDataReaderQuantity(deadline.Text??"");if(expiry<=DateTimeOffset.UtcNow.ToUnixTimeSeconds())throw new ArgumentException("Срок permit истек.");
                var recovery=byte.Parse(v.Text??"");byte[] ParseWord(string? text)=>Convert.FromHexString((text??"").Trim().Replace("0x","",StringComparison.OrdinalIgnoreCase));
                var draft=StakingNftDrafts.Delegate721ByPermit(activeClient,owner,validatorAddress,nft.Contract,nft.TokenId,expiry,recovery,ParseWord(r.Text),ParseWord(sigS.Text));
                await CombinedNftDelegation.VerifyAsync(activeClient);if(!(await new Erc721Service(activeClient).OwnerOfAsync(nft.Contract,nft.TokenId)).Equals(owner,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("NFT не принадлежит текущему счету.");
                if(version!=epoch||owner!=Address||!ReferenceEquals(activeClient,client))return;
                await Preview(draft,"Делегирование ERC721 по разрешению permit",$"NFT #{nft.TokenId}\nКоллекция: {nft.Contract}\nВалидатор: {validatorAddress}\nDeadline: {expiry}");
            });panel.Children.Add(submit);advanced.Content=panel;page.Children.Add(advanced);
        }
        Button("Разрешить контракту стейкинга использовать NFT",async()=>{
            await CombinedNftDelegation.VerifyAsync(client);
            await Preview((nft.Standard=="ERC721"?new Erc721Service(client).Approve(Address,nft.Contract,CombinedNftDelegation.Proxy,nft.TokenId):new Erc1155Service(client).SetApprovalForAll(Address,nft.Contract,CombinedNftDelegation.Proxy,true)),"Разрешение на использование всей NFT-коллекции контрактом стейкинга",nft.Collection+" · оператор "+CombinedNftDelegation.Proxy);
        });
        Button("Делегировать NFT",async()=>{
            var owner=Address;var version=epoch;var activeClient=client;var quantity=WalletDataReaderQuantity(amount.Text??"");
            await CombinedNftDelegation.VerifyAsync(activeClient);var service=new Erc1155Service(activeClient);
            if(nft.Standard=="ERC721") {
                var erc721=new Erc721Service(activeClient);
                if(quantity!=1||!(await erc721.OwnerOfAsync(nft.Contract,nft.TokenId)).Equals(owner,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("NFT не принадлежит текущему счету.");
                if(!(await erc721.GetApprovedAsync(nft.Contract,nft.TokenId)).Equals(CombinedNftDelegation.Proxy,StringComparison.OrdinalIgnoreCase)&&!await erc721.IsApprovedForAllAsync(nft.Contract,owner,CombinedNftDelegation.Proxy))throw new InvalidOperationException("Сначала разрешите передачу этого NFT.");
            } else {
                if(quantity<=0||quantity>await service.BalanceOfAsync(nft.Contract,owner,nft.TokenId))throw new InvalidOperationException("Недостаточно NFT.");
                if(!await service.IsApprovedForAllAsync(nft.Contract,owner,CombinedNftDelegation.Proxy))throw new InvalidOperationException("Сначала подтвердите разрешение.");
            }
            var holdTimestamp=WalletDataReaderQuantity(hold.Text??"0");
            if(holdTimestamp>0&&holdTimestamp<=DateTimeOffset.UtcNow.ToUnixTimeSeconds())throw new InvalidOperationException("Hold должен быть в будущем.");
            if(version!=epoch||owner!=Address||!ReferenceEquals(activeClient,client))return;
            using(var locatorReader=new WalletDataReader(activeClient,Path.Combine(dataDirectory,"discovery")))_=locatorReader.RememberNftStake(owner,validator.Text?.Trim()??"",nft.Contract,nft.TokenId,holdTimestamp);
            await Preview(StakingNftDrafts.Delegate(activeClient,owner,validator.Text?.Trim()??"",nft.Contract,nft.TokenId,quantity,nft.Standard=="ERC721",holdTimestamp),"Делегирование NFT через актуальный DecimalDelegation",quantity+" × NFT #"+nft.TokenId);
        },true);
        Button("Отозвать разрешение коллекции",async()=>{
            await CombinedNftDelegation.VerifyAsync(client);await Preview((nft.Standard=="ERC721"?new Erc721Service(client).Approve(Address,nft.Contract,"0x0000000000000000000000000000000000000000",nft.TokenId):new Erc1155Service(client).SetApprovalForAll(Address,nft.Contract,CombinedNftDelegation.Proxy,false)),"Отзыв разрешения NFT-коллекции",nft.Collection);
        });
    }
}
