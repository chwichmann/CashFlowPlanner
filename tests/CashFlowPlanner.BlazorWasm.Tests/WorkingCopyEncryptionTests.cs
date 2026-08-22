using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Storage.Json;
using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// The browser working copy is encrypted at rest with a device key, so that someone holding the
/// browser profile - a lost laptop, a synced backup, a shared machine - cannot read the household's
/// balances out of localStorage with a text editor. It protects nothing against script running on
/// the origin, and these tests do not pretend otherwise.
/// <para>
/// The real AES-GCM is unreachable from xUnit (no Web Crypto, no IndexedDB), so these tests drive
/// <see cref="BrowserPlanCacheService"/> over a fake browser and a stand-in cipher, and prove the
/// wiring: what reaches storage, what comes back, and what happens when the crypto is not there.
/// The cryptography itself is exercised in a browser by
/// <c>tools/working-copy-crypto-selftest.html</c>.
/// </para>
/// </summary>
public sealed class WorkingCopyEncryptionTests
{
    private const string PlanJsonKey = "cashflowplanner.currentPlanJson";
    private const string PreviousPlanJsonKey = "cashflowplanner.currentPlanJson.prev";
    private const string CachedAtKey = "cashflowplanner.currentPlanCachedAt";

    private const string PlanName = "Test Plan";
    private const string Salary = "87654.32";

    private static string PlanJson(string accountName = "Main Account")
    {
        return $$"""
            {"name":"{{PlanName}}","accounts":[{"name":"{{accountName}}","balance":{{Salary}}}]}
            """;
    }

    private static (BrowserPlanCacheService Cache,
        FakeLocalStorageJsRuntime Browser,
        FakeWorkingCopyCipher Cipher) CreateSubject()
    {
        var browser = new FakeLocalStorageJsRuntime();
        var cipher = new FakeWorkingCopyCipher();

        return (new BrowserPlanCacheService(browser, cipher), browser, cipher);
    }

    [Fact]
    public async Task WorkingCopy_Should_RoundTripThroughEncryption()
    {
        // Arrange
        var (cache, _, _) = CreateSubject();
        var json = PlanJson();

        // Act
        var write = await cache.SaveAsync(json);

        // Assert
        Assert.True(write.Success);
        Assert.Equal(json, await cache.LoadAsync());
    }

