using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Misfitz_Games.Controllers.Admin;

[ApiController]
public sealed class AdminMigrationsController(IConfiguration config) : ControllerBase
{
    private readonly IConfiguration _config = config;

    // POST /admin/db/add-name-column
    [HttpPost("/admin/db/add-name-column")]
    public IActionResult AddNameColumn()
    {
        // --- Simple shared-secret gate (use your existing admin auth if you have it)
        // Set env var: ADMIN_SECRET=some-long-random
        var expected = _config["ADMIN_SECRET"];
        if (!string.IsNullOrWhiteSpace(expected))
        {
            var provided = Request.Headers["X-Misfitz-Secret"].ToString();
            if (!FixedTimeEquals(provided, expected))
                return Unauthorized(new { ok = false, error = "Unauthorized" });
        }

        var dbPath = _config["DB_PATH"] ?? "/data/misfitz.db";

        // TODO: change this to your actual table name
        const string tableName = "Users";
        const string columnName = "Name";
        const string columnDef = "TEXT NULL";

        using var conn = Open(dbPath);
        conn.Open();

        if (!TableExists(conn, tableName))
            return NotFound(new { ok = false, error = $"Table '{tableName}' not found." });

        if (ColumnExists(conn, tableName, columnName))
            return Ok(new { ok = true, changed = false, message = "Column already exists." });

        // Hardcoded identifiers => no injection surface here
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDef};";
        cmd.ExecuteNonQuery();

        return Ok(new { ok = true, changed = true, message = $"Added column {tableName}.{columnName}" });
    }

    private static SqliteConnection Open(string dbPath)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        return new SqliteConnection(cs);
    }

    private static bool TableExists(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1;";
        cmd.Parameters.AddWithValue("$name", tableName);
        return cmd.ExecuteScalar() is not null;
    }

    private static bool ColumnExists(SqliteConnection conn, string tableName, string columnName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var name = r.GetString(r.GetOrdinal("name"));
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Prevent trivial timing attacks on secret comparisons
    private static bool FixedTimeEquals(string? a, string? b)
    {
        a ??= "";
        b ??= "";
        if (a.Length != b.Length) return false;

        var diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];

        return diff == 0;
    }

    public sealed record SetNameRequest(
    string Id,
    string Name
);

    [HttpPost("/admin/db/set-name")]
    public IActionResult SetName([FromBody] SetNameRequest req)
    {
        var expected = _config["ADMIN_SECRET"];
        if (!string.IsNullOrWhiteSpace(expected))
        {
            var provided = Request.Headers["X-Misfitz-Secret"].ToString();
            if (!FixedTimeEquals(provided, expected))
                return Unauthorized(new { ok = false, error = "Unauthorized" });
        }

        var dbPath = _config["DB_PATH"] ?? "/data/misfitz.db";

        const string table = "Users";   // 🔴 CHANGE THIS
        const string idColumn = "Name";      // 🔴 CHANGE THIS

        if (string.IsNullOrWhiteSpace(req.Id))
            return BadRequest(new { ok = false, error = "Id required" });

        var name = (req.Name ?? "").Trim();

        using var conn = Open(dbPath);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"UPDATE \"{table}\" SET \"Name\" = $name WHERE \"{idColumn}\" = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", req.Id);

        var affected = cmd.ExecuteNonQuery();

        return Ok(new { ok = true, affected });
    }
}