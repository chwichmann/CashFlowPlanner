using System.Net;
using System.Text;

namespace CashFlowPlanner.SmokeTests;

/// <summary>
/// Serves a published <c>wwwroot</c> over HTTP for the duration of a test.
/// <para>
/// Deliberately a bare <see cref="HttpListener"/> rather than a Kestrel host: this exists to
/// prove the published bytes work, so the fewer moving parts between the artifact and the
/// browser the better. It also mirrors GitHub Pages closely - static files, an SPA fallback to
/// <c>index.html</c>, and no server-side anything.
/// </para>
/// <para>
/// Content types matter more than they look. A Blazor app whose <c>.wasm</c> is served as
/// <c>application/octet-stream</c> fails to start, so getting these wrong would make the smoke
/// test fail for a reason that has nothing to do with the app.
/// </para>
/// </summary>
public sealed class StaticSiteServer : IDisposable
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".webmanifest"] = "application/manifest+json; charset=utf-8",
        [".wasm"] = "application/wasm",
        [".dat"] = "application/octet-stream",
        [".blat"] = "application/octet-stream",
        [".pdb"] = "application/octet-stream",
        [".dll"] = "application/octet-stream",
        [".png"] = "image/png",
        [".ico"] = "image/x-icon",
        [".svg"] = "image/svg+xml",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".txt"] = "text/plain; charset=utf-8"
    };

    private readonly HttpListener _listener = new();
    private readonly string _root;
    private readonly CancellationTokenSource _cts = new();

    public StaticSiteServer(string wwwrootPath, int port)
    {
        _root = Path.GetFullPath(wwwrootPath);
        BaseUrl = $"http://127.0.0.1:{port}/";

        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();

        _ = Task.Run(() => LoopAsync(_cts.Token));
    }

    public string BaseUrl { get; }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                Serve(context);
            }
            catch (Exception)
            {
                // A failed response must not take the listener down; the assertion in the test
                // is what should report the problem.
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch (Exception)
                {
                    // The client is already gone.
                }
            }
        }
    }

    private void Serve(HttpListenerContext context)
    {
        var relative = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath).TrimStart('/');

        if (relative.Length == 0)
        {
            relative = "index.html";
        }

        var path = Path.GetFullPath(Path.Combine(_root, relative));

        // Never serve outside the published root, even if a test asks for it.
        if (!path.StartsWith(_root, StringComparison.Ordinal))
        {
            context.Response.StatusCode = 403;
            context.Response.Close();
            return;
        }

        if (!File.Exists(path))
        {
            // The SPA fallback GitHub Pages provides via 404.html: a deep link must still boot
            // the app rather than 404.
            path = Path.Combine(_root, "index.html");

            if (!File.Exists(path))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }
        }

        var bytes = File.ReadAllBytes(path);
        var extension = Path.GetExtension(path);

        context.Response.ContentType = ContentTypes.TryGetValue(extension, out var type)
            ? type
            : "application/octet-stream";

        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (Exception)
        {
            // Already torn down.
        }

        _cts.Dispose();
    }

    /// <summary>
    /// Finds the published wwwroot, or null when there is none - the tests skip rather than fail
    /// in that case, because "nobody published" is not a defect in the app.
    /// </summary>
    public static string? FindPublishedWwwroot()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("CFP_PUBLISH_WWWROOT");

        if (!string.IsNullOrWhiteSpace(fromEnvironment) && Directory.Exists(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src", "CashFlowPlanner.BlazorWasm", "bin", "Release", "net10.0", "publish", "wwwroot");

            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
