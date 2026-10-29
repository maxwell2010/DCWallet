using System.Numerics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using DCWallet.Tests;
using Decimal.Web3;
using Xunit;
namespace DCWallet.UiTests;
public sealed class PortfolioScaleTests
{
    [AvaloniaFact]
    public async Task LargeNftPortfolioPagesAndSearchResetsPageSize()
    {
        var directory=Path.Combine(Path.GetTempPath(),"dc-nft-scale-"+Guid.NewGuid());Directory.CreateDirectory(directory);MainWindow? window=null;
        try
        {
            window=new MainWindow(directory,autoLoad:false);window.Show();OpenWatch(window);
            var nfts=Enumerable.Range(1,2501).Select(i=>new NftBalance(DiscoveryFixture.Token,i,"ERC721","Collection",BigInteger.One,"")).ToArray();
            typeof(MainWindow).GetField("nftData",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,new DataSet<NftBalance>(nfts,100,true,[]));
            Click(window,"NFT");
            Assert.Equal(48,NftLabels(window).Count());Assert.Contains("Найдено: 2501. Всего NFT-позиций: 2501.",Labels(window));
            Click(window,"Показать следующие 48 NFT");Assert.Equal(96,NftLabels(window).Count());
            var search=window.GetLogicalDescendants().OfType<TextBox>().Single();search.Text="2501"; await Task.Delay(50);
            Assert.Equal("Collection · #2501",Assert.Single(NftLabels(window)));
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<Button>(),b=>b.Content?.ToString()=="Показать следующие 48 NFT");
            search.Text="";await Task.Delay(50);Assert.Equal(48,NftLabels(window).Count());
            search.Text="not-found";await Task.Delay(50);Assert.Empty(NftLabels(window));Assert.Contains("Найдено: 0. Всего NFT-позиций: 2501.",Labels(window));
        }
        finally{window?.Close();Directory.Delete(directory,true);}
    }
    [AvaloniaFact]
    public async Task LockCancelsPendingCatalogAndNeverRepopulatesPrivatePortfolio()
    {
        using var rpc=new DiscoveryRpc();var handlers=new List<DelayedCatalog>();
        var directory=Path.Combine(Path.GetTempPath(),"dc-load-cancel-"+Guid.NewGuid());Directory.CreateDirectory(directory);MainWindow? window=null;
        try
        {
            window=new MainWindow(directory,DecimalNetwork.Mainnet() with {RpcUrl=new Uri(rpc.Url)},dataReaderFactory:client=>
            {var handler=new DelayedCatalog();handlers.Add(handler);return new WalletDataReader(client,handler:handler);});
            window.Show();OpenWatch(window);
            for(var i=0;i<200&&!handlers.Any(h=>h.Started);i++)await Task.Delay(10);
            Assert.Contains(handlers,h=>h.Started);Click(window,"Заблокировать");
            for(var i=0;i<200&&!handlers.Any(h=>h.Cancelled);i++)await Task.Delay(10);
            Assert.Contains(handlers,h=>h.Cancelled);await Task.Delay(100);
            Assert.DoesNotContain(Labels(window),t=>t?.Contains("TEST") == true || t?.Contains("Collection · #") == true);
            Assert.Equal(0,rpc.Broadcasts);
        }
        finally{window?.Close();Directory.Delete(directory,true);}
    }
    static void OpenWatch(Window window){Click(window,"Только просмотр");window.GetLogicalDescendants().OfType<TextBox>().First().Text=DiscoveryFixture.Owner;Click(window,"Открыть для просмотра");}
    static IEnumerable<string?> Labels(Window window)=>window.GetLogicalDescendants().OfType<TextBlock>().Select(t=>t.Text);
    static IEnumerable<string?> NftLabels(Window window)=>Labels(window).Where(t=>t?.StartsWith("Collection · #")==true);
    static void Click(Window window,string label)=>window.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label||b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    sealed class DelayedCatalog:HttpMessageHandler
    {
        public volatile bool Started;public volatile bool Cancelled;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Started=true;
            try{await Task.Delay(Timeout.InfiniteTimeSpan,ct);throw new InvalidOperationException();}
            catch(OperationCanceledException){Cancelled=true;throw;}
        }
    }
}
