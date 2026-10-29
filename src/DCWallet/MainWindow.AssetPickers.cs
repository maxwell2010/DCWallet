using Avalonia.Controls;
using Avalonia.Layout;
using DCWallet.Core;

namespace DCWallet;
public sealed partial class MainWindow
{
    private sealed record AddressChoice(string Address, string Label)
    {
        public override string ToString() => Label + " · " + Address;
    }
    private Control AddressPicker(TextBox input, Func<Task<IReadOnlyList<AddressChoice>>> load, string title, Func<string, CancellationToken, Task<IReadOnlyList<AddressChoice>>>? remoteSearch = null, bool portfolioRefresh = true)
    {
        var panel = new StackPanel { Spacing = 5 };
        var search = new TextBox { Watermark = "Поиск по названию или адресу" };
        var list = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 280, PlaceholderText = title };
        IReadOnlyList<AddressChoice> choices = [];
        IReadOnlyList<AddressChoice> remoteChoices = []; CancellationTokenSource? searchCancellation = null;
        string? remoteQuery = null; bool applying = false, attached = false;
        void Filter()
        {
            var query = search.Text?.Trim() ?? ""; applying = true;
            try { list.ItemsSource = choices.Where(c => c.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)).Concat(remoteQuery == query ? remoteChoices : []).DistinctBy(c => c.Address, StringComparer.OrdinalIgnoreCase).ToArray(); }
            finally { applying = false; }
        }
        async void PortfolioChanged()
        {
            var version = epoch; var chain = network.ChainId; var owner = Address;
            try { var loaded = await load(); if (attached && version == epoch && chain == network.ChainId && owner == Address) { choices = loaded; Filter(); } }
            catch (Exception) { /* Keep the existing list and form input when background refresh fails. */ }
        }
        search.TextChanged += async (_, _) =>
        {
            Filter(); searchCancellation?.Cancel(); searchCancellation?.Dispose();
            var query = search.Text?.Trim() ?? ""; if (remoteSearch is null || query.Length < 2 || !automaticLoading) return;
            searchCancellation = new CancellationTokenSource(); var ct = searchCancellation.Token; var version = epoch; var chain = network.ChainId;
            try
            {
                await Task.Delay(350, ct); var found = await remoteSearch(query, ct);
                if (!attached || ct.IsCancellationRequested || version != epoch || chain != network.ChainId || query != search.Text?.Trim()) return;
                remoteChoices = found; remoteQuery = query; Filter();
                list.PlaceholderText = "Результаты поиска - выберите адрес";
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!ct.IsCancellationRequested) list.PlaceholderText = "Поиск недоступен - введите адрес вручную"; }
        };
        panel.DetachedFromVisualTree += (_, _) => { attached = false; searchCancellation?.Cancel(); if (portfolioRefresh) PortfolioUpdated -= PortfolioChanged; };
        list.SelectionChanged += (_, _) => { if (!applying && list.SelectedItem is AddressChoice choice) input.Text = choice.Address; };
        var refresh = MakeButton("Обновить список", async () =>
        {
            var version = epoch; var chain = network.ChainId; var owner = Address;
            var loaded = await load();
            if (version != epoch || chain != network.ChainId || owner != Address) return;
            choices = loaded; Filter();
            status.Text = $"Загружено адресов: {choices.Count}. Адрес можно также ввести вручную.";
        });
        panel.Children.Add(search); panel.Children.Add(list); panel.Children.Add(refresh);
        // The loader runs only once attached, with failures shown locally and manual entry always available.
        panel.AttachedToVisualTree += async (_, _) =>
        {
            attached = true; if (portfolioRefresh) { PortfolioUpdated -= PortfolioChanged; PortfolioUpdated += PortfolioChanged; }
            var version = epoch; var chain = network.ChainId;
            try { var loaded = await load(); if (version == epoch && chain == network.ChainId) { choices = loaded; Filter(); } }
            catch (Exception) { list.PlaceholderText = "Список недоступен - введите адрес вручную"; }
        };
        return panel;
    }
    private Control AttachTokenPicker(TextBox input, bool reserveOnly = false) => AddressPicker(input, () =>
    {
        var items = (tokenData?.Items ?? []).Select(t => new AddressChoice(t.Contract, t.Symbol + " · " + t.Amount))
            .Concat((stakeData?.Items ?? []).Where(s => s.Token.Length == 42).Select(s => new AddressChoice(s.Token, s.Symbol)))
            .DistinctBy(t => t.Address, StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.FromResult<IReadOnlyList<AddressChoice>>(items);
    }, "Выберите токен кошелька", async (query, ct) =>
    {
        using var catalog = new AssetCatalogReader(network);
        var result = await catalog.SearchTokensAsync(query, ct);
        return result.Items.Select(t => new AddressChoice(t.Address, t.Symbol + " · " + t.Name)).ToArray();
    });
    private Control AttachNftPicker(TextBox input) => AddressPicker(input, () => Task.FromResult<IReadOnlyList<AddressChoice>>(
        (nftData?.Items ?? []).Select(n => new AddressChoice(n.Contract, n.Collection + " · " + n.Standard)).DistinctBy(n => n.Address, StringComparer.OrdinalIgnoreCase).ToArray()), "Выберите NFT-коллекцию");
    private Control AttachValidatorPicker(TextBox input) => AddressPicker(input, async () =>
    {
        var local = (stakeData?.Items ?? []).Where(s => s.Validator.Length == 42).Select(s => new AddressChoice(s.Validator, s.ValidatorName)).ToList();
        if (network.ChainId != 75 || !automaticLoading) return local.DistinctBy(v => v.Address, StringComparer.OrdinalIgnoreCase).ToArray();
        using var catalog = new AssetCatalogReader(network);
        var result = await catalog.ValidatorsAsync();
        local.AddRange(result.Items.Select(v => new AddressChoice(v.Address, v.Name)));
        if (!result.Complete) status.Text = result.Notice ?? "Каталог валидаторов может быть неполным. Можно ввести адрес вручную.";
        return local.DistinctBy(v => v.Address, StringComparer.OrdinalIgnoreCase).OrderBy(v => v.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }, "Выберите валидатора", portfolioRefresh: false);
}
