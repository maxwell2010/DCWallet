using System.Text.Json;
using Avalonia.Controls;
using DCWallet.Core;
namespace DCWallet;
public sealed partial class MainWindow
{
 private void LoadSelectedProfile(){try{var path=Path.Combine(dataDirectory,"selected-profile.json");if(File.Exists(path)&&new FileInfo(path).Length<200){var id=JsonSerializer.Deserialize<string>(File.ReadAllText(path));if(id is not null&&new VaultProfiles(dataDirectory).List().Any(p=>p.Id==id))selectedProfileId=id;}}catch(JsonException){} }
 private void SelectProfile(string id){_=new VaultProfiles(dataDirectory).Find(id);selectedProfileId=id;accountIndex=0;privateKeyProfile=false;Directory.CreateDirectory(dataDirectory);File.WriteAllText(Path.Combine(dataDirectory,"selected-profile.json"),JsonSerializer.Serialize(id));Lock();}
 private WalletSigningSession CreateCredentialSigner(VaultCredential credential){if(credential.Kind=="PrivateKey"){return WalletSigningSession.FromPrivateKey(credential.Secret,network.ChainId);}return WalletSigningSession.FromMnemonic(credential.Secret,network.ChainId,accountIndex);}
 private async Task StoreCredentialAsync(VaultCredential credential,string password,string name){
  if(!File.Exists(Path.Combine(dataDirectory,"wallet.vault"))&&new VaultProfiles(dataDirectory).List().Count==0){if(credential.Kind=="PrivateKey")await Vault.CreatePrivateKeyAsync(Path.Combine(dataDirectory,"wallet.vault"),credential.Secret,password);else await Vault.CreateAsync(Path.Combine(dataDirectory,"wallet.vault"),credential.Secret,password);selectedProfileId="legacy";}
  else{var profile=await new VaultProfiles(dataDirectory).CreateAsync(name,credential,password);selectedProfileId=profile.Id;}
  accountIndex=0;Directory.CreateDirectory(dataDirectory);await File.WriteAllTextAsync(Path.Combine(dataDirectory,"selected-profile.json"),JsonSerializer.Serialize(selectedProfileId));
 }
 private void ShowProfiles(){Reset("Профили кошелька","Каждый профиль хранит независимую фразу или приватный ключ в отдельном зашифрованном файле.");Back(()=>{if(signer is not null||watchAddress is not null)ShowAccounts();else ShowAccess();});
  foreach(var profile in new VaultProfiles(dataDirectory).List()){var selected=profile;Button(profile.Name+(profile.Id==selectedProfileId?" · выбран":""),()=>{SelectProfile(selected.Id);return Task.CompletedTask;});}
  Button("Создать независимую фразу",()=>{Lock();ShowCreate();return Task.CompletedTask;});Button("Импортировать независимую фразу",()=>{Lock();ShowImport();return Task.CompletedTask;});Button("Импортировать приватный ключ",()=>{Lock();ShowPrivateKeyImport();return Task.CompletedTask;});Button("Восстановить зашифрованную копию в новый профиль",async()=>{Lock();await RestoreBackup();});
 }
 private void ShowPrivateKeyImport(){Reset("Импорт приватного ключа","Ключ сохраняется только в зашифрованном файле. Один приватный ключ соответствует одному адресу.");Back(ShowAccess);BeginCard();var name=Field("Название профиля","Импорт ключа");var key=Field("Приватный ключ: 64 hex-символа",secret:true);var password=Field("Пароль кошелька",secret:true);var repeat=Field("Пароль еще раз",secret:true);EndCard();Button("Сохранить зашифрованный профиль",async()=>{if(password.Text!=repeat.Text)throw new ArgumentException("Пароли не совпадают.");var value=key.Text??"";var pass=password.Text??"";try{await StoreCredentialAsync(new("PrivateKey",value),pass,name.Text??"Импорт ключа");key.Text=password.Text=repeat.Text="";ShowAccess();}finally{value=pass="";}},true);}
}
