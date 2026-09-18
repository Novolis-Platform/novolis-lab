using ChannelHost.Contracts;
using Microsoft.Data.Sqlite;

namespace ChannelHost.Services;

/// <summary>
/// Append-only channel scrollback. Uses Microsoft.Data.Sqlite directly because
/// Novolis.Storage.Sqlite currently fails to build (IKeyed / IRepository API drift).
/// Path matches the plan: %LocalAppData%/Novolis/ChannelLab/
/// </summary>
public sealed class SqliteMessageStore : IAsyncDisposable
{
    readonly string _connectionString;
    readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteMessageStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "ChannelLab");
        Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={Path.Combine(dir, "messages.db")}";
        EnsureSchema();
    }

    public async Task AppendAsync(ChannelMessageDto message, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO messages (id, channel, nick, body, at_utc)
                VALUES ($id, $channel, $nick, $body, $at);
                """;
            cmd.Parameters.AddWithValue("$id", Guid.CreateVersion7().ToString("D"));
            cmd.Parameters.AddWithValue("$channel", message.Channel);
            cmd.Parameters.AddWithValue("$nick", message.Nick);
            cmd.Parameters.AddWithValue("$body", message.Body);
            cmd.Parameters.AddWithValue("$at", message.At.UtcDateTime.ToString("O"));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ChannelMessageDto>> GetRecentAsync(
        string channel,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT channel, nick, body, at_utc
                FROM messages
                WHERE channel = $channel
                ORDER BY at_utc DESC
                LIMIT $take;
                """;
            cmd.Parameters.AddWithValue("$channel", channel);
            cmd.Parameters.AddWithValue("$take", take);

            var rows = new List<ChannelMessageDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ChannelMessageDto(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    DateTimeOffset.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind)));
            }

            rows.Reverse();
            return rows;
        }
        finally
        {
            _gate.Release();
        }
    }

    void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS messages (
                id TEXT PRIMARY KEY,
                channel TEXT NOT NULL,
                nick TEXT NOT NULL,
                body TEXT NOT NULL,
                at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_messages_channel_at ON messages(channel, at_utc);
            """;
        cmd.ExecuteNonQuery();
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
