using Humanizer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Hubs;
using Misfitz_Games.Services;
using Misfitz_Games.Services.Games.Contexto;
using Misfitz_Games.Services.Games.Hangman;
using Misfitz_Games.Services.Infrastructure.Redis;
using Misfitz_Games.Services.Room;
using Misfitz_Games.Services.Tuya;
using System.Security.Claims;

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


        // CORS: if your frontend is served from the SAME origin as the API,
        // you do NOT need AllowCredentials+CORS at all. But leaving this is fine.
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
                 .AllowCredentials()
                 );
        });

        // --- EF Core (SQLite) for user accounts ---
        // Use Render disk path if you have one (recommended):
        // set env var DB_PATH=/data/misfitz.db
        var dbPath =
            builder.Configuration["DB_PATH"]
            ?? (builder.Environment.IsProduction() ? "/data/misfitz.db" : "Data/misfitz.db");
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dbDir))
            Directory.CreateDirectory(dbDir);

        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}")
        );

        // --- Auth: Cookie auth (recommended for your HTML + JS pages) ---
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "misfitz_auth";
                o.Cookie.HttpOnly = true;
                o.SlidingExpiration = true;

                // Render is HTTPS in production, force secure cookies
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;

                // If everything is same-site (same domain), Lax is perfect.
                // If you split frontend and API across domains, change this to None and ensure HTTPS.
                o.Cookie.SameSite = SameSiteMode.Lax;

                // Optional redirects (nice for normal browser navigation)
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

        // Redis factory (lazy, async)
        builder.Services.AddSingleton<RedisMuxFactory>();

        // App services
        builder.Services.AddSingleton<IRoomStateStore, RedisRoomStateStore>();
        builder.Services.AddSingleton<ContextoEngine>();
        builder.Services.AddSingleton<RoomBroadcastService>();
        builder.Services.AddSingleton<RoomGameBroadcaster>();
        builder.Services.AddSingleton<ContextoWordProvider>();
        builder.Services.AddSingleton<WordVectorStore>();
        builder.Services.AddSingleton<ContextoRankIndexStore>();

        // Effects / hardware services
        builder.Services.AddHttpClient<TuyaPlugService>();
        builder.Services.AddScoped<EffectsService>();
        builder.Services.AddScoped<EffectsEngine>();
        builder.Services.AddDataProtection();
        builder.Services.AddScoped<TuyaOAuthService>();

        //TikFinity / Spotify / Streamer
        builder.Services.AddSingleton<WebhookIngestService>();

        // Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        // Suppress EF Core SQL command logs
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        // Optional: quiet general EF noise too
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

        var app = builder.Build();

        var adminToken = builder.Configuration["ADMIN_TOKEN"] ?? "";
        var dataRoot = "/data/site";
        var backupsRoot = "/data/backups";

        // ------------- First-run bootstrap: copy wwwroot -> /data/site (only if empty) -------------
        Directory.CreateDirectory(dataRoot);
        Directory.CreateDirectory(backupsRoot);

        var seed = Path.Combine(app.Environment.ContentRootPath, "Data", "Site");
        if (Directory.Exists(seed) && !Directory.EnumerateFileSystemEntries(dataRoot).Any())
        {
            CopyDirectory(seed, dataRoot);
        }

        app.UseRouting();

        app.UseCors("default");

        app.UseAuthentication();
        app.UseAuthorization();

        // ------------- Serve editable site from /data/site -------------
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(dataRoot),
            RequestPath = "",
            OnPrepareResponse = ctx =>
            {
                // Helpful during frequent edits. You can relax later.
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                ctx.Context.Response.Headers.Pragma = "no-cache";
                ctx.Context.Response.Headers.Expires = "0";
            }
        });

        // If you also use app.UseStaticFiles() for wwwroot elsewhere, keep it AFTER the /data one,
        // so /data overrides by route priority. (Or remove it if you only want /data.)


        app.MapControllers();
        app.MapHub<RoomHub>("/hubs/room");

        // ------------- Admin auth helpers -------------
        bool IsAuthed(HttpContext ctx)
        {
            if (string.IsNullOrWhiteSpace(adminToken)) return false;

            // Header auth (useful for curl/tools)
            if (ctx.Request.Headers.TryGetValue("X-Admin-Token", out var hv) &&
                hv.ToString() == adminToken)
                return true;

            // Cookie auth (browser)
            if (ctx.Request.Cookies.TryGetValue("mg_admin", out var cv) &&
                cv == adminToken)
                return true;

            return false;
        }

        IResult? RequireAuth(HttpContext ctx)
        {
            if (IsAuthed(ctx)) return null;

            // Browser: show login page for /admin
            if (ctx.Request.Path.StartsWithSegments("/admin") && ctx.Request.Method == "GET")
                return Results.Content(AdminLoginHtml(), "text/html; charset=utf-8");

            return Results.Unauthorized();
        }

        // ------------- Admin pages -------------
        app.MapGet("/admin", (HttpContext ctx) =>
        {
            var deny = RequireAuth(ctx);
            if (deny is not null) return deny;

            return Results.Content(AdminEditorHtml(), "text/html; charset=utf-8");
        });

        app.MapPost("/admin/login", async (HttpContext ctx) =>
        {
            // Accept token from form or JSON
            string token = "";

            if (ctx.Request.HasFormContentType)
            {
                var form = await ctx.Request.ReadFormAsync();
                token = form["token"].ToString();
            }
            else
            {
                try
                {
                    using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
                    if (doc.RootElement.TryGetProperty("token", out var t))
                        token = t.GetString() ?? "";
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(adminToken) && token == adminToken)
            {
                ctx.Response.Cookies.Append("mg_admin", adminToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/"
                });
                return Results.Redirect("/admin");
            }

            return Results.Content(AdminLoginHtml("Invalid token."), "text/html; charset=utf-8");
        });

        app.MapPost("/admin/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete("mg_admin", new CookieOptions { Path = "/" });
            return Results.Redirect("/admin");
        });

        // ------------- Admin API (file operations) -------------
        app.MapGet("/admin/api/list", (HttpContext ctx) =>
        {
            var deny = RequireAuth(ctx);
            if (deny is not null) return deny;

            var files = ListFiles(dataRoot);
            return Results.Json(new { ok = true, root = dataRoot, files });
        });

        app.MapGet("/admin/api/read", (HttpContext ctx, string path) =>
        {
            var deny = RequireAuth(ctx);
            if (deny is not null) return deny;

            var full = SafeResolve(dataRoot, path);
            if (full is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });
            if (!System.IO.File.Exists(full)) return Results.NotFound(new { ok = false, error = "Not found." });

            var bytes = System.IO.File.ReadAllBytes(full);
            var text = TryDecode(bytes);

            return Results.Json(new { ok = true, path, content = text });
        });

        app.MapPost("/admin/api/save", async (HttpContext ctx) =>
        {
            var deny = RequireAuth(ctx);
            if (deny is not null) return deny;

            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            var rel = doc.RootElement.GetProperty("path").GetString() ?? "";
            var content = doc.RootElement.GetProperty("content").GetString() ?? "";

            var full = SafeResolve(dataRoot, rel);
            if (full is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);

            // Backup existing before overwrite
            if (System.IO.File.Exists(full))
                CreateBackup(backupsRoot, rel, System.IO.File.ReadAllBytes(full));

            System.IO.File.WriteAllText(full, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return Results.Json(new { ok = true, path = rel });
        });

        app.MapPost("/admin/api/upload", async (HttpContext ctx) =>
        {
            var deny = RequireAuth(ctx);
            if (deny is not null) return deny;

            if (!ctx.Request.HasFormContentType) return Results.BadRequest(new { ok = false, error = "Expected multipart/form-data" });

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
            if (System.IO.File.Exists(full))
            {
                var existing = System.IO.File.ReadAllBytes(full);
                CreateBackup(backupsRoot, targetRel, existing);
            }

            using var fs = System.IO.File.Create(full);
            await file.CopyToAsync(fs);

            return Results.Json(new { ok = true, path = targetRel });
        });

        app.MapPost("/admin/api/delete", async (HttpContext ctx) =>
        {
            var deny = RequireAuth(ctx);
            if (deny is not null) return deny;

            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            var rel = doc.RootElement.GetProperty("path").GetString() ?? "";

            var full = SafeResolve(dataRoot, rel);
            if (full is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            if (System.IO.File.Exists(full))
            {
                CreateBackup(backupsRoot, rel, System.IO.File.ReadAllBytes(full));
                System.IO.File.Delete(full);
                return Results.Json(new { ok = true });
            }

            if (Directory.Exists(full))
            {
                Directory.Delete(full, recursive: true);
                return Results.Json(new { ok = true });
            }

            return Results.NotFound(new { ok = false, error = "Not found." });
        });

        app.MapGet("/admin/api/backups", (HttpContext ctx, string path) =>
        {
            var deny = RequireAuth(ctx);
            if (deny is not null) return deny;

            var safe = SafeResolve(backupsRoot, BackupKey(path));
            if (safe is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            if (!Directory.Exists(safe)) return Results.Json(new { ok = true, items = Array.Empty<string>() });

            var items = Directory.EnumerateFiles(safe, "*.bak")
                .Select(Path.GetFileName)
                .OrderByDescending(x => x)
                .ToArray();

            return Results.Json(new { ok = true, items });
        });

        app.MapPost("/admin/api/rollback", async (HttpContext ctx) =>
        {
            var deny = RequireAuth(ctx);
            if (deny is not null) return deny;

            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            var rel = doc.RootElement.GetProperty("path").GetString() ?? "";
            var bak = doc.RootElement.GetProperty("backupFile").GetString() ?? "";

            var bakDir = SafeResolve(backupsRoot, BackupKey(rel));
            if (bakDir is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            var bakFull = Path.Combine(bakDir, bak);
            if (!System.IO.File.Exists(bakFull)) return Results.NotFound(new { ok = false, error = "Backup not found." });

            var target = SafeResolve(dataRoot, rel);
            if (target is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            // Backup current before rollback
            if (System.IO.File.Exists(target))
                CreateBackup(backupsRoot, rel, System.IO.File.ReadAllBytes(target));

            System.IO.File.WriteAllBytes(target, System.IO.File.ReadAllBytes(bakFull));
            return Results.Json(new { ok = true });
        });

        // ------------- Your existing endpoints/controllers/etc go here -------------

        // Create/migrate DB on startup (simple and effective)
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        app.MapGet("/debug/static", () =>
        {
            var dataRoot = "/data/site";
            var exists = Directory.Exists(dataRoot);
            var count = exists ? Directory.EnumerateFileSystemEntries(dataRoot, "*", SearchOption.AllDirectories).Count() : 0;
            return Results.Ok(new { dataRoot, exists, count });
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
            await tuya.SetSwitchAsync(tuya.DeviceId1, false); // or create a GetTokenAsync method
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
            var dbPath = cfg["DB_PATH"];
            return Results.Ok(new
            {
                ok = true,
                dbPath = dbPath ?? "(null)",
                dataDirExists = Directory.Exists("/data"),
                dataDirFiles = Directory.Exists("/data") ? Directory.GetFiles("/data") : [],
                cwd = Directory.GetCurrentDirectory()
            });
        });

        app.MapGet("/debug/dbpath", (IConfiguration cfg, IWebHostEnvironment env) =>
        {
            var dbPath =
                cfg["DB_PATH"]
                ?? (env.IsProduction() ? "/data/misfitz.db" : "Data/misfitz.db");

            return Results.Ok(new
            {
                env = env.EnvironmentName,
                dbPath,
                exists = System.IO.File.Exists(dbPath),
                dirExists = System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(dbPath)!),
                filesInDir = System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(dbPath)!)
                    ? System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(dbPath)!).Select(Path.GetFileName).ToArray()
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


        // ===================== Helpers =====================

        static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var dest = Path.Combine(destinationDir, Path.GetFileName(file));
                System.IO.File.Copy(file, dest, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dest = Path.Combine(destinationDir, Path.GetFileName(dir));
                CopyDirectory(dir, dest);
            }
        }

        static string? SafeResolve(string root, string relative)
        {
            relative = (relative ?? "").Replace('\\', '/').TrimStart('/');
            if (relative.Contains("..")) return null;

            var combined = Path.GetFullPath(Path.Combine(root, relative));
            var rootFull = Path.GetFullPath(root);

            if (!combined.StartsWith(rootFull, StringComparison.Ordinal)) return null;
            return combined;
        }

        static object[] ListFiles(string root)
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

                list.Add(new
                {
                    path = rel,
                    isDir,
                    size
                });
            }

            // Sort: dirs first then files
            return [..list
        .OrderByDescending(x => (bool)x.GetType().GetProperty("isDir")!.GetValue(x)!)
        .ThenBy(x => (string)x.GetType().GetProperty("path")!.GetValue(x)!)];
        }

        static void CreateBackup(string backupsRoot, string relPath, byte[] bytes)
        {
            var dir = Path.Combine(backupsRoot, BackupKey(relPath));
            Directory.CreateDirectory(dir);

            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            var name = $"{stamp}.bak";
            var full = Path.Combine(dir, name);

            System.IO.File.WriteAllBytes(full, bytes);
        }

        static string BackupKey(string relPath)
        {
            // Turn "pages/overlay.html" into a safe folder key
            relPath = (relPath ?? "").Replace('\\', '/').TrimStart('/');
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(relPath));
            return Convert.ToHexString(hash);
        }

        static string TryDecode(byte[] bytes)
        {
            // assume UTF-8 if possible, else fall back to base64 notice
            try
            {
                var text = Encoding.UTF8.GetString(bytes);
                // Rough “binary?” heuristic
                if (text.Any(ch => ch == '\0'))
                    return $"/* Binary file (not editable here). Size: {bytes.Length} bytes */";
                return text;
            }
            catch
            {
                return $"/* Could not decode as UTF-8. Size: {bytes.Length} bytes */";
            }
        }

        static string AdminLoginHtml(string? error = null) => $@"<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
  <title>Misfitz Admin Login</title>
  <style>
    body {{ font-family: system-ui, sans-serif; background:#0b0f14; color:#e6edf3; display:flex; min-height:100vh; align-items:center; justify-content:center; }}
    .card {{ width:min(420px, 92vw); background:#111826; border:1px solid #22304a; border-radius:16px; padding:18px; box-shadow: 0 10px 30px rgba(0,0,0,.35); }}
    h1 {{ margin:0 0 10px; font-size:18px; }}
    .muted {{ color:#9fb0c5; font-size:13px; margin-bottom:12px; }}
    input {{ width:100%; padding:10px 12px; border-radius:12px; border:1px solid #22304a; background:#0b1220; color:#e6edf3; }}
    button {{ width:100%; margin-top:10px; padding:10px 12px; border-radius:12px; border:0; background:#2f81f7; color:white; font-weight:600; cursor:pointer; }}
    .err {{ margin-top:10px; color:#ff7b72; font-size:13px; }}
  </style>
</head>
<body>
  <form class=""card"" method=""post"" action=""/admin/login"">
    <h1>Misfitz Games — Admin</h1>
    <div class=""muted"">Enter your admin token to edit live pages served from <code>/data/site</code>.</div>
    <input name=""token"" type=""password"" placeholder=""ADMIN_TOKEN"" autocomplete=""current-password"" />
    <button type=""submit"">Login</button>
    {(string.IsNullOrWhiteSpace(error) ? "" : $@"<div class=""err"">{System.Net.WebUtility.HtmlEncode(error)}</div>")}
  </form>
</body>
</html>";

        static string AdminEditorHtml() => """
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
    <form method="post" action="/admin/logout">
      <button class="btn" type="submit">Logout</button>
    </form>
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
  const r = await fetch(url, opts);
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

  // FIXED: strip leading slashes correctly
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
  const r = await fetch('/admin/api/upload', { method:'POST', body: fd });
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
}