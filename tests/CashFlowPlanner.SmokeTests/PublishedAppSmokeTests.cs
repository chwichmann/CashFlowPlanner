using Microsoft.Playwright;

namespace CashFlowPlanner.SmokeTests;

/// <summary>
/// Loads the <em>published</em> app in a real browser and checks it actually runs.
///
/// <para>
/// This exists because of two production failures that a green build and a full unit suite both
/// missed:
/// </para>
/// <list type="number">
/// <item>
/// Removing <c>BlazorWebAssemblyLoadAllGlobalizationData</c> to save ~1 MB shipped a build that
/// reached 100% and then died before rendering, because the app sets its culture after the
/// runtime starts. Compilation succeeded, 353 tests passed, the German resource assemblies were
/// byte-identical - and the site was blank.
/// </item>
/// <item>
/// The bundled example plan spelled a person's field <c>"name"</c> where the model serialises
/// <c>"displayName"</c>. Unknown JSON fields are deliberately preserved, so the file validated,
/// round-tripped and simulated perfectly while rendering a blank row.
/// </item>
/// </list>
///
/// <para>
/// Neither is reachable from a unit test: the first needs the trimmed publish output, and the
/// second needs something to actually look at the screen. Both are caught here in seconds.
/// </para>
/// </summary>
[Trait("Category", "Smoke")]
public sealed class PublishedAppSmokeTests : IAsyncLifetime
{
    private StaticSiteServer? _server;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>Set when a prerequisite is missing. See <see cref="RequirePrerequisites"/>.</summary>
    private string? _skipReason;

    /// <summary>
    /// In CI these must actually run; locally they are optional.
    ///
    /// A test that quietly passes because it never ran is the same trap as the shadow test file
    /// this project already deleted once - it reads as coverage and is not. So when CI sets
    /// CFP_SMOKE_REQUIRED, a missing published output or browser is a hard failure; on a
    /// developer machine it is a no-op with the reason printed.
    /// </summary>
    private static bool SmokeIsRequired =>
        Environment.GetEnvironmentVariable("CFP_SMOKE_REQUIRED") == "1";

    private bool RequirePrerequisites()
    {
        if (_skipReason is null)
        {
            return true;
        }

        Assert.False(SmokeIsRequired, $"Smoke tests were required but could not run: {_skipReason}");

        Console.WriteLine($"[smoke] skipped: {_skipReason}");

        return false;
    }

