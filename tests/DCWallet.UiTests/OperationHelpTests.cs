using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Xunit;
namespace DCWallet.UiTests;
public class OperationHelpTests
{
 [AvaloniaFact]public async Task ToolsExplainPurposeUnitsAndKeepJsonInAdvancedSection()
 {
  var directory=Path.Combine(Path.GetTempPath(),"dc-tools-help-"+Guid.NewGuid());Directory.CreateDirectory(directory);var window=new MainWindow(directory,autoLoad:false);
  try {
   window.Show();Click(window,"Только просмотр");window.GetLogicalDescendants().OfType<TextBox>().First().Text="0x1111111111111111111111111111111111111111";Click(window,"Открыть для просмотра");
   typeof(MainWindow).GetMethod("ShowOperations",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object?[]{"TokenEconomy"});await Task.Delay(30);
   var combos=window.GetLogicalDescendants().OfType<ComboBox>().Where(c=>c.ItemsSource is IEnumerable<OperationGroup> || c.ItemsSource is IEnumerable<Operation>).ToArray();var operations=combos.Single(c=>c.ItemsSource is IEnumerable<Operation>);
   operations.SelectedItem=OperationCatalog.All.First(o=>o.Method.Name=="BuildBuyTokenForExactDelDraft");await Task.Delay(30);
   Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text?.Contains("ограничивает допустимую цену")==true);
   Assert.Contains(window.GetLogicalDescendants().OfType<TextBox>(),t=>ToolTip.GetTip(t)?.ToString()?.Contains("1 DEL = 1000000000000000000")==true);
   var advanced=window.GetLogicalDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Расширенные параметры и JSON");Assert.False(advanced.IsExpanded);
   var json=window.GetLogicalDescendants().OfType<CheckBox>().Single(c=>c.Content?.ToString()=="Редактировать исходный JSON");Assert.False(json.IsEffectivelyVisible);
   advanced.IsExpanded=true;await Task.Delay(30);Assert.True(json.IsEffectivelyVisible);json.IsChecked=true;await Task.Delay(30);
   var raw=window.GetLogicalDescendants().OfType<TextBox>().Single(t=>t.AcceptsReturn&&t.Text?.Contains("amountOutMinRaw")==true);Assert.True(raw.IsEffectivelyVisible);Assert.Contains("valueWei",raw.Text);
   json.IsChecked=false;await Task.Delay(30);Assert.False(raw.IsEffectivelyVisible);
  }finally{window.Close();Directory.Delete(directory,true);}
 }
 static void Click(Window window,string label)=>window.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label||b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
