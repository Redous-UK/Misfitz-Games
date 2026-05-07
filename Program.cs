using Humanizer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Misfitz_Games.Data;
using Misfitz_Games.Hubs;
using Misfitz_Games.Models.Battles;
using Misfitz_Games.Models.Battles.Requests;
using Misfitz_Games.Services;
using Misfitz_Games.Services.Games.Contexto;
using Misfitz_Games.Services.Games.Hangman;
using Misfitz_Games.Services.Games.HigherLower;
using Misfitz_Games.Services.Games.RiddleMeThis;
using Misfitz_Games.Services.Games.Trivia;
using Misfitz_Games.Services.Infrastructure.Redis;
using Misfitz_Games.Services.Room;
using Misfitz_Games.Services.Effects;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Misfitz_Games;

public static class Program
{
    private static readonly string[] handler = ["admin", "member", "guest"];
    private static readonly string[] handlerArray = ["pending", "approved", "declined", "completed"];

    public sealed record UpdateUserRoleRequest(string Role);

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

                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Lax;

                o.LoginPath = "/login.html";
                o.AccessDeniedPath = "/login.html";

                o.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = ctx =>
                    {
                        if (ctx.Request.Path.StartsWithSegments("/api") ||
                        ctx.Request.Path.StartsWithSegments("/admin/api") ||
                        ctx.Request.Path.StartsWithSegments("/member") ||
                        ctx.Request.Path.StartsWithSegments("/admin/site"))
                        {
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }
                        ctx.Response.Redirect(ctx.RedirectUri);
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = ctx =>
                    {
                        if (ctx.Request.Path.StartsWithSegments("/api") ||
                        ctx.Request.Path.StartsWithSegments("/admin/api") ||
                        ctx.Request.Path.StartsWithSegments("/member") ||
                        ctx.Request.Path.StartsWithSegments("/admin/site"))
                        {
                            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }
                        ctx.Response.Redirect(ctx.RedirectUri);
                        return Task.CompletedTask;
                    }
                };
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
        builder.Services.AddSingleton<ContextoWordProvider>();
        builder.Services.AddSingleton<WordVectorStore>();
        builder.Services.AddSingleton<ContextoRankIndexStore>();
        builder.Services.AddSingleton<HangmanService>();
        builder.Services.AddHttpClient<TriviaService>();
        builder.Services.AddSingleton<HigherLowerService>();
        builder.Services.AddScoped<RiddleRepository>();
        builder.Services.AddScoped<RiddleImportService>();
        builder.Services.AddScoped<LeaderboardService>();
        builder.Services.AddHttpClient<RiddleImportService>();

        // Effects / hardware services
        builder.Services.AddHttpClient<TuyaPlugService>();
        builder.Services.AddScoped<EffectsService>();
        builder.Services.AddScoped<EffectsEngine>();
        builder.Services.AddScoped<HueProvider>();
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

