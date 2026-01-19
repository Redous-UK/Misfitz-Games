using Microsoft.AspNetCore.HttpOverrides;
using Misfitz_Games.Hubs;
using Misfitz_Games.Services;
using StackExchange.Redis;

namespace Misfitz_Games;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<Misfitz_Games.Services.RoomBroadcastService>();

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisUrl = builder.Configuration["REDIS_URL"]
                ?? throw new InvalidOperationException("REDIS_URL not set");

            var uri = new Uri(redisUrl);

            var userInfo = uri.UserInfo.Split(':', 2);
            var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

            var opts = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ConnectRetry = 10,
                ConnectTimeout = 20000,
                KeepAlive = 30,

                // External Render Key Value needs TLS:
                Ssl = true,
                SslHost = uri.Host, // important for SNI
            };

            opts.EndPoints.Add(uri.Host, uri.Port);

            if (!string.IsNullOrWhiteSpace(password)) opts.Password = password;
            if (!string.IsNullOrWhiteSpace(username)) opts.User = username;

            return ConnectionMultiplexer.Connect(opts);
        });


        builder.Services.AddSingleton<IRoomStateStore, RedisRoomStateStore>();
        builder.Services.AddSingleton<ContextoEngine>();
        builder.Services.AddSingleton<RoomBroadcastService>();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("default", p =>
                p.AllowAnyHeader()
                 .AllowAnyMethod()
                 .AllowCredentials()
                 .SetIsOriginAllowed(_ => true));
        });

        var app = builder.Build();

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto
        });

        app.UseRouting();
        app.UseCors("default");

        app.UseStaticFiles();

        app.MapControllers();
        app.MapHub<RoomHub>("/hubs/room");

        app.MapGet("/livez", () => Results.Ok(new
        {
            ok = true,
            service = "Misfitz-Games",
            utc = DateTimeOffset.UtcNow
        }));

        app.MapGet("/debug/redis", (StackExchange.Redis.IConnectionMultiplexer mux) =>
        {
            var endpoints = mux.GetEndPoints().Select(e => e.ToString()).ToArray();

            return Results.Ok(new
            {
                isConnected = mux.IsConnected,
                endpoints
            });
        });

        app.Run();
    }
}