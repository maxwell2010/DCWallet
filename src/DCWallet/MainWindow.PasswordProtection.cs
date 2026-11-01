using Avalonia.Controls;
using DCWallet.Core;
namespace DCWallet;
public sealed partial class MainWindow
{
    private void ShowPasswordProtectionControls()
    {
        var path=VaultPath;var disabled=Vault.IsPasswordless(path);
        target.Children.Add(new Separator());target.Children.Add(Text("Парольная защита текущего профиля",15));
        target.Children.Add(Text(disabled?"Пароль отключен, в том числе при подписи транзакций и Permit.":"Парольная защита включена.",13,Muted));
        target.Children.Add(Text("Внимание: без пароля любой, кто получит файл кошелька или доступ к компьютеру, сможет узнать ключи и перевести средства. Автоблокировка в этом режиме не защищает средства. Резервные копии, созданные без пароля, также доступны без него. Подтверждение каждой операции остается обязательным.",13,UiPalette.Error));
        var current=Field("Пароль для изменения защиты",secret:true);((Control)current.Parent!).IsVisible=!disabled;
        TextBox? next=null,repeat=null;
        var consent=new CheckBox{Content="Понимаю риск и хочу отключить пароль",IsVisible=!disabled};target.Children.Add(consent);
        if(disabled){next=Field("Новый пароль для включения защиты",secret:true);repeat=Field("Повтор нового пароля защиты",secret:true);}
        Button(disabled?"Включить парольную защиту":"Отключить пароль",async()=>{
            if(path!=VaultPath)throw new InvalidOperationException("Профиль изменился.");
            if(!disabled&&consent.IsChecked!=true)throw new InvalidOperationException("Подтвердите предупреждение безопасности.");
            if(disabled&&next!.Text!=repeat!.Text)throw new ArgumentException("Пароли не совпадают.");
            var old=current.Text??"";var replacement=next?.Text??"";current.Text="";if(next is not null)next.Text="";if(repeat is not null)repeat.Text="";
            try{await Vault.SetPasswordProtectionAsync(path,old,replacement,!disabled);}
            finally{old=replacement="";}
            if(path==VaultPath){Lock();status.Text=disabled?"Парольная защита включена.":"Пароль отключен для текущего профиля. Операции доступны без пароля.";}
        });
    }
}