        app.Use(async (ctx, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                // This WILL show in Render logs
                app.Logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

                // If headers already sent, rethrow
                if (ctx.Response.HasStarted) throw;

                ctx.Response.Clear();
                ctx.Response.StatusCode = 500;

                // Return JSON (so your browser Network tab finally shows the reason)
                ctx.Response.ContentType = "application/json; charset=utf-8";
                var payload = new
                {
                    ok = false,
                    error = "Server error",
                    detail = ex.Message,               // keep it simple for prod
                    type = ex.GetType().Name,
                    path = ctx.Request.Path.ToString()
                };

                await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(payload));
            }
        });

        // ===================== Site roots =====================
        var isProduction = app.Environment.IsProduction();

        var writableRoot = isProduction
            ? "/data"
            : Path.Combine(app.Environment.ContentRootPath, "Data");

        var dataRoot = Path.Combine(writableRoot, "site");
        var backupsRoot = Path.Combine(writableRoot, "backups");
        var seedRoot = Path.Combine(app.Environment.ContentRootPath, "Data", "Site");

        Console.WriteLine($"[SITE] Environment: {app.Environment.EnvironmentName}");
        Console.WriteLine($"[SITE] isProduction: {isProduction}");
        Console.WriteLine($"[SITE] writableRoot: {writableRoot}");
        Console.WriteLine($"[SITE] dataRoot: {dataRoot}");
        Console.WriteLine($"[SITE] backupsRoot: {backupsRoot}");
        Console.WriteLine($"[SITE] seedRoot: {seedRoot}");

        Directory.CreateDirectory(writableRoot);
        Directory.CreateDirectory(dataRoot);
        Directory.CreateDirectory(backupsRoot);

        // ===================== Data/Site -> storage sync (SAFE) =====================
        var pushAll = (Environment.GetEnvironmentVariable("SITE_PUSH_ALL") ?? "off").Trim().ToLowerInvariant();
        var clean = (Environment.GetEnvironmentVariable("SITE_PUSH_CLEAN") ?? "off").Trim().ToLowerInvariant();

        var overwrite = pushAll is "on" or "true" or "1" or "yes";
        var doClean = clean is "on" or "true" or "1" or "yes";

        Console.WriteLine($"[SITE] Overwrite: {overwrite}");
        Console.WriteLine($"[SITE] Clean: {doClean}");

        // ✅ ONLY run sync in persistent environments (Render)
        if (isProduction && Directory.Exists(seedRoot))
        {
            try
            {
                SyncDirectory(seedRoot, dataRoot, overwrite, doClean);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SITE] Sync failed: {ex}");
            }
        }
        else
        {
            Console.WriteLine("[SITE] Sync skipped (non-persistent environment).");
        }

        // ===================== Bootstrap =====================

        // moved to own controller. See Controllers/Admin/BootstrapAdminController.cs

        // ===================== Pipeline =====================
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

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

        // ===================== Admin editor page (route) =====================
        app.MapGet("/admin", () =>
            Results.Content(AdminEditorHtml(), "text/html; charset=utf-8")
        ).RequireAuthorization("AdminOnly");

        // ===================== Admin APIs (existing) =====================
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
            var relDir = form["dir"].ToString();
            var file = form.Files.GetFile("file");
            if (file is null) return Results.BadRequest(new { ok = false, error = "Missing file." });

            var safeDir = string.IsNullOrWhiteSpace(relDir) ? "" : relDir.Trim();
            var targetRel = Path.Combine(safeDir, file.FileName).Replace('\\', '/');

            var full = SafeResolve(dataRoot, targetRel);
            if (full is null) return Results.BadRequest(new { ok = false, error = "Invalid path." });

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);

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

            if (File.Exists(target))
                CreateBackup(backupsRoot, rel, File.ReadAllBytes(target));

            File.WriteAllBytes(target, File.ReadAllBytes(bakFull));
            return Results.Json(new { ok = true });
        });

        app.MapGet("/admin/sql", async context =>
        {
            context.Response.Redirect("/admin/admin-dashboard.html");
        })
.RequireAuthorization("AdminOnly");

        app.MapGet("/admin/users", async (AppDbContext db) =>
        {
            var users = await db.Users
                .AsNoTracking()
                .OrderBy(u => u.Username)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.DisplayName,
                    u.Role,
                    u.CreatedUtc
                })
                .ToListAsync();

            return Results.Ok(new { users });
        })
.RequireAuthorization("AdminOnly");


        app.MapPost("/admin/users/{userId}/role", async (
    Guid userId,
    UpdateUserRoleRequest request,
    AppDbContext db) =>
        {
            var allowed = handler;
            var role = (request.Role ?? "").Trim().ToLowerInvariant();

            if (!allowed.Contains(role))
                return Results.BadRequest(new { ok = false, error = "Invalid role." });

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                return Results.NotFound(new { ok = false, error = "User not found." });

            user.Role = role;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                ok = true,
                userId = user.Id,
                role = user.Role
            });
        })
