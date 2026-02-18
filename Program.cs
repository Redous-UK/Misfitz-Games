using Humanizer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Misfitz_Games.Data;
using Misfitz_Games.Hubs;
using Misfitz_Games.Services;
using Misfitz_Games.Services.Games.Contexto;
using Misfitz_Games.Services.Games.Hangman;
using Misfitz_Games.Services.Games.Trivia;
using Misfitz_Games.Services.Infrastructure.Redis;
using Misfitz_Games.Services.Room;
using Misfitz_Games.Services.Tuya;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Misfitz_Games;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSignalR(o =>
        {
            o.EnableDetailedErrors = true;
            o.KeepAliveInterval = TimeSpan.FromSeconds(10);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("default", p =>
                p.AllowAnyHeader()
                 .AllowAnyMethod()
                 .AllowCredentials()
                 .SetIsOriginAllowed(_ => true));

            options.AddPolicy("dev", p =>
                p.WithOrigins("http://localhost:5173", "http://localhost:8080")
                 .AllowAnyHeader()
                 .AllowAnyMethod()
                 .AllowCredentials());
        });

        // --- EF Core (SQLite) for user accounts ---
        var dbPath =
            builder.Configuration["DB_PATH"]
            ?? (builder.Environment.IsProduction() ? "/data/misfitz.db" : "Data/misfitz.db");

        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dbDir))
            Directory.CreateDirectory(dbDir);

        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}")
        );

        // --- Auth: Cookie auth ---
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "misfitz_auth";
                o.Cookie.HttpOnly = true;
                o.SlidingExpiration = true;

                // Render is HTTPS in production
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Lax;

                o.LoginPath = "/user.html";
                o.AccessDeniedPath = "/user.html";
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("Player", p =>
                p.RequireAssertion(ctx =>
                    ctx.User.HasClaim(ClaimTypes.Role, "guest") ||
                    ctx.User.HasClaim(ClaimTypes.Role, "member") ||
                    ctx.User.HasClaim(ClaimTypes.Role, "admin")))
            .AddPolicy("MemberOrAdmin", p =>
                p.RequireAssertion(ctx =>
                    ctx.User.HasClaim(ClaimTypes.Role, "member") ||
                    ctx.User.HasClaim(ClaimTypes.Role, "admin")))
            .AddPolicy("AdminOnly", p => p.RequireClaim(ClaimTypes.Role, "admin"));

        // Redis
        builder.Services.AddSingleton<RedisMuxFactory>();

        // App services
        builder.Services.AddSingleton<IRoomStateStore, RedisRoomStateStore>();
        builder.Services.AddSingleton<ContextoEngine>();
        builder.Services.AddSingleton<RoomBroadcastService>();
        builder.Services.AddSingleton<RoomGameBroadcaster>();


        // Game Services
        // Contexto
        builder.Services.AddSingleton<ContextoWordProvider>();
        builder.Services.AddSingleton<WordVectorStore>();
        builder.Services.AddSingleton<ContextoRankIndexStore>();
        // Hangman
        builder.Services.AddSingleton<HangmanService>();
        // Trivia
        builder.Services.AddHttpClient<TriviaService>();       

        // Effects / hardware services
        builder.Services.AddHttpClient<TuyaPlugService>();
        builder.Services.AddScoped<EffectsService>();
        builder.Services.AddScoped<EffectsEngine>();
        builder.Services.AddDataProtection();
        builder.Services.AddScoped<TuyaOAuthService>();

        // TikFinity / Spotify / Streamer
        builder.Services.AddSingleton<WebhookIngestService>();

        // Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

        var app = builder.Build();

        // ===================== Site roots =====================
        var dataRoot = "/data/site";
        var backupsRoot = "/data/backups";

        Directory.CreateDirectory(dataRoot);
        Directory.CreateDirectory(backupsRoot);

        // Seed packaged defaults -> /data/site when needed (fresh disk / missing essentials)
        var seedRoot = Path.Combine(app.Environment.ContentRootPath, "Data", "Site");

        // Wildcard-friendly requirements (prevents clobbering existing edits)
        BootstrapSite(seedRoot, dataRoot, requiredPatterns:
        [
            "*.html",
            "*.css",
            "*.js"
            // add "*.png" if you want, but not required for a working site
        ]);

        // ===================== Pipeline =====================
        app.UseRouting();
        app.UseCors("default");
        app.UseAuthentication();
        app.UseAuthorization();

        // Serve editable site from /data/site
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(dataRoot),
            RequestPath = "",
            OnPrepareResponse = ctx =>
            {
                // Helpful during frequent edits. Relax later.
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                ctx.Context.Response.Headers.Pragma = "no-cache";
                ctx.Context.Response.Headers.Expires = "0";
            }
        });

        app.MapControllers();
        app.MapHub<RoomHub>("/hubs/room");

        // ===================== Admin editor (uses existing cookie auth) =====================
        app.MapGet("/admin", () =>
            Results.Content(AdminEditorHtml(), "text/html; charset=utf-8")
        ).RequireAuthorization("AdminOnly");

        var adminApi = app.MapGroup("/admin/api")
            .RequireAuthorization("AdminOnly");

        adminApi.MapGet("/list", () =>
        {
            var files = ListFiles(dataRoot);
            return Results.Json(new { ok = true, root = dataRoot, files });
        });

        adminApi.MapGet("/read", (string path) =>
        {
            var full = SafeResolve(dataRoot, path);
            if (full is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });
            if (!File.Exists(full)) return Results.NotFound(new { ok = false, error = "Not found." });

            var bytes = File.ReadAllBytes(full);
            var text = TryDecode(bytes);

            return Results.Json(new { ok = true, path, content = text });
        });

        adminApi.MapPost("/save", async (HttpContext ctx) =>
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            var rel = doc.RootElement.GetProperty("path").GetString() ?? "";
            var content = doc.RootElement.GetProperty("content").GetString() ?? "";

            var full = SafeResolve(dataRoot, rel);
            if (full is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);

            // Backup existing before overwrite
            if (File.Exists(full))
                CreateBackup(backupsRoot, rel, File.ReadAllBytes(full));

            File.WriteAllText(full, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return Results.Json(new { ok = true, path = rel });
        });

        adminApi.MapPost("/upload", async (HttpContext ctx) =>
        {
            if (!ctx.Request.HasFormContentType)
                return Results.BadRequest(new { ok = false, error = "Expected multipart/form-data" });

            var form = await ctx.Request.ReadFormAsync();
            var relDir = form["dir"].ToString(); // can be "" for root
            var file = form.Files.GetFile("file");
            if (file is null) return Results.BadRequest(new { ok = false, error = "Missing file." });

            var safeDir = string.IsNullOrWhiteSpace(relDir) ? "" : relDir.Trim();
            var targetRel = Path.Combine(safeDir, file.FileName).Replace('\\', '/');

            var full = SafeResolve(dataRoot, targetRel);
            if (full is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);

            // Backup existing
            if (File.Exists(full))
                CreateBackup(backupsRoot, targetRel, File.ReadAllBytes(full));

            using var fs = File.Create(full);
            await file.CopyToAsync(fs);

            return Results.Json(new { ok = true, path = targetRel });
        });

        adminApi.MapPost("/delete", async (HttpContext ctx) =>
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            var rel = doc.RootElement.GetProperty("path").GetString() ?? "";

            var full = SafeResolve(dataRoot, rel);
            if (full is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            if (File.Exists(full))
            {
                CreateBackup(backupsRoot, rel, File.ReadAllBytes(full));
                File.Delete(full);
                return Results.Json(new { ok = true });
            }

            if (Directory.Exists(full))
            {
                Directory.Delete(full, recursive: true);
                return Results.Json(new { ok = true });
            }

            return Results.NotFound(new { ok = false, error = "Not found." });
        });

        adminApi.MapGet("/backups", (string path) =>
        {
            var safe = SafeResolve(backupsRoot, BackupKey(path));
            if (safe is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            if (!Directory.Exists(safe))
                return Results.Json(new { ok = true, items = Array.Empty<string>() });

            var items = Directory.EnumerateFiles(safe, "*.bak")
                .Select(Path.GetFileName)
                .OrderByDescending(x => x)
                .ToArray();

            return Results.Json(new { ok = true, items });
        });

        adminApi.MapPost("/rollback", async (HttpContext ctx) =>
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            var rel = doc.RootElement.GetProperty("path").GetString() ?? "";
            var bak = doc.RootElement.GetProperty("backupFile").GetString() ?? "";

            var bakDir = SafeResolve(backupsRoot, BackupKey(rel));
            if (bakDir is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            var bakFull = Path.Combine(bakDir, bak);
            if (!File.Exists(bakFull)) return Results.NotFound(new { ok = false, error = "Backup not found." });

            var target = SafeResolve(dataRoot, rel);
            if (target is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            // Backup current before rollback
            if (File.Exists(target))
                CreateBackup(backupsRoot, rel, File.ReadAllBytes(target));

            File.WriteAllBytes(target, File.ReadAllBytes(bakFull));
            return Results.Json(new { ok = true });
        });

        // ===================== DB migrate =====================
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        // ===================== Debug endpoints =====================
        app.MapGet("/debug/static", () =>
        {
            var exists = Directory.Exists(dataRoot);
            var count = exists
                ? Directory.EnumerateFileSystemEntries(dataRoot, "*", SearchOption.AllDirectories).Count()
                : 0;

            return Results.Ok(new
            {
                dataRoot,
                seedRoot,
                seedExists = Directory.Exists(seedRoot),
                exists,
                count
            });
        });

        app.MapGet("/", context =>
        {
            context.Response.Redirect("/user.html");
            return Task.CompletedTask;
        });

        app.MapGet("/debug", (HttpContext ctx) =>
        {
            var user = ctx.User;
            var isAdmin =
                user?.IsInRole("admin") == true ||
                user?.Claims?.Any(c => (c.Type == "role" || c.Type.EndsWith("/role")) && c.Value == "admin") == true;

            if (!isAdmin) return Results.NotFound();
            return Results.Redirect("/debug.html");
        });

        app.MapGet("/livez", () => Results.Ok(new
        {
            ok = true,
            service = "Misfitz-Games",
            utc = DateTimeOffset.UtcNow
        }));

        app.MapGet("/debug/tuya", async (TuyaPlugService tuya) =>
        {
            await tuya.SetSwitchAsync(tuya.DeviceId1, false);
            return Results.Ok(new { ok = true, utc = DateTimeOffset.UtcNow });
        });

        app.MapGet("/debug/redis", (RedisMuxFactory factory) =>
        {
            var task = factory.Task;
            return Results.Ok(new
            {
                status = task.Status.ToString(),
                isCompleted = task.IsCompleted,
                isFaulted = task.IsFaulted,
                isCanceled = task.IsCanceled
            });
        });

        app.MapGet("/debug/redis/details", async (RedisMuxFactory factory) =>
        {
            var mux = await factory.GetAsync();
            return Results.Ok(new
            {
                isConnected = mux.IsConnected,
                endpoints = mux.GetEndPoints().Select(e => e.ToString()).ToArray()
            });
        });

        app.MapGet("/debug/whoami", (HttpContext ctx) => Results.Ok(new
        {
            isAuth = ctx.User?.Identity?.IsAuthenticated == true,
            claims = ctx.User?.Claims?.Select(c => new { c.Type, c.Value }).ToArray() ?? Array.Empty<object>()
        }));

        app.MapGet("/debug/db", async (AppDbContext db) =>
        {
            var canConnect = await db.Database.CanConnectAsync();
            return Results.Ok(new
            {
                ok = true,
                canConnect,
                provider = db.Database.ProviderName
            });
        });

        app.MapGet("/debug/env", (IConfiguration cfg) =>
        {
            var dbPath2 = cfg["DB_PATH"];
            return Results.Ok(new
            {
                ok = true,
                dbPath = dbPath2 ?? "(null)",
                dataDirExists = Directory.Exists("/data"),
                dataDirFiles = Directory.Exists("/data") ? Directory.GetFiles("/data") : [],
                cwd = Directory.GetCurrentDirectory()
            });
        });

        app.MapGet("/debug/dbpath", (IConfiguration cfg, IWebHostEnvironment env) =>
        {
            var dbPath3 =
                cfg["DB_PATH"]
                ?? (env.IsProduction() ? "/data/misfitz.db" : "Data/misfitz.db");

            return Results.Ok(new
            {
                env = env.EnvironmentName,
                dbPath = dbPath3,
                exists = File.Exists(dbPath3),
                dirExists = Directory.Exists(Path.GetDirectoryName(dbPath3)!),
                filesInDir = Directory.Exists(Path.GetDirectoryName(dbPath3)!)
                    ? Directory.GetFiles(Path.GetDirectoryName(dbPath3)!).Select(Path.GetFileName).ToArray()
                    : []
            });
        });

        app.MapGet("/debug/users", async (AppDbContext db) =>
        {
            var count = await db.Users.CountAsync();
            var last = await db.Users
                .OrderByDescending(u => u.Id)
                .Take(10)
                .Select(u => new { u.Id, u.Username, u.Role, u.CreatedUtc, u.LastLoginUtc })
                .ToListAsync();

            return Results.Ok(new { ok = true, count, last });
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.Run();
    }

    // ===================== Helpers =====================

    // Wildcard-aware bootstrap:
    // Seeds /data/site from Data/Site only if /data/site is empty OR missing any required patterns.
    private static void BootstrapSite(string seedRoot, string dataRoot, string[] requiredPatterns)
    {
        if (!Directory.Exists(seedRoot))
        {
            Console.WriteLine($"[site] Seed folder missing: {seedRoot}");
            return;
        }

        Directory.CreateDirectory(dataRoot);

        var hasAny = Directory.EnumerateFileSystemEntries(dataRoot).Any();

        bool PatternExists(string pattern) =>
            Directory.EnumerateFiles(dataRoot, pattern, SearchOption.AllDirectories).Any();

        var missingRequired = requiredPatterns.Any(p => !PatternExists(p));

        if (hasAny && !missingRequired)
        {
            Console.WriteLine("[site] /data/site already populated; skipping seed.");
            return;
        }

        Console.WriteLine($"[site] Seeding /data/site from: {seedRoot}");
        CopyDirectory(seedRoot, dataRoot, overwrite: true);
        Console.WriteLine("[site] Seed complete.");
    }

    private static void CopyDirectory(string sourceDir, string destDir, bool overwrite)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSub = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSub, overwrite);
        }
    }

    private static string? SafeResolve(string root, string relative)
    {
        relative = (relative ?? "").Replace('\\', '/').TrimStart('/');
        if (relative.Contains("..")) return null;

        var combined = Path.GetFullPath(Path.Combine(root, relative));
        var rootFull = Path.GetFullPath(root);

        if (!combined.StartsWith(rootFull, StringComparison.Ordinal)) return null;
        return combined;
    }

    private static object[] ListFiles(string root)
    {
        var list = new List<object>();
        foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
            var isDir = Directory.Exists(path);
            long size = 0;

            if (!isDir)
            {
                try { size = new FileInfo(path).Length; } catch { }
            }

            list.Add(new { path = rel, isDir, size });
        }

        // dirs first then files
        return [.. list
            .OrderByDescending(x => (bool)x.GetType().GetProperty("isDir")!.GetValue(x)!)
            .ThenBy(x => (string)x.GetType().GetProperty("path")!.GetValue(x)!)];
    }

    private static void CreateBackup(string backupsRoot, string relPath, byte[] bytes)
    {
        var dir = Path.Combine(backupsRoot, BackupKey(relPath));
        Directory.CreateDirectory(dir);

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        var name = $"{stamp}.bak";
        var full = Path.Combine(dir, name);

        File.WriteAllBytes(full, bytes);
    }

    private static string BackupKey(string relPath)
    {
        relPath = (relPath ?? "").Replace('\\', '/').TrimStart('/');
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(relPath));
        return Convert.ToHexString(hash);
    }

    private static string TryDecode(byte[] bytes)
    {
        try
        {
            var text = Encoding.UTF8.GetString(bytes);
            if (text.Any(ch => ch == '\0'))
                return $"/* Binary file (not editable here). Size: {bytes.Length} bytes */";
            return text;
        }
        catch
        {
            return $"/* Could not decode as UTF-8. Size: {bytes.Length} bytes */";
        }
    }

    private static string AdminEditorHtml() => """
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>Misfitz Web Editor</title>
  <style>
    body{font-family:system-ui,sans-serif;background:#0b0f14;color:#e6edf3;margin:0}
    .top{display:flex;gap:10px;align-items:center;justify-content:space-between;padding:12px 14px;border-bottom:1px solid #22304a;background:#0f172a;position:sticky;top:0}
    .btn{padding:8px 10px;border-radius:10px;border:1px solid #22304a;background:#111826;color:#e6edf3;cursor:pointer}
    .btn.primary{background:#2f81f7;border-color:#2f81f7;color:#fff}
    .wrap{display:grid;grid-template-columns:320px 1fr;min-height:calc(100vh - 52px)}
    .left{border-right:1px solid #22304a;padding:10px;overflow:auto}
    .right{padding:10px;display:flex;flex-direction:column;gap:10px}
    input,textarea,select{border-radius:10px;border:1px solid #22304a;background:#0b1220;color:#e6edf3;padding:8px}
    textarea{width:100%;height:52vh;font-family:ui-monospace,Consolas,monospace;font-size:13px;line-height:1.35}
    .file{padding:8px;border-radius:10px;border:1px solid #22304a;background:#111826;margin-bottom:8px;cursor:pointer}
    .file:hover{border-color:#2f81f7}
    .row{display:flex;gap:8px;align-items:center;flex-wrap:wrap}
    .muted{color:#9fb0c5;font-size:12px}
    iframe{width:100%;height:34vh;border:1px solid #22304a;border-radius:12px;background:#0b1220}
    .badge{padding:3px 8px;border-radius:999px;border:1px solid #22304a;background:#111826;font-size:12px}
    code{font-family:ui-monospace,Consolas,monospace}
  </style>
</head>
<body>
  <div class="top">
    <div class="row">
      <div style="font-weight:700">Misfitz Web Editor</div>
      <div class="badge" id="status">Loading…</div>
      <div class="muted">Edits save into <code>/data/site</code> (no rebuild)</div>
    </div>
    <a class="btn" href="/user.html">Account</a>
  </div>

  <div class="wrap">
    <div class="left">
      <div class="row" style="margin-bottom:10px">
        <input id="filter" placeholder="Filter files…" style="flex:1" />
        <button class="btn" id="btnRefresh">Refresh</button>
      </div>

      <div class="row" style="margin-bottom:10px">
        <input type="file" id="uploadFile" />
        <input id="uploadDir" placeholder="dir (optional)" style="width:140px" />
        <button class="btn" id="btnUpload">Upload</button>
      </div>

      <div id="files"></div>
    </div>

    <div class="right">
      <div class="row">
        <input id="path" placeholder="path…" style="flex:1" />
        <button class="btn primary" id="btnSave">Save</button>
        <button class="btn" id="btnDelete">Delete</button>
        <button class="btn" id="btnBackups">Backups</button>
      </div>

      <textarea id="content" placeholder="Select a file to edit…"></textarea>

      <div class="row">
        <button class="btn" id="btnPreview">Preview</button>
        <span class="muted">Preview works best for HTML pages. (If CSS/JS looks cached, hard refresh.)</span>
      </div>

      <iframe id="preview" title="preview"></iframe>

      <div id="backupPanel" class="muted"></div>
    </div>
  </div>

<script>
const el = (id) => document.getElementById(id);
let allFiles = [];

function setStatus(t){ el('status').textContent = t; }

async function api(url, opts){
  const r = await fetch(url, { credentials:'include', ...opts });

  // ✅ If not logged in as admin, bounce to normal login
  if (r.status === 401 || r.status === 403){
    location.href = '/user.html';
    return { ok:false, error:'not authorized' };
  }

  const text = await r.text();
  try { return JSON.parse(text); } catch { return { ok:false, error:text || ('HTTP '+r.status) }; }
}

function renderFiles(){
  const q = el('filter').value.trim().toLowerCase();
  const box = el('files');
  box.innerHTML = '';
  for(const f of allFiles){
    if(q && !f.path.toLowerCase().includes(q)) continue;
    const div = document.createElement('div');
    div.className = 'file';
    div.textContent = (f.isDir ? '📁 ' : '📄 ') + f.path;
    div.onclick = () => openFile(f.path, f.isDir);
    box.appendChild(div);
  }
}

async function loadList(){
  setStatus('Loading files…');
  const r = await api('/admin/api/list');
  if(!r.ok){ setStatus('Error'); alert(r.error || 'Failed'); return; }
  allFiles = r.files || [];
  renderFiles();
  setStatus('Ready');
}

async function openFile(path, isDir){
  el('backupPanel').textContent = '';
  if(isDir){ el('path').value = path + '/'; el('content').value=''; return; }

  setStatus('Reading…');
  const r = await api('/admin/api/read?path=' + encodeURIComponent(path));
  if(!r.ok){ setStatus('Error'); alert(r.error || 'Read failed'); return; }

  el('path').value = path;
  el('content').value = r.content ?? '';
  setStatus('Ready');
}

async function saveFile(){
  const path = el('path').value.trim();
  if(!path || path.endsWith('/')){ alert('Pick a file path'); return; }

  setStatus('Saving…');
  const r = await api('/admin/api/save', {
    method:'POST',
    headers:{'Content-Type':'application/json'},
    body: JSON.stringify({ path, content: el('content').value })
  });

  if(!r.ok){ setStatus('Error'); alert(r.error || 'Save failed'); return; }

  setStatus('Saved');
  await loadList();
}

async function deletePath(){
  const path = el('path').value.trim();
  if(!path){ alert('Enter a path'); return; }
  if(!confirm('Delete ' + path + '? A backup will be kept for files.')) return;

  setStatus('Deleting…');
  const r = await api('/admin/api/delete', {
    method:'POST',
    headers:{'Content-Type':'application/json'},
    body: JSON.stringify({ path })
  });

  if(!r.ok){ setStatus('Error'); alert(r.error || 'Delete failed'); return; }

  el('content').value = '';
  setStatus('Deleted');
  await loadList();
}

function preview(){
  const path = el('path').value.trim();
  if(!path || path.endsWith('/')){ alert('Pick an HTML file to preview'); return; }
  const bust = Date.now();

  const clean = path.replace(/^\/+/, '');
  el('preview').src = '/' + clean + '?v=' + bust;
}

async function showBackups(){
  const path = el('path').value.trim();
  if(!path || path.endsWith('/')){ alert('Pick a file'); return; }

  const r = await api('/admin/api/backups?path=' + encodeURIComponent(path));
  if(!r.ok){ alert(r.error || 'Failed'); return; }

  const items = r.items || [];
  if(items.length === 0){
    el('backupPanel').textContent = 'No backups yet for this file.';
    return;
  }

  const wrap = document.createElement('div');
  wrap.innerHTML = '<div style="margin-top:6px;font-weight:700">Backups</div>';

  for(const b of items){
    const row = document.createElement('div');
    row.style.marginTop = '6px';

    const btn = document.createElement('button');
    btn.className = 'btn';
    btn.textContent = 'Rollback to ' + b;
    btn.onclick = async () => {
      if(!confirm('Rollback ' + path + ' to ' + b + '?')) return;
      const rr = await api('/admin/api/rollback', {
        method:'POST',
        headers:{'Content-Type':'application/json'},
        body: JSON.stringify({ path, backupFile: b })
      });
      if(!rr.ok){ alert(rr.error || 'Rollback failed'); return; }
      await openFile(path, false);
      alert('Rolled back.');
    };

    row.appendChild(btn);
    wrap.appendChild(row);
  }

  el('backupPanel').innerHTML = '';
  el('backupPanel').appendChild(wrap);
}

async function upload(){
  const file = el('uploadFile').files[0];
  if(!file){ alert('Pick a file to upload'); return; }
  const dir = el('uploadDir').value.trim();

  const fd = new FormData();
  fd.append('file', file);
  fd.append('dir', dir);

  setStatus('Uploading…');
  const r = await fetch('/admin/api/upload', { method:'POST', credentials:'include', body: fd });

  if (r.status === 401 || r.status === 403){
    location.href = '/user.html';
    return;
  }

  const text = await r.text();
  let j; try{ j = JSON.parse(text);}catch{ j={ok:false,error:text}; }
  if(!j.ok){ setStatus('Error'); alert(j.error || 'Upload failed'); return; }

  setStatus('Uploaded');
  await loadList();
}

el('btnRefresh').onclick = (e)=>{ e.preventDefault(); loadList(); };
el('btnSave').onclick = (e)=>{ e.preventDefault(); saveFile(); };
el('btnDelete').onclick = (e)=>{ e.preventDefault(); deletePath(); };
el('btnPreview').onclick = (e)=>{ e.preventDefault(); preview(); };
el('btnBackups').onclick = (e)=>{ e.preventDefault(); showBackups(); };
el('btnUpload').onclick = (e)=>{ e.preventDefault(); upload(); };
el('filter').addEventListener('input', renderFiles);

loadList();
</script>
</body>
</html>
""";
}
