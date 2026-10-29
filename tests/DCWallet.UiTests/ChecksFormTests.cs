using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Decimal.Web3;
using Xunit;
namespace DCWallet.UiTests;
public sealed class ChecksFormTests
{
    [AvaloniaFact]public void ChecksShowsAssetModesEncryptedFileActionsAndSeparatePasswords()
    {
        var dir=Path.Combine(Path.GetTempPath(),"dc-checks-ui-"+Guid.NewGuid());Directory.CreateDirectory(dir);var window=new MainWindow(dir,DecimalNetwork.Testnet(),autoLoad:false);
        try
        {
            window.Show();Click(window,"Только просмотр");window.GetLogicalDescendants().OfType<TextBox>().First().Text="0x1111111111111111111111111111111111111111";Click(window,"Открыть для просмотра");
            typeof(MainWindow).GetMethod("ShowChecks",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
            var controls=window.GetLogicalDescendants().ToArray();Assert.Contains(controls.OfType<ComboBox>(),c=>c.Items.Cast<object>().Any(i=>i.ToString()=="ERC20 с permit"));
            Assert.Contains(controls.OfType<TextBox>(),t=>t.Text=="0xb6161CAA8Bd7167C2fa67b93b041FF9a94d6a735");
            Assert.True(controls.OfType<TextBox>().Count(t=>t.PasswordChar!='\0')>=3);
            Assert.Contains(controls.OfType<Button>(),b=>b.Content?.ToString()=="Сохранить чек и подготовить создание");Assert.Contains(controls.OfType<Button>(),b=>b.Content?.ToString()=="Открыть файл и подготовить погашение");
            Assert.Contains(controls.OfType<TextBlock>(),t=>t.Text?.Contains("Пароль чека передайте отдельно")==true);
        }
        finally{window.Close();Directory.Delete(dir,true);}
    }
    private static void Click(Window window,string text)=>window.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==text||b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
