using StackExchange.Redis;

namespace Misfitz_Games.Services;

public sealed class RedisMuxFactory(IConfiguration config)
{
    private readonly Lazy<Task<IConnectionMultiplexer>> _lazy = new(() => ConnectAsync(config));

    public Task<IConnectionMultiplexer> GetAsync() => _lazy.Value;

    private static async Task<IConnectionMultiplexer> ConnectAsync(IConfiguration config)
    {
        var redisUrl = config["REDIS_URL"]
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
            Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase),
            SslHost = uri.Host,
        };

        opts.EndPoints.Add(uri.Host, uri.Port);

        if (!string.IsNullOrWhiteSpace(password)) opts.Password = password;
        if (!string.IsNullOrWhiteSpace(username)) opts.User = username;

        return await ConnectionMultiplexer.ConnectAsync(opts);
    }
}