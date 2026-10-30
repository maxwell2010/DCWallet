using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Xunit;
namespace DCWallet.UiTests;
public sealed class SafeNavigationRegressionTests
{
    [AvaloniaFact]
    public async Task CommonAccountMenuOpensWeightedSafeWorkspaceDirectly()
    {
        await InWallet(async window=>
        {
            Click(window,"Общий счет"); await Task.Delay(30);AssertSafeWorkspace(window);
        });
    }
    [AvaloniaFact]
    public async Task ToolsDoNotOfferLegacySdkMultisigButQuickActionUsesWeightedWorkspace()
    {
        await InWallet(async window=>
        {
            typeof(MainWindow).GetMethod("ShowOperations",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,["TokenEconomy"]);await Task.Delay(30);
            var groups=window.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.ItemsSource is IEnumerable<OperationGroup>);
            var choices=((IEnumerable<OperationGroup>)groups.ItemsSource!).ToArray();
            Assert.DoesNotContain(choices,g=>g.Code=="Multisig");
            Assert.Contains(OperationCatalog.All,o=>o.Group=="Multisig"); // Preserve SDK fixtures, restrict the public UI path.
            foreach(var group in choices)
            {
                groups.SelectedItem=group;await Task.Delay(10);
                var methods=window.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.ItemsSource is IEnumerable<Operation>);
                Assert.DoesNotContain((IEnumerable<Operation>)methods.ItemsSource!,o=>o.Group=="Multisig");
            }
            Click(window,"Общий счет");await Task.Delay(30);AssertSafeWorkspace(window);
        });
    }
    [AvaloniaFact]
    public async Task LegacyMultisigRouteCannotReopenGenericSdkEditors()
    {
        await InWallet(async window=>
        {
            typeof(MainWindow).GetMethod("ShowOperations",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,["Multisig"]);
            await Task.Delay(30);AssertSafeWorkspace(window);
        });
    }
    static void AssertSafeWorkspace(MainWindow window)
    {
        Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text?.Contains("Взвешенный Decimal Safe",StringComparison.Ordinal)==true);
        Assert.Contains(window.GetLogicalDescendants().OfType<Button>(),b=>b.Content?.ToString()=="Загрузить владельцев и порог");
        Assert.Contains(window.GetLogicalDescendants().OfType<Button>(),b=>b.Content?.ToString()=="Проверить подписи и подготовить исполнение");
        Assert.DoesNotContain(window.GetLogicalDescendants().OfType<ComboBox>(),c=>c.ItemsSource is IEnumerable<Operation> || c.ItemsSource is IEnumerable<OperationGroup>);
    }
    static async Task InWallet(Func<MainWindow,Task> scenario)
    {
        var directory=Path.Combine(Path.GetTempPath(),"dc-safe-navigation-"+Guid.NewGuid());Directory.CreateDirectory(directory);var window=new MainWindow(directory,autoLoad:false);
        try
        {
            window.Show();Click(window,"Только просмотр");window.GetLogicalDescendants().OfType<TextBox>().First().Text="0x1111111111111111111111111111111111111111";Click(window,"Открыть для просмотра");await Task.Delay(30);await scenario(window);
        }
        finally{window.Close();Directory.Delete(directory,true);}
    }
    static void Click(Window window,string label)=>window.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label||b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
