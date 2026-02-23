using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;

namespace Misfitz_Games.Controllers.Admin;

[ApiController]
public sealed partial class AdminDbController(IConfiguration config) : ControllerBase
{
    private readonly IConfiguration _config = config;

    // --------- Security gate ----------
    private bool IsAuthorized()
    {
        // Allow bypass via env flag (TEMPORARY)
        if (_config["ADMIN_DB_NO_AUTH"] == "true")
            return true;

        var expected = _config["ADMIN_SECRET"];
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        var provided = Request.Headers["X-Misfitz-Secret"].ToString();
        return FixedTimeEquals(provided, expected);
    }

    private string DbPath => _config["DB_PATH"] ?? "/data/misfitz.db";

    private SqliteConnection Open()
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = DbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        return new SqliteConnection(cs);
    }

    // --------- Viewer endpoints ----------

    // GET /admin/db/overview
    [HttpGet("/admin/db/overview")]
    public IActionResult Overview()
    {
        if (!IsAuthorized()) return Unauthorized(new { ok = false });

        using var conn = Open();
        conn.Open();

        var tables = new List<object>();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT name
                FROM sqlite_master
                WHERE type='table' AND name NOT LIKE 'sqlite_%'
                ORDER BY name;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                tables.Add(new { name = r.GetString(0) });
        }

        return Ok(new { ok = true, dbPath = DbPath, tables });
    }

    // GET /admin/db/schema?table=AppUsers
    [HttpGet("/admin/db/schema")]
    public IActionResult Schema([FromQuery] string table)
    {
        if (!IsAuthorized()) return Unauthorized(new { ok = false });
        if (!IsSafeIdent(table)) return BadRequest(new { ok = false, error = "Invalid table name" });

        using var conn = Open();
        conn.Open();

        if (!TableExists(conn, table))
            return NotFound(new { ok = false, error = $"Table '{table}' not found." });

        var cols = new List<object>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                cols.Add(new
                {
                    cid = r.GetInt32(r.GetOrdinal("cid")),
                    name = r.GetString(r.GetOrdinal("name")),
                    type = r.GetString(r.GetOrdinal("type")),
                    notnull = r.GetInt32(r.GetOrdinal("notnull")) == 1,
                    dflt_value = r.IsDBNull(r.GetOrdinal("dflt_value")) ? null : r.GetString(r.GetOrdinal("dflt_value")),
                    pk = r.GetInt32(r.GetOrdinal("pk")) == 1
                });
            }
        }

        long count;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT COUNT(1) FROM \"{table}\";";
            count = (long)(cmd.ExecuteScalar() ?? 0L);
        }

        return Ok(new { ok = true, table, rowCount = count, columns = cols });
    }

    // GET /admin/db/rows?table=AppUsers&limit=50&offset=0
    [HttpGet("/admin/db/rows")]
    public IActionResult Rows([FromQuery] string table, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        if (!IsAuthorized()) return Unauthorized(new { ok = false });
        if (!IsSafeIdent(table)) return BadRequest(new { ok = false, error = "Invalid table name" });

        limit = Math.Clamp(limit, 1, 200);
        offset = Math.Max(0, offset);

        using var conn = Open();
        conn.Open();

        if (!TableExists(conn, table))
            return NotFound(new { ok = false, error = $"Table '{table}' not found." });

        // Basic: return rows as list of dictionaries (column -> value)
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{table}\" LIMIT $limit OFFSET $offset;";
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$offset", offset);

        using var r = cmd.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();

        while (r.Read())
        {
            var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < r.FieldCount; i++)
            {
                var col = r.GetName(i);
                d[col] = r.IsDBNull(i) ? null : r.GetValue(i);
            }
            rows.Add(d);
        }

        return Ok(new { ok = true, table, limit, offset, rows });
    }

    // --------- Limited, explicit mutations ----------

    // -- Used to have alter table until fixed EF migration

    // POST /admin/db/update/user-name
    // Body: { "table":"AppUsers", "idColumn":"Id", "id":"123", "name":"Craig" }
    // This is an example of a SAFE targeted update (you can tailor it to your schema).
    public sealed record UpdateNameRequest(string Table, string IdColumn, string Id, string Name);

    [HttpPost("/admin/db/update/user-name")]
    public IActionResult UpdateUserName([FromBody] UpdateNameRequest req)
    {
        if (!IsAuthorized()) return Unauthorized(new { ok = false });

        if (!IsSafeIdent(req.Table) || !IsSafeIdent(req.IdColumn))
            return BadRequest(new { ok = false, error = "Invalid identifiers" });

        // Keep it tight: name length limits
        var name = (req.Name ?? "").Trim();
        if (name.Length is < 1 or > 64)
            return BadRequest(new { ok = false, error = "Name must be 1-64 chars." });

        using var conn = Open();
        conn.Open();

        if (!TableExists(conn, req.Table))
            return NotFound(new { ok = false, error = $"Table '{req.Table}' not found." });

        // Parameterized values = safe
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE \"{req.Table}\" SET \"Name\" = $name WHERE \"{req.IdColumn}\" = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", req.Id);

        var affected = cmd.ExecuteNonQuery();
        return Ok(new { ok = true, affected });
    }

    [HttpPost("/admin/db/fill-null-names")]
    public IActionResult FillNullNames()
    {
        if (!IsAuthorized())
            return Unauthorized(new { ok = false });
        _ = _config["DB_PATH"] ?? "/data/misfitz.db";

        const string table = "Users"; // 🔴 CHANGE

        using var conn = Open();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"UPDATE \"{table}\" SET \"Name\" = 'Unknown' WHERE \"Name\" IS NULL;";

        var affected = cmd.ExecuteNonQuery();

        return Ok(new { ok = true, affected });
    }

    [HttpPost("/admin/db/query")]
    public IActionResult Query([FromBody] SqlQueryRequest req)
    {
        // 🔒 Protect this endpoint
        if (!Request.Headers.TryGetValue("X-Misfitz-Secret", out var secret)
            || secret != "YOUR_SECRET_HERE")
        {
            return Unauthorized(new { ok = false });
        }

        var sql = (req?.Sql ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sql))
            return BadRequest(new { ok = false, error = "SQL required" });

        var path = "/data/misfitz.db";

        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        // Decide if query returns rows
        if (sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
         || sql.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = cmd.ExecuteReader();

            var cols = Enumerable.Range(0, reader.FieldCount)
                                 .Select(reader.GetName)
                                 .ToArray();

            var rows = new List<object[]>();

            while (reader.Read())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                rows.Add(values);
            }

            return Ok(new { ok = true, columns = cols, rows });
        }
        else
        {
            var affected = cmd.ExecuteNonQuery();
            return Ok(new { ok = true, affected });
        }
    }


public sealed class SqlQueryRequest
{
    public string? Sql { get; set; }
}

    // --------- Helpers ----------

    private static bool IsSafeIdent(string? s) => !string.IsNullOrWhiteSpace(s)
            && Regex.IsMatch(s, "^[A-Za-z_][A-Za-z0-9_]*$");

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

    private static bool FixedTimeEquals(string? a, string? b)
    {
        a ??= "";
        b ??= "";
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

}