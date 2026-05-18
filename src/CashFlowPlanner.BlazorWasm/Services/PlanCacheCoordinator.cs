using CashFlowPlanner.Storage.Json;

namespace CashFlowPlanner.BlazorWasm.Services;

public sealed class PlanCacheCoordinator
{
    private readonly CashFlowAppState _appState;
    private readonly CashFlowPlanJsonSerializer _jsonSerializer;
    private readonly BrowserPlanCacheService _browserCache;

    private bool _initialized;
    private bool _isRestoring;
    private bool _isSaving;

    public PlanCacheCoordinator(
        CashFlowAppState appState,
        CashFlowPlanJsonSerializer jsonSerializer,
        BrowserPlanCacheService browserCache)
    {
        _appState = appState;
        _jsonSerializer = jsonSerializer;
        _browserCache = browserCache;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        _appState.Changed += OnAppStateChanged;

        await RestoreAsync();
    }

    private async Task RestoreAsync()
    {
        if (_appState.CurrentPlan is not null)
        {
            return;
        }

        var cachedJson = await _browserCache.LoadAsync();

        if (string.IsNullOrWhiteSpace(cachedJson))
        {
            return;
        }

        try
        {
            _isRestoring = true;

            var document = _jsonSerializer.DeserializeDocument(cachedJson);
            _appState.LoadDocument(document);
        }
        finally
        {
            _isRestoring = false;
        }
    }

    public async Task SaveCurrentPlanAsync()
    {
        if (_isRestoring || _isSaving)
        {
            return;
        }

        try
        {
            _isSaving = true;

            if (_appState.CurrentPlan is null)
            {
                await _browserCache.ClearAsync();
                return;
            }

            var document = _appState.GetDocumentForSave();
            var json = _jsonSerializer.SerializeDocument(document);

            await _browserCache.SaveAsync(json);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void OnAppStateChanged()
    {
        _ = SaveCurrentPlanAsync();
    }
}