.RequireAuthorization("AdminOnly");

        // ===================== NEW: /admin/site endpoints (used by your new folder-only explorer) =====================
        // Your admin HTML is calling:
        //   GET /admin/site/list?path=
        //   GET /admin/site/read?path=
        // These must exist, otherwise the left panel will show nothing.
        var adminSite = app.MapGroup("/admin/site")
    .RequireAuthorization("AdminOnly");

        adminSite.MapGet("/list", (string? path) =>
        {
            try
            {
                var rel = NormalizeRelPath(path);              // never throws now (see below)
                var abs = SafeResolve(dataRoot, rel);
                if (abs is null)
                    return Results.BadRequest(new { ok = false, error = "Invalid path.", path = rel });

                if (!Directory.Exists(abs))
                    return Results.NotFound(new { ok = false, error = "Folder not found.", path = rel });

                var dirs = Directory.EnumerateDirectories(abs)
                    .Select(d => new DirectoryInfo(d))
                    .Select(di => new
                    {
                        name = di.Name,
                        type = "dir",
                        size = (long?)null,
                        updatedUtc = di.LastWriteTimeUtc
                    });

                var files = Directory.EnumerateFiles(abs)
                    .Select(f => new FileInfo(f))
                    .Select(fi => new
                    {
                        name = fi.Name,
                        type = "file",
                        size = (long?)fi.Length,
                        updatedUtc = fi.LastWriteTimeUtc
                    });

                var entries = dirs.Concat(files)
                    .OrderBy(e => e.type == "dir" ? 0 : 1)
                    .ThenBy(e => e.name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return Results.Json(new { ok = true, path = rel, entries });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[admin/site/list] ERROR path='{path}': {ex}");
                return Results.Problem("admin/site/list failed");
            }
        });

        adminSite.MapGet("/read", (string path) =>
        {
            try
            {
                var rel = NormalizeRelPath(path);
                var full = SafeResolve(dataRoot, rel);
                if (full is null)
                    return Results.BadRequest(new { ok = false, error = "Invalid path.", path = rel });

                if (!File.Exists(full))
                    return Results.NotFound(new { ok = false, error = "Not found.", path = rel });

                var bytes = File.ReadAllBytes(full);
                var content = TryDecode(bytes);
                return Results.Json(new { ok = true, path = rel, content });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[admin/site/read] ERROR path='{path}': {ex}");
                return Results.Problem("admin/site/read failed");
            }
        });

        // ===================== DB migrate =====================
        var skipMigrate = builder.Configuration["SKIP_MIGRATE"] == "1";
        var fixMigrationId = builder.Configuration["DBFIX_MARK_MIGRATION"];

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            if (!skipMigrate)
            {

                if (!string.IsNullOrWhiteSpace(fixMigrationId))
                {
                    try
                    {
                        MarkMigrationApplied(dbPath, fixMigrationId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[DBFIX] failed: " + ex);
                        // decide: either continue or crash — I'd continue
                    }
                }


                db.Database.Migrate();
                Console.WriteLine("[EF] Migrate complete");
            }
            else
            {
                Console.WriteLine("[EF] SKIP_MIGRATE=1 (skipping db.Database.Migrate)");
            }
        }

        static void MarkMigrationApplied(string dbPath, string migrationId)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            // Get a ProductVersion from an existing row (fallback if empty)
            string productVersion = "8.0.11";
            using (var vcmd = conn.CreateCommand())
            {
                vcmd.CommandText = "SELECT ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;";
                var v = vcmd.ExecuteScalar();
                if (v is string s && !string.IsNullOrWhiteSpace(s))
                    productVersion = s;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
                SELECT $id, $ver
                WHERE NOT EXISTS (
                  SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = $id
                );";
            cmd.Parameters.AddWithValue("$id", migrationId);
            cmd.Parameters.AddWithValue("$ver", productVersion);

            var rows = cmd.ExecuteNonQuery();
            Console.WriteLine($"[DBFIX] MarkMigrationApplied: {migrationId} inserted={rows} productVersion={productVersion}");
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

        app.MapGet("/debug/whoami", (HttpContext ctx) => Results.Ok(new
        {
            isAuth = ctx.User?.Identity?.IsAuthenticated == true,
            claims = ctx.User?.Claims?.Select(c => new { c.Type, c.Value }).ToArray() ?? Array.Empty<object>()
        }));

        app.MapGet("/", context =>
        {
            context.Response.Redirect("/login.html");
            return Task.CompletedTask;
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.Run();
    }

    // ===================== Models =====================
    private sealed class SiteEntry
    {
        public required string Name { get; init; }
        public required string Type { get; init; } // "dir" | "file"
        public long? Size { get; init; }
        public DateTime UpdatedUtc { get; init; }
    }

    // ===================== Helpers =====================

    static void SyncDirectory(string sourceRoot, string targetRoot, bool overwrite, bool clean)
    {
        Directory.CreateDirectory(targetRoot);

        // Copy source -> target
        foreach (var srcFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceRoot, srcFile);
            var dstFile = Path.Combine(targetRoot, rel);

            Directory.CreateDirectory(Path.GetDirectoryName(dstFile)!);

            if (!overwrite && File.Exists(dstFile))
                continue;

            File.Copy(srcFile, dstFile, overwrite: true);
        }

        if (clean)
        {
            foreach (var dstFile in Directory.EnumerateFiles(targetRoot, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(targetRoot, dstFile);
                var srcFile = Path.Combine(sourceRoot, rel);

                if (!File.Exists(srcFile))
                    File.Delete(dstFile);
            }

            foreach (var dstDir in Directory.EnumerateDirectories(targetRoot, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(dstDir).Any())
                    Directory.Delete(dstDir);
            }
        }

        Console.WriteLine($"[SITE] Sync complete. Overwrite={overwrite}, Clean={clean}");
    }

    private static string NormalizeRelPath(string? path)
    {
        var p = (path ?? "").Trim();
        p = p.Replace('\\', '/').TrimStart('/');
        while (p.Contains("//", StringComparison.Ordinal))
            p = p.Replace("//", "/", StringComparison.Ordinal);
        // Do NOT throw — just treat as invalid later
        if (p.Contains("..", StringComparison.Ordinal))
            return "__INVALID__";

        return p;
    }

    private static string? SafeResolve(string root, string relative)
    {
        relative = (relative ?? "").Replace('\\', '/').TrimStart('/');
        if (relative.Contains("..") || relative == "__INVALID__") return null;

        var combined = Path.GetFullPath(Path.Combine(root, relative));
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                      + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(combined.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                           root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                           StringComparison.OrdinalIgnoreCase))
            return null;

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
    .row{display:flex;gap:8px;align-items:center;flex-wrap:wrap}
    .muted{color:#9fb0c5;font-size:12px}
    iframe{width:100%;height:34vh;border:1px solid #22304a;border-radius:12px;background:#0b1220}
    .badge{padding:3px 8px;border-radius:999px;border:1px solid #22304a;background:#111826;font-size:12px}
    code{font-family:ui-monospace,Consolas,monospace}
    #files{display:flex;flex-direction:column;gap:6px}
  </style>
</head>
<body>
  <div class="top">
    <div class="row">
      <div style="font-weight:700">Misfitz Web Editor</div>
      <div class="badge" id="status">Loading…</div>
      <div class="muted">Edits save into <code>/data/site</code> (no rebuild)</div>
    </div>
    <button id="btnMe" class="btn">Check login</button>
  </div>

  <div class="wrap">
    <div class="left">
      <div class="row" style="justify-content:space-between; gap:8px; margin-bottom:8px;">
        <div class="muted" id="pathLabel">/</div>
        <button id="btnUp" class="btn" disabled>Up</button>
      </div>
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
        <span class="muted">Preview works best for HTML pages.</span>
      </div>

      <iframe id="preview" title="preview"></iframe>
      <div id="backupPanel" class="muted"></div>
    </div>
  </div>

<script>
const el = (id) => document.getElementById(id);

let currentPath = "";         // "" = root
let lastEntries = [];
let allPathsCache = null;     // cache for /admin/api/list
let currentFilePath = null;   // currently opened file (relative)

function setStatus(text) {
  const s = el("status");
  if (s) s.textContent = text;
}

window.addEventListener("error", (e) => console.error("[error]", e.message, e.filename, e.lineno, e.error));
window.addEventListener("unhandledrejection", (e) => console.error("[unhandled]", e.reason));

async function api(url, opts) {
  const r = await fetch(url, opts);
  const txt = await r.text();
  let json = null;
  try { json = txt ? JSON.parse(txt) : null; } catch { /* non-json */ }
  if (!r.ok) throw new Error(`HTTP ${r.status}${json ? ": " + JSON.stringify(json) : ""}`);
  return json ?? {};
}

function joinPath(base, child) {
  const b = (base || "").replace(/\/+$/, "");
  const c = (child || "").replace(/^\/+/, "");
  return b ? `${b}/${c}` : c;
}

function parentPath(path) {
  if (!path) return "";
  const p = path.replace(/\/+$/, "");
  const idx = p.lastIndexOf("/");
  return idx === -1 ? "" : p.slice(0, idx);
}

async function listAll() {
  return await api("/admin/api/list");
}

async function listFolder(path) {
  if (!allPathsCache) allPathsCache = await listAll();

  const prefix = (path || "").replace(/^\/+|\/+$/g, "");
  const wantPrefix = prefix ? (prefix + "/") : "";

  const entriesMap = new Map();

  for (const item of (allPathsCache.files || [])) {
    const p = (item.path || "").replace(/^\/+/, "");
    if (wantPrefix && !p.startsWith(wantPrefix)) continue;

    const rest = wantPrefix ? p.slice(wantPrefix.length) : p;
    if (!rest) continue;

    const parts = rest.split("/");
    const name = parts[0];

    if (parts.length === 1) {
      if (!entriesMap.has(name)) {
        entriesMap.set(name, {
          name,
          type: item.isDir ? "dir" : "file",
          size: item.size ?? null
        });
      }
    } else {
      if (!entriesMap.has(name)) {
        entriesMap.set(name, { name, type: "dir", size: null });
      }
    }
  }

  return { ok: true, path: prefix, entries: [...entriesMap.values()] };
}

function syncUploadDir() {
  const up = el("uploadDir");
  if (up) up.value = currentPath || "";
}

function normalizeEntries(out) {
  return Array.isArray(out.entries) ? out.entries : [];
}

async function openFile(path) {
  currentFilePath = path;

  const pathBox = el("path");
  if (pathBox) pathBox.value = path;

  // IMPORTANT: use the working read endpoint
  const r = await api(`/admin/api/read?path=${encodeURIComponent(path)}`);

  const ta = el("content");
  ta.value = r.content ?? "";
  ta.focus();

  setStatus("Loaded");
}

function renderEntries(entries) {
  const list = el("files");
  list.innerHTML = "";

  for (const e of entries) {
    const row = document.createElement("div");
    row.className = "fileRow";
    row.style.display = "flex";
    row.style.justifyContent = "space-between";
    row.style.alignItems = "center";
    row.style.gap = "10px";
    row.style.padding = "8px 10px";
    row.style.cursor = "pointer";
    row.style.borderRadius = "12px";
    row.style.userSelect = "none";

    row.onmouseenter = () => row.style.background = "rgba(255,255,255,.06)";
    row.onmouseleave = () => row.style.background = "transparent";

    const left = document.createElement("div");
    left.style.display = "flex";
    left.style.gap = "10px";
    left.style.alignItems = "center";
    left.style.minWidth = "0";

    const icon = document.createElement("span");
    icon.textContent = e.type === "dir" ? "📁" : "📄";

    const name = document.createElement("span");
    name.textContent = e.name;
    name.style.whiteSpace = "nowrap";
    name.style.overflow = "hidden";
    name.style.textOverflow = "ellipsis";

    left.appendChild(icon);
    left.appendChild(name);

    const right = document.createElement("span");
    right.className = "muted";
    right.style.whiteSpace = "nowrap";
    right.textContent = e.type === "dir" ? "" : (e.size != null ? `${e.size}b` : "");

    row.appendChild(left);
    row.appendChild(right);

    row.onclick = async () => {
      if (e.type === "dir") {
        currentPath = joinPath(currentPath, e.name);
        syncUploadDir();
        const f = el("filter"); if (f) f.value = "";
        await refreshLeftPanel();
      } else {
        const full = joinPath(currentPath, e.name);
        syncUploadDir();
        await openFile(full);
      }
    };

    list.appendChild(row);
  }
}

function applyFilter() {
  const q = (el("filter")?.value || "").trim().toLowerCase();
  if (!q) return renderEntries(lastEntries);
  renderEntries(lastEntries.filter(e => (e.name || "").toLowerCase().includes(q)));
}

async function refreshLeftPanel() {
  el("pathLabel").textContent = "/" + (currentPath || "");
  el("btnUp").disabled = !currentPath;
  syncUploadDir();

  setStatus("Loading…");
  const out = await listFolder(currentPath);

  const entries = normalizeEntries(out).sort((a, b) => {
    if (a.type !== b.type) return a.type === "dir" ? -1 : 1;
    return a.name.localeCompare(b.name, undefined, { sensitivity: "base" });
  });

  lastEntries = entries;

  if (!entries.length) {
    el("files").innerHTML = `<div class="muted" style="padding:8px 10px;">No files in this folder.</div>`;
    setStatus("Ready");
    return;
  }

  applyFilter();
  setStatus("Ready");
}

async function saveCurrent() {
  const path = (el("path")?.value || "").trim() || currentFilePath;
  if (!path) return alert("Open a file first.");

  setStatus("Saving…");
  await api("/admin/api/save", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ path, content: el("content").value })
  });

  // Invalidate cache so list reflects new files
  allPathsCache = null;
  await refreshLeftPanel();
  setStatus("Saved");
}

async function deleteCurrent() {
  const path = (el("path")?.value || "").trim() || currentFilePath;
  if (!path) return alert("Select a file/folder path first.");

  if (!confirm(`Delete '${path}'? This cannot be undone (but we keep backups for files).`)) return;

  setStatus("Deleting…");
  await api("/admin/api/delete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ path })
  });

  // Clear editor if we deleted the open file
  if (currentFilePath === path) {
    currentFilePath = null;
    el("path").value = "";
    el("content").value = "";
  }

  allPathsCache = null;
  await refreshLeftPanel();
  setStatus("Deleted");
}

async function uploadSelected() {
  const f = el("uploadFile").files?.[0];
  if (!f) return alert("Choose a file to upload.");

  const dir = (el("uploadDir").value || "").trim();

  setStatus("Uploading…");
  const fd = new FormData();
  fd.append("file", f);
  fd.append("dir", dir);

  await api("/admin/api/upload", { method: "POST", body: fd });

  el("uploadFile").value = "";
  allPathsCache = null;
  await refreshLeftPanel();
  setStatus("Uploaded");
}

async function showBackups() {
  const path = (el("path")?.value || "").trim() || currentFilePath;
  if (!path) return alert("Open a file first (backups are per file).");

  setStatus("Loading backups…");
  const r = await api(`/admin/api/backups?path=${encodeURIComponent(path)}`);

  const panel = el("backupPanel");
  const items = r.items || [];

  if (!items.length) {
    panel.innerHTML = `<div>No backups for <code>${escapeHtml(path)}</code>.</div>`;
    setStatus("Ready");
    return;
  }

  panel.innerHTML = `
    <div style="margin-top:6px;">
      <div class="muted" style="margin-bottom:6px;">Backups for <code>${escapeHtml(path)}</code>:</div>
      ${items.map(b => `
        <div class="row" style="justify-content:space-between; border:1px solid #22304a; border-radius:10px; padding:8px; margin-bottom:6px;">
          <div><code>${escapeHtml(b)}</code></div>
          <button class="btn" data-rollback="${escapeAttr(b)}">Rollback</button>
        </div>
      `).join("")}
    </div>
  `;

  // Wire rollback buttons
  panel.querySelectorAll("button[data-rollback]").forEach(btn => {
    btn.addEventListener("click", async () => {
      const backupFile = btn.getAttribute("data-rollback");
      if (!backupFile) return;

      if (!confirm(`Rollback '${path}' to backup '${backupFile}'?`)) return;

      setStatus("Rolling back…");
      await api("/admin/api/rollback", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ path, backupFile })
      });

      // Reload file into editor
      await openFile(path);
      setStatus("Rolled back");
    });
  });

  setStatus("Ready");
}

