using System.Net;
using System.Text;
using System.Text.Json;
using DCWallet.Core;
using Xunit;

namespace DCWallet.Tests;

public sealed class ReleaseUpdateClientTests
{
    const string Repo = "https://github.com/maxwell2010/DCWallet";
    static string Release(string version, bool draft = false, string? page = null, string? download = null) => JsonSerializer.Serialize(new
    {
        name = "DC WALLET " + version, draft, prerelease = true, tag_name = "alpha",
        html_url = page ?? Repo + "/releases/tag/alpha",
        assets = new[] { new { name = "DCWallet-win-x64.exe", browser_download_url = download ?? Repo + "/releases/download/alpha/DCWallet-win-x64.exe" } }
    });

    [Theory]
    [InlineData("0.2.0-alpha.3", "0.2.0-alpha.10", true)]
    [InlineData("0.2.0-alpha.10", "0.2.0-alpha.3", false)]
    [InlineData("0.2.0-alpha.3", "0.2.0-alpha.3", false)]
    [InlineData("0.2.0-alpha.3+old", "0.2.0-alpha.3+new", false)]
    [InlineData("0.2.0-alpha.3", "0.2.0", true)]
    [InlineData("0.2.0", "0.2.0-alpha.10", false)]
    [InlineData("0.2.0", "0.3.0-alpha.1", false)]
    [InlineData("0.2.0-alpha.3", "0.2.0-alpha.03", false)]
    [InlineData("alpha", "0.2.0-alpha.4", false)]
    [InlineData("0.2.0-alpha.3", "not-a-version", false)]
    public async Task NotifiesOnlyForStrictlyNewerVersion(string current, string available, bool expected)
    {
        var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(Release(available)) });
        using var http = new HttpClient(handler);
        var update = await new ReleaseUpdateClient(http).CheckAsync(current);
        Assert.Equal(expected, update is not null);
        if (update is not null)
        {
            Assert.Equal(current, update.CurrentVersion); Assert.Equal(available, update.NewVersion);
            Assert.Equal(Repo + "/releases/download/alpha/DCWallet-win-x64.exe", update.GetDownloadUri("win-x64")!.AbsoluteUri);
            Assert.Null(update.GetDownloadUri("unsupported"));
        }
    }

    [Theory]
    [InlineData(true, null, null)]
    [InlineData(false, "https://example.com/alpha", null)]
    [InlineData(false, null, "https://example.com/install.exe")]
    public async Task DraftOrUntrustedReleaseNeverOffersDownload(bool draft, string? page, string? download)
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(Release("0.2.0-alpha.4", draft, page, download)) }));
        Assert.Null(await new ReleaseUpdateClient(http).CheckAsync("0.2.0-alpha.3"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "")]
    [InlineData(HttpStatusCode.Forbidden, "rate limited")]
    [InlineData(HttpStatusCode.OK, "not JSON")]
    [InlineData(HttpStatusCode.OK, "{}")]
    public async Task UnavailableReleaseIsSilent(HttpStatusCode status, string body)
    {
        using var http = new HttpClient(new Handler(_ => new(status) { Content = new StringContent(body) }));
        Assert.Null(await new ReleaseUpdateClient(http).CheckAsync("0.2.0-alpha.3"));
    }

    [Fact]
    public async Task NetworkFailureIsSilentAndRequestUsesOnlyPublicFixedEndpoint()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Contains(request.RequestUri!.AbsoluteUri, new[] { "https://api.github.com/repos/maxwell2010/DCWallet/releases/latest", "https://api.github.com/repos/maxwell2010/DCWallet/releases/tags/alpha" });
            Assert.NotEmpty(request.Headers.UserAgent); Assert.Null(request.Headers.Authorization);
            throw new HttpRequestException("offline");
        }));
        Assert.Null(await new ReleaseUpdateClient(http).CheckAsync("0.2.0-alpha.3"));
    }

    [Fact]
    public async Task OversizedResponseIsSilent()
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(new string('a', 1048577), Encoding.UTF8) }));
        Assert.Null(await new ReleaseUpdateClient(http).CheckAsync("0.2.0-alpha.3"));
    }

    [Theory]
    [InlineData("0.2.0-alpha.3")]
    [InlineData("0.1.0")]
    public async Task StableReleaseIsPreferredWithVersionBoundTrustedAsset(string current)
    {
        var requests = new List<string>();
        var stable = Release("0.2.0").Replace("\"prerelease\":true", "\"prerelease\":false")
            .Replace("\"tag_name\":\"alpha\"", "\"tag_name\":\"v0.2.0\"")
            .Replace("/tag/alpha", "/tag/v0.2.0").Replace("/download/alpha/", "/download/v0.2.0/");
        using var http = new HttpClient(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.OK) { Content = new StringContent(stable) };
        }));
        var update = await new ReleaseUpdateClient(http).CheckAsync(current);
        Assert.NotNull(update); Assert.Equal("0.2.0", update.NewVersion);
        Assert.Equal(Repo + "/releases/download/v0.2.0/DCWallet-win-x64.exe", update.GetDownloadUri("win-x64")!.AbsoluteUri);
        Assert.Single(requests); Assert.EndsWith("/latest", requests[0]);
    }

    [Fact]
    public async Task StableInstallNeverQueriesAlphaEvenWhenLatestIsUnavailable()
    {
        var requests = new List<string>();
        using var http = new HttpClient(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.NotFound);
        }));
        Assert.Null(await new ReleaseUpdateClient(http).CheckAsync("0.2.0"));
        Assert.Single(requests); Assert.EndsWith("/latest", requests[0]);
    }

    [Fact]
    public async Task AlphaInstallFallsBackOnlyAfterStableProbe()
    {
        var requests = new List<string>();
        using var http = new HttpClient(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return request.RequestUri.AbsolutePath.EndsWith("/latest") ? new(HttpStatusCode.NotFound)
                : new(HttpStatusCode.OK) { Content = new StringContent(Release("0.2.0-alpha.4")) };
        }));
        Assert.NotNull(await new ReleaseUpdateClient(http).CheckAsync("0.2.0-alpha.3"));
        Assert.Equal(2, requests.Count); Assert.EndsWith("/latest", requests[0]); Assert.EndsWith("/tags/alpha", requests[1]);
    }

    sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
