using System.Globalization;
using CashFlowPlanner.BlazorWasm;
using CashFlowPlanner.BlazorWasm.Models;
using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.BlazorWasm.Services.BankImport;
using CashFlowPlanner.Core.Banking.Import;
using CashFlowPlanner.Core.Pillar3a;
using CashFlowPlanner.Storage.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");


builder.Services.AddLocalization();
builder.Services.AddScoped<BrowserCultureService>();

// Stateless and culture-agnostic at construction time - it reads CultureInfo.CurrentCulture on
// every call, so a singleton is safe even though the culture is assigned after the host is built.
builder.Services.AddSingleton<AppFormatter>();

builder.Services.AddSingleton<CashFlowAppState>();
builder.Services.AddSingleton<CashFlowPlanJsonSerializer>();
builder.Services.AddSingleton<StarterPlanProvider>();
builder.Services.AddSingleton<PlanCryptoService>();
builder.Services.AddSingleton<PassphrasePromptService>();
builder.Services.AddSingleton<PlanExportPreferences>();
builder.Services.AddSingleton<PlanFileService>();
builder.Services.AddSingleton<DiskAutoSaveService>();
builder.Services.AddSingleton<IDiskAutoSave>(sp => sp.GetRequiredService<DiskAutoSaveService>());
builder.Services.AddSingleton<DiskAutoSaveCoordinator>();
builder.Services.AddSingleton<DashboardSummaryService>();
builder.Services.AddSingleton<MonthlyCashflowSummaryService>();

// Encrypts the localStorage working copy with a device key. Registered as a concrete singleton so
// the same instance - and therefore the same imported JS module and the same one-time fallback
// warning - is shared by everything that stores a working copy.
builder.Services.AddSingleton<WorkingCopyCipher>();
builder.Services.AddSingleton<IWorkingCopyCipher>(sp => sp.GetRequiredService<WorkingCopyCipher>());
builder.Services.AddSingleton<BrowserPlanCacheService>();
builder.Services.AddSingleton<IBrowserPlanCache>(sp => sp.GetRequiredService<BrowserPlanCacheService>());
builder.Services.AddSingleton<IUnsavedChangesGuard, BrowserUnsavedChangesGuard>();

// Constructed explicitly: PlanCacheCoordinator has a constructor taking the debounce window, which
// DI cannot supply, so the wiring is spelled out rather than left to constructor selection.
builder.Services.AddSingleton(sp => new PlanCacheCoordinator(
    sp.GetRequiredService<CashFlowAppState>(),
    sp.GetRequiredService<CashFlowPlanJsonSerializer>(),
    sp.GetRequiredService<IBrowserPlanCache>(),
    sp.GetRequiredService<UiFeedbackService>(),
    PlanCacheCoordinator.DefaultDebounceDelay,
    sp.GetRequiredService<IUnsavedChangesGuard>()));
builder.Services.AddScoped<EnumLocalizer>();
builder.Services.AddScoped<Pillar3aProjectionEngine>();
builder.Services.AddScoped<Pillar3aTaxYearSimulator>();

// Singleton, not scoped: PlanCacheCoordinator is a singleton and reports autosave failures
// through this service. Blazor WebAssembly only ever has one scope, so this is not a behaviour
// change for the components that inject it.
builder.Services.AddSingleton<UiFeedbackService>();
builder.Services.AddScoped<IBankImportStore, BankImportStoreLocalStorage>();
builder.Services.AddScoped<BankStatementImportService>();

var host = builder.Build();

await host.Services.GetRequiredService<PlanExportPreferences>().InitializeAsync();

var cultureService = host.Services.GetRequiredService<BrowserCultureService>();
var preferences = await cultureService.LoadAsync();

ApplyCulture(preferences);

await host.RunAsync();


static void ApplyCulture(CulturePreferences preferences)
{
    var formattingCulture = new CultureInfo(preferences.RegionCulture);
    var uiCulture = new CultureInfo(preferences.LanguageCulture);

    CultureInfo.DefaultThreadCurrentCulture = formattingCulture;
    CultureInfo.DefaultThreadCurrentUICulture = uiCulture;

    CultureInfo.CurrentCulture = formattingCulture;
    CultureInfo.CurrentUICulture = uiCulture;
}
