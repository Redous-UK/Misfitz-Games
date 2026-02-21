using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Text;

namespace Misfitz_Games.Controllers.Admin;

[ApiController]
public sealed class AdminSiteController : ControllerBase
{
    // TODO: replace with your existing admin auth policy/role
    // If you already protect /admin routes via cookie auth + admin check, mirror that here.

    public sealed class SiteEntry
    {
        public required string Name { get; init; }
        public required string Type { get; init; } // "dir" or "file"
        public long? Size { get; init; }           // null for folders
        public DateTime UpdatedUtc { get; init; }
    }

    [Authorize]
    [HttpGet("/admin/site/list")]
    public IActionResult List([FromQuery] string? path, [FromServices] IWebHostEnvironment env, [FromServices] IConfiguration config)
    {
        var root = GetSiteRootPath(env, config);
        var rel = NormalizeRelPath(path);
        var abs = ResolveUnderRoot(root, rel);

        if (!Directory.Exists(abs))
            return NotFound(new { ok = false, error = "Folder not found", path = rel });

        var dirs = Directory.EnumerateDirectories(abs)
           .Select(d => new DirectoryInfo(d))
           .Select(di => new SiteEntry
           {
               Name = di.Name,
               Type = "dir",
               Size = null,
               UpdatedUtc = di.LastWriteTimeUtc
           });

        var files = Directory.EnumerateFiles(abs)
            .Select(f => new FileInfo(f))
            .Select(fi => new SiteEntry
            {
                Name = fi.Name,
                Type = "file",
                Size = fi.Length,
                UpdatedUtc = fi.LastWriteTimeUtc
            });

        var entries = dirs
            .Concat(files)
            .OrderBy(e => e.Type == "dir" ? 0 : 1)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new
        {
            ok = true,
            path = rel,
            entries
        });
    }

    [Authorize]
    [HttpGet("/admin/site/read")]
    public IActionResult Read([FromQuery] string? path, [FromServices] IWebHostEnvironment env, [FromServices] IConfiguration config)
    {
        var root = GetSiteRootPath(env, config);
        var rel = NormalizeRelPath(path);
        var abs = ResolveUnderRoot(root, rel);

        if (!System.IO.File.Exists(abs))
            return NotFound(new { ok = false, error = "File not found", path = rel });

        // Optional: protect against huge files
        var fi = new FileInfo(abs);
        const long maxBytes = 2_000_000; // 2MB
        if (fi.Length > maxBytes)
            return BadRequest(new { ok = false, error = "File too large for editor", path = rel, size = fi.Length, maxBytes });

        var content = System.IO.File.ReadAllText(abs, Encoding.UTF8);
        return Ok(new { ok = true, path = rel, content });
    }

    private static string GetSiteRootPath(IWebHostEnvironment env, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(env);
        // Prefer your existing config key if you already have one.
        // Common patterns: "SITE_ROOT", "SITE_PATH", "DATA_SITE_PATH"
        var configured = config["SITE_ROOT"] ?? config["SITE_PATH"] ?? config["DATA_SITE_PATH"];

        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        // Default to Render persistent disk path. In local dev you likely have /data too.
        // If you already seed to /data/site, keep this consistent.
        return Path.GetFullPath("/data/site");
    }

    private static string NormalizeRelPath(string? path)
    {
        // Treat null/blank as root
        var p = (path ?? "").Trim();

        // Remove leading slashes so we treat it as relative
        p = p.TrimStart('/', '\\');

        // Normalize separators to OS separator
        p = p.Replace('\\', '/');

        // Disallow traversal tokens
        if (p.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid path.");

        // Collapse multiple slashes
        while (p.Contains("//", StringComparison.Ordinal))
            p = p.Replace("//", "/", StringComparison.Ordinal);

        return p;
    }

    private static string ResolveUnderRoot(string root, string rel)
    {
        var abs = Path.GetFullPath(Path.Combine(root, rel));

        // Ensure abs is under root
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                      + Path.DirectorySeparatorChar;

        if (!abs.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(abs.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path escapes site root.");

        return abs;
    }
}