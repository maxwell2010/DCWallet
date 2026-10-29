using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.UiTests;
public class NftWorkspaceTests
{
 [AvaloniaFact]public void TestnetWorkspaceHasExplicitNetworkAndWatchModeGuards()
 {
  foreach(var chain in new[]{75,202020}){
   var dir=Path.Combine(Path.GetTempPath(),"dc-nft-workspace-"+Guid.NewGuid());Directory.CreateDirectory(dir);var window=new MainWindow(dir,DecimalNetwork.Custom("fixture",chain,"http://127.0.0.1:1"),autoLoad:false);
   try{window.Show();Click(window,"Только просмотр");window.GetLogicalDescendants().OfType<TextBox>().First().Text="0x1111111111111111111111111111111111111111";Click(window,"Открыть для просмотра");Show(window);Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text==(chain==75?"Этот профиль доступен только в Decimal testnet.":"Для управления NFT-стейкингом откройте кошелек с ключом."));Assert.DoesNotContain(window.GetLogicalDescendants().OfType<Button>(),b=>b.Content?.ToString()=="Подготовить отзыв всей NFT-позиции");}finally{window.Close();Directory.Delete(dir,true);}
  }
 }
 [AvaloniaFact]public async Task ManualWorkspaceRejectsInvalidAddressIdAndHashBeforeAnyRpc()
 {
  var calls=0;using var rpc=new DCWallet.Tests.FakeRpc {ReadResult=(m,p)=>{Interlocked.Increment(ref calls);return "0x31524";}};
  var dir=Path.Combine(Path.GetTempPath(),"dc-nft-fields-"+Guid.NewGuid());MainWindow? window=null;
  try{
   await Vault.CreateAsync(Path.Combine(dir,"wallet.vault"),DecimalWallet.GenerateMnemonic(),"temporary-test-password");window=new MainWindow(dir,DecimalNetwork.Custom("fixture",202020,rpc.Url),autoLoad:false);window.Show();window.GetLogicalDescendants().OfType<TextBox>().Single().Text="temporary-test-password";Click(window,"Открыть кошелек");for(var i=0;i<200&&!window.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text=="Баланс еще не загружен");i++)await Task.Delay(20);Show(window);
   Click(window,"Подготовить отзыв всей NFT-позиции");await Task.Delay(20);Assert.Equal(0,calls);
   Field(window,"Контракт NFT-коллекции").Text="0x2222222222222222222222222222222222222222";Field(window,"Номер NFT в коллекции").Text="-1";Field(window,"Валидатор").Text="0x3333333333333333333333333333333333333333";Click(window,"Подготовить отзыв всей NFT-позиции");await Task.Delay(20);Assert.Equal(0,calls);
   Click(window,"Найти заявку по квитанции");await Task.Delay(20);Assert.Equal(0,calls);Click(window,"Проверить заявку через контракт");await Task.Delay(20);Assert.Equal(0,calls);Assert.Equal(0,rpc.Broadcasts);
   Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text?.Contains("не DEL-резерв")==true||t.Text?.Contains("не является количеством NFT")==true);
  }finally{window?.Close();if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
 static TextBox Field(Window w,string label)=>(w.GetLogicalDescendants().OfType<TextBlock>().First(t=>t.Text==label).Parent as StackPanel)!.Children.OfType<TextBox>().Single();
 static void Show(MainWindow w)=>typeof(MainWindow).GetMethod("ShowCombinedNftWorkspace",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);
 static void Click(Window w,string label)=>w.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label||b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
