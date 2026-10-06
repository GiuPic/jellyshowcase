using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.JellyBridge.Services;

/// <summary>
/// Adds the JellyShowcase client script (the "Richiedi" button) to the web client's index.html at
/// request time, without touching jellyfin-web on disk. Same approach as Jellyfin Enhanced:
/// buffer the uncompressed index response and insert a script tag before &lt;/body&gt;.
/// </summary>
public class ShowcaseScriptStartupFilter : IStartupFilter
{
    private const string ScriptPath = "/JellyShowcase/client.js";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(InvokeAsync);
            next(app);
        };
    }

    private static async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || !IsIndexRequest(context.Request.Path.Value))
        {
            await next();
            return;
        }

        // Plain, complete 200 response: no compression, no partial content.
        context.Request.Headers.Remove("Accept-Encoding");
        context.Request.Headers.Remove("Range");
        context.Request.Headers.Remove("If-Range");

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next();
        }
        catch
        {
            context.Response.Body = originalBody;
            throw;
        }
        context.Response.Body = originalBody;
        buffer.Seek(0, SeekOrigin.Begin);

        var isHtml = context.Response.StatusCode == 200
            && (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) ?? false);
        if (!isHtml)
        {
            await buffer.CopyToAsync(originalBody);
            return;
        }

        var html = Encoding.UTF8.GetString(buffer.ToArray());
        var bodyClose = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyClose >= 0 && html.IndexOf(ScriptPath, StringComparison.OrdinalIgnoreCase) < 0)
        {
            var version = typeof(Plugin).Assembly.GetName().Version;
            html = html.Substring(0, bodyClose)
                + $"<script plugin=\"JellyShowcase\" src=\"..{ScriptPath}?v={version}\" defer></script>\n"
                + html.Substring(bodyClose);
        }

        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html;charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        context.Response.Headers.Remove("ETag");
        context.Response.Headers.Remove("Last-Modified");
        context.Response.Headers.Remove("Accept-Ranges");
        await originalBody.WriteAsync(bytes, 0, bytes.Length);
    }

    private static bool IsIndexRequest(string? path)
        => !string.IsNullOrEmpty(path)
            && (path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web", StringComparison.OrdinalIgnoreCase));
}