    [Fact]
    public async Task StoredValue_Should_NotContainRecognisablePlanContent()
    {
        // Arrange
        var (cache, browser, _) = CreateSubject();

        // Act
        await cache.SaveAsync(PlanJson());

        var stored = browser.Items[PlanJsonKey];

        // Assert - the point of the whole exercise: `strings` over the profile finds nothing.
        Assert.StartsWith(WorkingCopyEnvelope.Prefix, stored, StringComparison.Ordinal);
        Assert.DoesNotContain(PlanName, stored, StringComparison.Ordinal);
        Assert.DoesNotContain(Salary, stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Main Account", stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CachedAtTimestamp_Should_StayReadable()
    {
        // It is a wall-clock time that reveals nothing about the finances, and the navbar has to
        // show it even when the key store is sick.
        var (cache, browser, _) = CreateSubject();

        await cache.SaveAsync(PlanJson());

        Assert.False(WorkingCopyEnvelope.IsEnvelope(browser.Items[CachedAtKey]));
        Assert.NotNull(await cache.GetCachedAtAsync());
    }

    [Fact]
    public async Task PlaintextWorkingCopy_Should_BeRestoredAndUpgradedInPlace()
    {
        // Arrange - a returning user, whose localStorage still holds the plan the old way.
        var (cache, browser, _) = CreateSubject();
        var json = PlanJson();

        browser.Items[PlanJsonKey] = json;

        // Act
        var restored = await cache.LoadAsync();

        // Assert - nothing is lost, and the plaintext does not survive the visit.
        Assert.Equal(json, restored);
        Assert.StartsWith(
            WorkingCopyEnvelope.Prefix,
            browser.Items[PlanJsonKey],
            StringComparison.Ordinal);
        Assert.DoesNotContain(PlanName, browser.Items[PlanJsonKey], StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlaintextWorkingCopy_Should_BeRewrittenEncryptedOnTheNextSave()
    {
        // Arrange - the same returning user, but the first thing that happens is an edit rather
        // than a read, so the upgrade has to happen on the write path too.
        var (cache, browser, _) = CreateSubject();

        browser.Items[PlanJsonKey] = PlanJson();

        // Act
        var edited = PlanJson("Renamed Account");

        await cache.SaveAsync(edited);

        // Assert
        Assert.Equal(edited, await cache.LoadAsync());
        Assert.StartsWith(
            WorkingCopyEnvelope.Prefix,
            browser.Items[PlanJsonKey],
            StringComparison.Ordinal);

        // The plaintext it displaced is the recovery copy, and it is encrypted too.
        Assert.Equal(PlanJson(), await cache.LoadPreviousAsync());
        Assert.StartsWith(
            WorkingCopyEnvelope.Prefix,
            browser.Items[PreviousPlanJsonKey],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlaintextWorkingCopy_Should_SurviveWhenCryptoIsUnavailable()
    {
        // The upgrade must not be a chance to lose the plan on a browser that cannot encrypt.
        var (cache, browser, cipher) = CreateSubject();

        cipher.CryptoUnavailable = true;
        browser.Items[PlanJsonKey] = PlanJson();

        Assert.Equal(PlanJson(), await cache.LoadAsync());
        Assert.Equal(PlanJson(), browser.Items[PlanJsonKey]);
    }

    [Fact]
    public async Task CryptoUnavailable_Should_WritePlaintextRatherThanNothing()
    {
        // Arrange - IndexedDB blocked, private mode, a browser that simply refuses.
        var (cache, browser, cipher) = CreateSubject();

        cipher.CryptoUnavailable = true;

        // Act
        var write = await cache.SaveAsync(PlanJson());

        // Assert - silent data loss is a worse outcome than plaintext (finding P1b). The user's
        // afternoon survives; the console carries the warning.
        Assert.True(write.Success);
        Assert.Equal(PlanJson(), browser.Items[PlanJsonKey]);
        Assert.Equal(PlanJson(), await cache.LoadAsync());
    }

    [Fact]
    public async Task LostDeviceKey_Should_ReportNoWorkingCopyRatherThanThrow()
    {
        // Arrange - the user cleared site data, which takes IndexedDB with it, but localStorage
        // survived. The envelope is now permanently unreadable.
        var (cache, browser, cipher) = CreateSubject();

        await cache.SaveAsync(PlanJson());

        cipher.DeviceKeyLost = true;

        // Act
        var restored = await cache.LoadAsync();

        // Assert - "there is no working copy" is a normal state: the plan file is the source of
        // truth, and the app falls back to it.
        Assert.Null(restored);
        Assert.Null(await cache.LoadPreviousAsync());

        // And the app keeps working: the next save replaces the unreadable value.
        cipher.DeviceKeyLost = false;

        await cache.SaveAsync(PlanJson("Recovered Account"));

        Assert.Equal(PlanJson("Recovered Account"), await cache.LoadAsync());
        Assert.DoesNotContain(PlanName, browser.Items[PlanJsonKey], StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviousGoodCopy_Should_RotateOnARealChange()
    {
        // Arrange
        var (cache, browser, _) = CreateSubject();

        await cache.SaveAsync(PlanJson());

        // Act
        await cache.SaveAsync(PlanJson("Renamed Account"));

        // Assert
        Assert.Equal(PlanJson("Renamed Account"), await cache.LoadAsync());
        Assert.Equal(PlanJson(), await cache.LoadPreviousAsync());
        Assert.DoesNotContain(
            "Renamed Account",
            browser.Items[PreviousPlanJsonKey],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviousGoodCopy_Should_NotRotateWhenNothingChanged()
    {
        // A fresh IV per write means two saves of an identical plan produce different ciphertext.
        // If the change test compared ciphertext it would rotate every time and throw the only
        // recovery point away.
        var (cache, _, _) = CreateSubject();

        await cache.SaveAsync(PlanJson());
        await cache.SaveAsync(PlanJson("Renamed Account"));
        await cache.SaveAsync(PlanJson("Renamed Account"));

        Assert.Equal(PlanJson(), await cache.LoadPreviousAsync());
    }

    [Fact]
    public async Task Clear_Should_RemoveEveryStoredValue()
    {
        var (cache, browser, _) = CreateSubject();

        await cache.SaveAsync(PlanJson());
        await cache.SaveAsync(PlanJson("Renamed Account"));

        await cache.ClearAsync();

        Assert.Empty(browser.Items);
    }

    [Fact]
    public async Task QuotaExceeded_Should_StillBeReportedThroughTheCipher()
    {
        // The encryption layer must not swallow the quota diagnostics that finding P1b added.
        var (cache, browser, _) = CreateSubject();

        browser.QuotaExceeded = true;

        var write = await cache.SaveAsync(PlanJson());

        Assert.False(write.Success);
        Assert.Equal(PlanCacheWriteFailure.QuotaExceeded, write.Failure);
    }

    /// <summary>
    /// End to end through the real coordinator: a returning user with a plaintext plan in
    /// localStorage gets it back, and it is encrypted by the time they have done anything.
    /// </summary>
    [Fact]
    public async Task ReturningUser_Should_KeepTheirPlanAndGetItEncrypted()
    {
        // Arrange - session one, before this feature existed.
        var browser = new FakeLocalStorageJsRuntime();
        var cipher = new FakeWorkingCopyCipher { CryptoUnavailable = true };

        var oldState = new CashFlowAppState();

        var oldCoordinator = new PlanCacheCoordinator(
            oldState,
            new CashFlowPlanJsonSerializer(),
            new BrowserPlanCacheService(browser, cipher),
            new UiFeedbackService(),
            TimeSpan.Zero);

        await oldCoordinator.InitializeAsync();

        oldState.SetPlan(AppStateTestPlanFactory.CreatePlan());

        await oldCoordinator.SaveCurrentPlanAsync();

        oldCoordinator.Dispose();

        Assert.Contains(PlanName, browser.Items[PlanJsonKey], StringComparison.Ordinal);

        // Act - session two, on a build that encrypts.
        cipher.CryptoUnavailable = false;

        var newState = new CashFlowAppState();

        var newCoordinator = new PlanCacheCoordinator(
            newState,
            new CashFlowPlanJsonSerializer(),
            new BrowserPlanCacheService(browser, cipher),
            new UiFeedbackService(),
            TimeSpan.Zero);

        await newCoordinator.InitializeAsync();

        // Assert - the plan came back...
        Assert.NotNull(newState.CurrentPlan);
        Assert.Equal(PlanName, newState.CurrentPlan!.Name);
        Assert.Equal(3, newState.CurrentPlan.Accounts.Count);

        // ...and it is no longer readable on disk.
        Assert.StartsWith(
            WorkingCopyEnvelope.Prefix,
            browser.Items[PlanJsonKey],
            StringComparison.Ordinal);
        Assert.DoesNotContain(PlanName, browser.Items[PlanJsonKey], StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Main Account",
            browser.Items[PlanJsonKey],
            StringComparison.Ordinal);

        // And an edit still round-trips.
        var account = newState.CurrentPlan.Accounts
            .Single(x => x.Id == AppStateTestPlanFactory.MainAccountId);

        newState.AddOrUpdateAccount(new Account
        {
            Id = account.Id,
            Name = "Renamed Account",
            Type = account.Type,
            Currency = account.Currency,
            OpeningBalance = account.OpeningBalance,
            OpeningDate = account.OpeningDate
        });

        await newCoordinator.SaveCurrentPlanAsync();

        var reloadedState = new CashFlowAppState();

        var reloadingCoordinator = new PlanCacheCoordinator(
            reloadedState,
            new CashFlowPlanJsonSerializer(),
            new BrowserPlanCacheService(browser, cipher),
            new UiFeedbackService(),
            TimeSpan.Zero);

        await reloadingCoordinator.InitializeAsync();

        Assert.Contains(reloadedState.CurrentPlan!.Accounts, x => x.Name == "Renamed Account");

        newCoordinator.Dispose();
        reloadingCoordinator.Dispose();
    }

    /// <summary>
    /// The real <see cref="WorkingCopyCipher"/> against a browser that is not there. This is the
    /// only part of the production cipher xUnit can reach, and it is the part that matters most:
    /// what it does when JavaScript is unavailable.
    /// </summary>
    public sealed class WhenJavaScriptIsUnavailable
    {
        [Fact]
        public async Task Protect_Should_ReturnThePlaintext()
        {
            var cipher = new WorkingCopyCipher(new ThrowingJsRuntime());

            Assert.Equal(PlanJson(), await cipher.ProtectAsync(PlanJson()));
            Assert.True(cipher.IsPlaintextFallbackActive);
        }

        [Fact]
        public async Task Unprotect_Should_PassPlaintextThroughWithoutTouchingJavaScript()
        {
            var cipher = new WorkingCopyCipher(new ThrowingJsRuntime());

            // Migration has to work even in a session where crypto is broken.
            Assert.Equal(PlanJson(), await cipher.UnprotectAsync(PlanJson()));
            Assert.False(cipher.IsPlaintextFallbackActive);
        }

        [Fact]
        public async Task Unprotect_Should_ReturnNullForAnEnvelopeItCannotOpen()
        {
            var cipher = new WorkingCopyCipher(new ThrowingJsRuntime());

            Assert.Null(await cipher.UnprotectAsync(WorkingCopyEnvelope.Prefix + "abc:def"));
        }

        [Fact]
        public async Task Cache_Should_StillStoreThePlan()
        {
            // The whole stack over a dead browser: still no data loss.
            var browser = new FakeLocalStorageJsRuntime();

            var cache = new BrowserPlanCacheService(
                browser,
                new WorkingCopyCipher(new ThrowingJsRuntime()));

            var write = await cache.SaveAsync(PlanJson());

            Assert.True(write.Success);
            Assert.Equal(PlanJson(), await cache.LoadAsync());
        }

        /// <summary>
        /// Every interop call fails, the way it does when the module cannot be imported.
        /// </summary>
        private sealed class ThrowingJsRuntime : IJSRuntime
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
                throw new JSException($"JavaScript is not available: {identifier}");

            public ValueTask<TValue> InvokeAsync<TValue>(
                string identifier, CancellationToken cancellationToken, object?[]? args) =>
                throw new JSException($"JavaScript is not available: {identifier}");
        }
    }
}
