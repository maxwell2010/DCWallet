using System.Numerics;
using Avalonia.Controls;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisend;
using Decimal.Web3.Tokens;
using Decimal.Web3.Transactions;
namespace DCWallet;
public sealed partial class MainWindow
{
    private long permitGeneration;
    private Action? clearPermitCredential;
    private async Task PreviewWithPermit(string token,string spender,BigInteger raw,string description,string summary,Func<Erc20PermitSignature,TransactionDraft> buildDraft,Func<Task> validate,Action returnTo)
    {
        var activeClient=client;var activeOwner=Address;var activeSigner=signer??throw new InvalidOperationException("Для Permit откройте кошелек с ключом.");var activeEpoch=epoch;var generation=permitGeneration;var vaultPath=VaultPath;
        void Current(){if(activeEpoch!=epoch||generation!=permitGeneration||!ReferenceEquals(activeClient,client)||!ReferenceEquals(activeSigner,signer)||activeOwner!=Address||vaultPath!=VaultPath)throw new InvalidOperationException("Кошелек или параметры изменились. Подготовьте Permit заново.");}
        async Task ValidateProfile(){try{await DesktopPermitPolicy.ValidateAsync(activeClient,activeOwner,token,spender,raw);}catch(NotSupportedException){throw;}catch{throw new InvalidOperationException("Не удалось подтвердить контракт, баланс или разрешение Permit. Повторите проверку.");}}
        await validate();Current();
        await ValidateProfile();Current();
        var deadline=DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        Erc20PermitData data;
        try{data=await new Erc20PermitService(activeClient,token).PrepareAsync(activeOwner,spender,raw,deadline);}catch{throw new InvalidOperationException("Не удалось подтвердить поддержку Permit. Можно явно разрешить точную сумму отдельной транзакцией.");}
        Current();var digest=Erc20PermitService.CalculateDigest(data);var metadata=await new Erc20Service(activeClient,token).GetMetadataAsync();Current();
        Reset("Разрешение по подписи",description);generation=permitGeneration;
        BeginCard();foreach(var text in new[]{"Сеть: "+network.Name+" · "+network.ChainId,"Кошелек: "+activeOwner,"Токен: "+token,"Получатель разрешения: "+spender,"Точная сумма: "+Units.Format(raw,metadata.Decimals)+" "+metadata.Symbol,"Nonce Permit: "+data.Nonce,"Разрешение действует до: "+DateTimeOffset.FromUnixTimeSeconds(deadline).ToString("u"),summary,"Подпись Permit бесплатна. После нее отдельно подтвердите транзакцию и комиссию."})target.Children.Add(Text(text,14));EndCard();
        var passwordHost=new StackPanel{Spacing=5};passwordHost.Children.Add(Text("Пароль кошелька для подписи Permit"));var password=new TextBox{Name="DesktopPermitPassword",PasswordChar='●',MinHeight=34};password.DetachedFromVisualTree+=(_,_)=>password.Text="";passwordHost.Children.Add(password);passwordHost.IsVisible=!Vault.IsPasswordless(vaultPath);target.Children.Add(passwordHost);clearPermitCredential=()=>password.Text="";var consumed=false;
        async Task Fresh(){Current();await validate();Current();await ValidateProfile();Current();if(DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60>=deadline)throw new InvalidOperationException("Permit истек. Подготовьте операцию заново.");Erc20PermitData fresh;try{fresh=await new Erc20PermitService(activeClient,token).PrepareAsync(activeOwner,spender,raw,deadline);}catch{throw new InvalidOperationException("Не удалось повторно проверить Permit. Транзакция не отправлена.");}Current();if(!Erc20PermitService.CalculateDigest(fresh).AsSpan().SequenceEqual(digest))throw new InvalidOperationException("Nonce или домен Permit изменился. Подготовьте операцию заново.");}
        var signButton=MakeButton("Подписать разрешение",async()=>{
            if(consumed)throw new InvalidOperationException("Permit уже использован.");consumed=true;var credentialText=password.Text??"";password.Text="";
            try{
                await Fresh();var credential=await Vault.OpenCredentialAsync(vaultPath,credentialText);credentialText="";
                try{Current();using var temporary=CreateCredentialSigner(credential);if(!temporary.Address.Equals(activeOwner,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Подписант изменился.");await Fresh();var signatureBytes=Convert.FromHexString(temporary.SignDigest(digest).Replace("0x",""));if(signatureBytes.Length!=65||signatureBytes[64] is not(27 or 28))throw new InvalidOperationException("Некорректная подпись Permit.");var signature=new Erc20PermitSignature(deadline,signatureBytes[64],signatureBytes[..32],signatureBytes[32..64]);await Fresh();var draft=buildDraft(signature);if(!draft.From.Equals(activeOwner,StringComparison.OrdinalIgnoreCase)||!draft.To.Equals(spender,StringComparison.OrdinalIgnoreCase)||draft.ValueWei!=0)throw new InvalidOperationException("Параметры Permit не совпадают с согласием.");await Preview(draft,description,summary,beforeSend:Fresh);}
                finally{credential=new("Mnemonic","");}
            }finally{credentialText="";password.Text="";}
        },true);
        signButton.Name="DesktopPermitSign";target.Children.Add(signButton);
        var cancelButton=MakeButton("Отменить Permit",()=>{consumed=true;password.Text="";Invalidate();returnTo();return Task.CompletedTask;});cancelButton.Name="DesktopPermitCancel";target.Children.Add(cancelButton);
    }
}
