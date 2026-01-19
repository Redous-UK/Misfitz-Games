using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Misfitz_Games.Hubs;
using Misfitz_Games.Services;

namespace Misfitz_Games;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddSignalR();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("default", p =>
                p.AllowAnyHeader()
                 .AllowAnyMethod()
                 .AllowCredentials()
                 .SetIsOriginAllowed(_ => true));
        });

        // Redis factory (lazy, async)
        builder.Services.AddSingleton<RedisMuxFactory>();

        // App services
        builder.Services.AddSingleton<IRoomStateStore, RedisRoomStateStore>();
        builder.Services.AddSingleton<ContextoEngine>();
        builder.Services.AddSingleton<RoomBroadcastService>();
        builder.Services.AddSingleton<ContextoWordProvider>();

        var jwtSecret = builder.Configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
            throw new InvalidOperationException("JWT_SECRET not set");
        var keyBytes = Encoding.UTF8.GetBytes(jwtSecret);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !app.Environment.IsDevelopment();
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                // Read JWT from HttpOnly cookie: mf_admin
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Cookies["mf_admin"];
                        if (!string.IsNullOrWhiteSpace(token))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", p => p.RequireClaim("role", "admin"));
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
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseStaticFiles();

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

        app.Run();
    }
}