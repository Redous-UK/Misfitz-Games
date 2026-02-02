using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Hubs;
using Misfitz_Games.Services;
using System.Runtime.ConstrainedExecution;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace Misfitz_Games;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var pfxPath = builder.Configuration["DP_PFX_PATH"];     // e.g. /etc/secrets/dp.pfx
        var pfxPass = builder.Configuration["DP_PFX_PASSWORD"]; // secret

        var cert = new X509Certificate2(pfxPath, pfxPass);
        builder.Services.AddControllers();

        builder.Services
            .AddDataProtection()
            .SetApplicationName("misfitz-games-app")
            .PersistKeysToFileSystem(new DirectoryInfo(
                builder.Environment.IsProduction() ? "/data/" : "Data/"))
            .ProtectKeysWithCertificate(cert);

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
        builder.Services.AddSingleton<ContextoWordProvider>();

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        // Suppress EF Core SQL command logs
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        // Optional: quiet general EF noise too
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

        var app = builder.Build();

        // Needed behind Render/proxies
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        // Static files first is fine
        app.UseStaticFiles();

        app.UseRouting();

        app.UseCors("default");

        app.UseAuthentication();
        app.UseAuthorization();

        // Create/migrate DB on startup (simple and effective)
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        app.MapControllers();
        app.MapHub<RoomHub>("/hubs/room");

        app.MapGet("/livez", () => Results.Ok(new
        {
            ok = true,
            service = "Misfitz-Games",
            utc = DateTimeOffset.UtcNow
        }));

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

        app.Run();
    }
}