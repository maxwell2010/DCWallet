using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Xunit;
namespace DCWallet.UiTests;
public class ChecksAssetModeUiTests
{
 [AvaloniaFact]public async Task MainnetDefaultsAndThreeAssetModesKeepUnusedSecretsHidden(){var dir=Path.Combine(Path.GetTempPath(),"dc-checks-modes-"+Guid.NewGuid());Directory.CreateDirectory(dir);var window=new MainWindow(dir,autoLoad:false);try{window.Show();Click(window,"Только просмотр");window.GetLogicalDescendants().OfType<TextBox>().First().Text="0x1111111111111111111111111111111111111111";Click(window,"Открыть для просмотра");typeof(MainWindow).GetMethod("ShowChecks",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,null);
 var contract=Field(window,"Контракт Checks");Assert.Equal(ChecksDeploymentProfile.MainnetContract,contract.Text);var token=Field(window,"Контракт ERC20 (только для токенового чека)");var password=Field(window,"Пароль кошелька (только для permit)");var asset=window.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.ItemsSource is string[] a&&a.SequenceEqual(new[]{"DEL","ERC20","ERC20 с permit"}));var approval=window.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Разрешить точную сумму токенового чека");Assert.False(((Control)token.Parent!).IsVisible);Assert.False(((Control)password.Parent!).IsVisible);Assert.False(approval.IsVisible);asset.SelectedIndex=1;Assert.True(((Control)token.Parent!).IsVisible);Assert.False(((Control)password.Parent!).IsVisible);Assert.True(approval.IsVisible);asset.SelectedIndex=2;Assert.True(((Control)password.Parent!).IsVisible);Assert.False(approval.IsVisible);Assert.NotEqual('\0',password.PasswordChar);password.Text="fixture-secret-not-to-echo";asset.SelectedIndex=0;Assert.Equal("",password.Text);Assert.False(((Control)password.Parent!).IsVisible);Assert.Contains("Unix",ToolTip.GetTip(Field(window,"Последний блок погашения (номер блока, не дата)"))?.ToString());Click(window,"Сохранить чек и подготовить создание");await Task.Delay(20);Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text=="Для создания чека откройте кошелек с ключом.");Assert.DoesNotContain(window.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text?.Contains("fixture-secret-not-to-echo")==true);
 }finally{window.Close();Directory.Delete(dir,true);}}
 static TextBox Field(Window w,string label)=>(w.GetLogicalDescendants().OfType<TextBlock>().First(t=>t.Text==label).Parent as StackPanel)!.Children.OfType<TextBox>().Single();
 static void Click(MainWindow w,string label)=>w.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label||b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
