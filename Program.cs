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

            var options = ConfigurationOptions.Parse(redisUrl, ignoreUnknown: true);
            options.AbortOnConnectFail = false;   // keep retrying
            options.ConnectRetry = 5;
            options.ConnectTimeout = 10000;

            return ConnectionMultiplexer.Connect(options);
        }
);
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

        app.Run();
    }
}