    public async Task InitializeAsync()
    {
        var wwwroot = StaticSiteServer.FindPublishedWwwroot();

        if (wwwroot is null)
        {
            _skipReason =
                "CFP_PUBLISH_WWWROOT is not set. Publish first, then point it at the published "
                + "wwwroot: dotnet publish src/CashFlowPlanner.BlazorWasm -c Release -o /tmp/pub "
                + "&& CFP_PUBLISH_WWWROOT=/tmp/pub/wwwroot dotnet test tests/CashFlowPlanner.SmokeTests";
            return;
        }

        _server = new StaticSiteServer(wwwroot);

        if (!await _server.RespondsAsync())
        {
            _skipReason =
                $"The test web server at {_server.BaseUrl} did not answer. Nothing can be "
                + "verified, so this is a failure rather than a slow run.";
            return;
        }

        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,

                // Required on most Linux CI images. Without --no-sandbox Chromium cannot
                // start in an unprivileged container, and the failure presents as a hang
                // rather than an error. --disable-dev-shm-usage avoids the small /dev/shm
                // those images give you, which crashes the renderer on a page this size.
                Args = ["--no-sandbox", "--disable-dev-shm-usage"]
            });
        }
        catch (PlaywrightException ex)
        {
            _skipReason =
                $"Playwright's browser is not installed ({ex.Message}). Run `pwsh bin/Debug/net10.0/playwright.ps1 install chromium`.";
        }
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        _server?.Dispose();
    }

    /// <summary>
    /// A page with the service worker disabled.
    ///
    /// The worker is not what these tests check - it has its own verification - and letting
    /// it run makes them slow and racy: on first load it precaches every asset, so a
    /// NetworkIdle wait may never settle, and a second load may be served from its cache
    /// rather than from the artifact under test.
    /// </summary>
    private async Task<IPage> NewPageAsync(int? width = null)
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ServiceWorkers = ServiceWorkerPolicy.Block,
            ViewportSize = width is null
                ? null
                : new ViewportSize { Width = width.Value, Height = 800 }
        });

        return await context.NewPageAsync();
    }

    private async Task<(IPage Page, List<string> Errors)> OpenAsync(string path = "")
    {
        var page = await NewPageAsync();
        var errors = new List<string>();

        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };

        page.PageError += (_, error) => errors.Add(error);

        await page.GotoAsync(_server!.BaseUrl + path, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });

        return (page, errors);
    }

    [Fact]
    public async Task ThePublishedApp_Boots_AndRenders()
    {
        if (!RequirePrerequisites())
        {
            return;
        }

        var (page, errors) = await OpenAsync();

        // The heading only exists once Blazor has started and rendered a routed page. This is
        // the assertion that the ICU regression would have failed.
        await page.WaitForSelectorAsync("h1", new PageWaitForSelectorOptions { Timeout = 30_000 });

        var heading = await page.InnerTextAsync("h1");

        Assert.False(string.IsNullOrWhiteSpace(heading), "the app rendered no heading");

        // Blazor's unhandled-error strip. Visible means the app died after starting, which is
        // exactly how the culture failure presented.
        var errorUiVisible = await page.EvalOnSelectorAsync<bool>(
            "#blazor-error-ui", "el => getComputedStyle(el).display !== 'none'");

        Assert.False(errorUiVisible, $"Blazor reported an unhandled error. Console: {string.Join(" | ", errors)}");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ADeepLink_Boots_ThroughTheSpaFallback()
    {
        if (!RequirePrerequisites())
        {
            return;
        }

        // GitHub Pages serves 404.html for an unknown path; a deep link must still start the app
        // rather than showing a not-found page.
        var (page, errors) = await OpenAsync("transactions");

        await page.WaitForSelectorAsync("h1", new PageWaitForSelectorOptions { Timeout = 30_000 });

        Assert.Empty(errors);
    }

    [Fact]
    public async Task TheBundledExample_Loads_AndShowsItsData()
    {
        if (!RequirePrerequisites())
        {
            return;
        }

        var (page, errors) = await OpenAsync();

        await page.WaitForSelectorAsync("h1", new PageWaitForSelectorOptions { Timeout = 30_000 });

        // The empty state offers create / open / example. Take the example, which is the path a
        // first-time user takes and the one that shipped a plan with a blank person on it.
        var sample = page.Locator("button", new PageLocatorOptions
        {
            HasTextRegex = new System.Text.RegularExpressions.Regex("Beispiel laden|Load example")
        });

        await sample.First.ClickAsync(new LocatorClickOptions { Timeout = 30_000 });

        // The plan name appears in the navigation bar once a plan is loaded.
        await page.WaitForSelectorAsync(".navbar-plan-name", new PageWaitForSelectorOptions
        {
            Timeout = 30_000
        });

        var planName = await page.InnerTextAsync(".navbar-plan-name");

        Assert.Contains("Starter", planName);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task EveryEntityInTheExample_RendersAName()
    {
        if (!RequirePrerequisites())
        {
            return;
        }

        var (page, errors) = await OpenAsync();

        await page.WaitForSelectorAsync("h1", new PageWaitForSelectorOptions { Timeout = 30_000 });

        var sample = page.Locator("button", new PageLocatorOptions
        {
            HasTextRegex = new System.Text.RegularExpressions.Regex("Beispiel laden|Load example")
        });

        await sample.First.ClickAsync(new LocatorClickOptions { Timeout = 30_000 });
        await page.WaitForSelectorAsync(".navbar-plan-name", new PageWaitForSelectorOptions { Timeout = 30_000 });

        // The person in the bundled plan once rendered as an empty row, because the JSON spelled
        // the field "name" and the model serialises "displayName". Nothing but looking at the
        // screen catches that class of mistake.
        await page.GotoAsync(_server!.BaseUrl + "persons", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });

        await page.WaitForSelectorAsync("table tbody tr", new PageWaitForSelectorOptions { Timeout = 30_000 });

        var firstCell = await page.InnerTextAsync("table tbody tr td");

        Assert.False(
            string.IsNullOrWhiteSpace(firstCell),
            "the example's person rendered without a name - check the JSON property spelling");

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(1920)]
    [InlineData(1600)]
    [InlineData(1440)]
    [InlineData(1366)]
    [InlineData(1280)]   // the width this was broken at
    [InlineData(1180)]
    [InlineData(1024)]
    [InlineData(992)]    // Bootstrap's lg breakpoint; below this the navbar collapses
    public async Task TheSaveIndicator_StaysVisible_AtEveryDesktopWidth(int width)
    {
        if (!RequirePrerequisites())
        {
            return;
        }

        // The navigation row holds twelve destinations, the plan name and the save badge.
        // When the status block was allowed to shrink without a floor, the list took all the
        // space and the block collapsed to width 0 - so on an ordinary 1280px laptop the plan
        // name and, far worse, the export-needed / saved badge were simply not on screen.
        //
        // That badge is the data-loss indicator. It may shrink; it must never disappear.
        var page = await NewPageAsync(width);

        await page.GotoAsync(_server!.BaseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });

        await page.WaitForSelectorAsync("h1", new PageWaitForSelectorOptions { Timeout = 30_000 });

        var sample = page.Locator("button", new PageLocatorOptions
        {
            HasTextRegex = new System.Text.RegularExpressions.Regex("Beispiel laden|Load example")
        });

        await sample.First.ClickAsync(new LocatorClickOptions { Timeout = 30_000 });

        await page.WaitForSelectorAsync(".navbar-plan-status", new PageWaitForSelectorOptions
        {
            Timeout = 30_000
        });

        var statusWidth = await page.EvalOnSelectorAsync<int>(
            ".navbar-plan-status",
            "el => Math.round(el.getBoundingClientRect().width)");

        Assert.True(
            statusWidth > 100,
            $"at {width}px the plan status is {statusWidth}px wide - the save indicator is not usable");

        await page.CloseAsync();
    }
}