async function previewCurrent() {
  const path = (el("path")?.value || "").trim() || currentFilePath;
  if (!path) return alert("Open a file to preview.");

  // If they opened a .css/.js, try to preview its likely HTML sibling (optional).
  // For now: just preview the path they selected.
  const iframe = el("preview");
  const bust = `v=${Date.now()}`;

  // Ensure leading slash
  const urlPath = "/" + path.replace(/^\/+/, "");
  iframe.src = urlPath.includes("?") ? `${urlPath}&${bust}` : `${urlPath}?${bust}`;

  setStatus("Previewing");
}

async function checkLogin() {
  try {
    setStatus("Checking login…");

    // We are on /admin, so this is the perfect auth probe:
    // - 200 => logged in as admin
    // - 401/403 => not logged in / not admin
    const r = await fetch("/admin/api/list", { method: "GET" });

    if (r.status === 200) {
      setStatus("Logged in (admin)");
      return;
    }

    if (r.status === 401) {
      setStatus("Not logged in");
      return;
    }

    if (r.status === 403) {
      setStatus("Logged in (no admin)");
      return;
    }

    setStatus(`Login check: ${r.status}`);
  } catch (e) {
    console.warn("checkLogin failed", e);
    setStatus("Login check failed");
  }
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, c => ({ "&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;" }[c]));
}
function escapeAttr(s) {
  return escapeHtml(s).replace(/"/g, "&quot;");
}

// Wire buttons/events
document.addEventListener("DOMContentLoaded", () => {
  // Basic guards
  const must = ["btnUp","btnRefresh","btnUpload","btnSave","btnDelete","btnBackups","btnPreview","filter","files","content","path"];
  const missing = must.filter(id => !el(id));
  if (missing.length) {
    console.error("[admin] missing elements:", missing);
    return;
  }

  el("filter").value = "";

  el("btnUp").addEventListener("click", async () => {
    currentPath = parentPath(currentPath);
    el("filter").value = "";
    await refreshLeftPanel();
  });

  el("btnRefresh").addEventListener("click", async () => {
    allPathsCache = null;
    await refreshLeftPanel();
  });

  el("filter").addEventListener("input", applyFilter);

  el("btnUpload").addEventListener("click", () => uploadSelected().catch(err => alert(err.message)));

  el("btnSave").addEventListener("click", () => saveCurrent().catch(err => alert(err.message)));

  el("btnDelete").addEventListener("click", () => deleteCurrent().catch(err => alert(err.message)));

  el("btnBackups").addEventListener("click", () => showBackups().catch(err => alert(err.message)));

  el("btnPreview").addEventListener("click", () => previewCurrent().catch(err => alert(err.message)));

  el("btnMe")?.addEventListener("click", () => checkLogin().catch(() => {}));

  refreshLeftPanel().catch(err => {
    console.error(err);
    setStatus("Error");
  });

  // Optional: initial login check
  checkLogin().catch(() => {});
});
</script>
</body>
</html>
""";
}
