using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.UiTests;
public class VaultProfileUiTests
{
 [AvaloniaFact]public async Task KeyProfileUnlocksAsOneAddressAndLegacyRemainsSelectable(){var directory=Path.Combine(Path.GetTempPath(),"dc-profile-ui-"+Guid.NewGuid());Directory.CreateDirectory(directory);const string password="fixture-password-long";await Vault.CreateAsync(Path.Combine(directory,"wallet.vault"),DecimalWallet.GenerateMnemonic(),password);var profile=await new VaultProfiles(directory).CreateAsync("Key fixture",new("PrivateKey",new string('0',63)+"1"),password);var window=new MainWindow(directory,autoLoad:false);try{window.Show();typeof(MainWindow).GetMethod("SelectProfile",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,new object[]{profile.Id});var pass=window.GetLogicalDescendants().OfType<TextBox>().Single();pass.Text=password;window.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Открыть кошелек").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));for(var i=0;i<100&&typeof(MainWindow).GetField("signer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window) is null;i++)await Task.Delay(20);var signer=Assert.IsType<WalletSigningSession>(typeof(MainWindow).GetField("signer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window));Assert.Equal("0x7E5F4552091A69125d5DfCb7b8C2659029395Bdf",signer.Address);var selector=window.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.Name=="AccountSelector");Assert.False(selector.IsEnabled);Assert.Single(selector.Items);typeof(MainWindow).GetMethod("SelectProfile",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,new object[]{"legacy"});Assert.Null(typeof(MainWindow).GetField("signer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window));Assert.True(File.Exists(Path.Combine(directory,"wallet.vault")));Assert.Equal(2,new VaultProfiles(directory).List().Count);}finally{window.Close();Directory.Delete(directory,true);}}
}
