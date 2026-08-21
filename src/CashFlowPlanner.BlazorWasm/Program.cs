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

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<CashFlowAppState>();
builder.Services.AddSingleton<CashFlowPlanJsonSerializer>();
builder.Services.AddSingleton<DashboardSummaryService>();
builder.Services.AddSingleton<MonthlyCashflowSummaryService>();
builder.Services.AddSingleton<BrowserPlanCacheService>();
builder.Services.AddSingleton<IBrowserPlanCache>(sp => sp.GetRequiredService<BrowserPlanCacheService>());
builder.Services.AddSingleton<PlanCacheCoordinator>();
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
