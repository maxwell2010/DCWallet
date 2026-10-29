using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.UiTests;
public class NewWorkspaceVisualTests
{
 [AvaloniaFact]public async Task ProfilesChecksAndSafeDelegateRenderAcrossThreeThemesWithoutSecretValues(){
  foreach(var theme in new[]{"light","dark","neon"}){var directory=Path.Combine(Path.GetTempPath(),"dc-new-workspaces-"+Guid.NewGuid());Directory.CreateDirectory(directory);await File.WriteAllTextAsync(Path.Combine(directory,"preferences.json"),"{\"Theme\":\""+theme+"\"}");var window=new MainWindow(directory,DecimalNetwork.Testnet(),autoLoad:false){Width=1180,Height=940};
   try{window.Show();Show(window,"ShowProfiles");Capture(window,theme,"profiles");Show(window,"ShowPrivateKeyImport");Capture(window,theme,"key-import");
    var fields=window.GetLogicalDescendants().OfType<TextBox>().ToArray();fields[1].Text="invalid-sensitive-fixture";fields[2].Text=fields[3].Text="fixture-password-valid";Click(window,"Сохранить зашифрованный профиль");for(var i=0;i<50&&window.GetLogicalDescendants().OfType<TextBlock>().All(t=>t.Text!="Invalid private key.");i++)await Task.Delay(10);Assert.DoesNotContain(window.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text?.Contains("invalid-sensitive-fixture")==true);Assert.Empty(new VaultProfiles(directory).List());
    Show(window,"ShowAccess");Click(window,"Только просмотр");window.GetLogicalDescendants().OfType<TextBox>().First().Text="0x1111111111111111111111111111111111111111";Click(window,"Открыть для просмотра");Show(window,"ShowChecks");Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text?.Contains("Пароль чека передайте отдельно")==true);Capture(window,theme,"checks");
    Show(window,"ShowSafe");window.GetLogicalDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Расширенный вызов контракта").IsExpanded=true;var mode=window.GetLogicalDescendants().OfType<CheckBox>().Single(c=>c.Content?.ToString()=="Расширенная операция DelegateCall");mode.IsChecked=true;Assert.Contains(window.GetLogicalDescendants().OfType<CheckBox>(),c=>c.Content?.ToString()?.Contains("может изменить владельцев")==true||c.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("может изменить владельцев")==true));var scroll=window.GetLogicalDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="MainScroll");await Task.Delay(20);scroll.Offset=new Avalonia.Vector(0,600);await Task.Delay(20);Capture(window,theme,"safe-delegate");
   }finally{window.Close();Directory.Delete(directory,true);}
  }
 }
 static void Show(MainWindow w,string name)=>typeof(MainWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,null);
 static void Click(MainWindow w,string label)=>w.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label||b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
 static void Capture(MainWindow w,string theme,string name){var root=Environment.GetEnvironmentVariable("DC_QA_DIR")??Path.Combine(AppContext.BaseDirectory,"screenshots");Directory.CreateDirectory(root);using var frame=w.CaptureRenderedFrame();Assert.NotNull(frame);frame.Save(Path.Combine(root,theme+"-"+name+".png"));}
}
