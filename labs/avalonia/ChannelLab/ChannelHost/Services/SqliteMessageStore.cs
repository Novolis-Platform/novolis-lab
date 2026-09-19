using ChannelHost.Contracts;
using Microsoft.Data.Sqlite;
using Novolis.Messaging.SecureText;

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

    public async Task AppendAsync(SecureTextRelayEnvelopeDto message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var envelope = SecureTextEnvelopeCodec.Deserialize(message.Envelope);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO secure_messages (id, channel, from_nick, to_nick, envelope, at_utc)
                VALUES ($id, $channel, $fromNick, $toNick, $envelope, $at);
                """;
            cmd.Parameters.AddWithValue("$id", envelope.Header.MessageId.ToString("D"));
            cmd.Parameters.AddWithValue("$channel", message.Channel);
            cmd.Parameters.AddWithValue("$fromNick", message.FromNick);
            cmd.Parameters.AddWithValue("$toNick", message.ToNick);
            cmd.Parameters.Add("$envelope", SqliteType.Blob).Value = message.Envelope;
            cmd.Parameters.AddWithValue("$at", envelope.Header.SentAtUtc.UtcDateTime.ToString("O"));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SecureTextRelayEnvelopeDto>> GetRecentAsync(
        string channel,
        string nick,
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
                SELECT channel, from_nick, to_nick, envelope
                FROM secure_messages
                WHERE channel = $channel AND (from_nick = $nick OR to_nick = $nick)
                ORDER BY at_utc DESC
                LIMIT $take;
                """;
            cmd.Parameters.AddWithValue("$channel", channel);
            cmd.Parameters.AddWithValue("$nick", nick);
            cmd.Parameters.AddWithValue("$take", take);

            var rows = new List<SecureTextRelayEnvelopeDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new SecureTextRelayEnvelopeDto(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetFieldValue<byte[]>(3)));
            }

            rows.Reverse();
            return rows;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AppendGroupAsync(
        SecureTextGroupRelayEnvelopeDto message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var envelope = SecureTextEnvelopeCodec.Deserialize(message.Envelope);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO secure_group_messages (id, channel, group_id, group_name, from_nick, to_nick, envelope, at_utc)
                VALUES ($id, $channel, $groupId, $groupName, $fromNick, $toNick, $envelope, $at);
                """;
            cmd.Parameters.AddWithValue("$id", envelope.Header.MessageId.ToString("D"));
            cmd.Parameters.AddWithValue("$channel", message.Channel);
            cmd.Parameters.AddWithValue("$groupId", message.GroupId.ToString("D"));
            cmd.Parameters.AddWithValue("$groupName", message.GroupName);
            cmd.Parameters.AddWithValue("$fromNick", message.FromNick);
            cmd.Parameters.AddWithValue("$toNick", message.ToNick);
            cmd.Parameters.Add("$envelope", SqliteType.Blob).Value = message.Envelope;
            cmd.Parameters.AddWithValue("$at", envelope.Header.SentAtUtc.UtcDateTime.ToString("O"));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SecureTextGroupRelayEnvelopeDto>> GetRecentGroupAsync(
        string channel,
        Guid groupId,
        string nick,
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
                SELECT channel, group_id, group_name, from_nick, to_nick, envelope
                FROM secure_group_messages
                WHERE channel = $channel AND group_id = $groupId
                  AND (from_nick = $nick OR to_nick = $nick)
                ORDER BY at_utc DESC
                LIMIT $take;
                """;
            cmd.Parameters.AddWithValue("$channel", channel);
            cmd.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
            cmd.Parameters.AddWithValue("$nick", nick);
            cmd.Parameters.AddWithValue("$take", take);

            var rows = new List<SecureTextGroupRelayEnvelopeDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new SecureTextGroupRelayEnvelopeDto(
                    reader.GetString(0),
                    Guid.Parse(reader.GetString(1)),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetFieldValue<byte[]>(5)));
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
            CREATE TABLE IF NOT EXISTS secure_messages (
                id TEXT PRIMARY KEY,
                channel TEXT NOT NULL,
                from_nick TEXT NOT NULL,
                to_nick TEXT NOT NULL,
                envelope BLOB NOT NULL,
                at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_secure_messages_channel_at ON secure_messages(channel, at_utc);
            CREATE TABLE IF NOT EXISTS secure_group_messages (
                id TEXT PRIMARY KEY,
                channel TEXT NOT NULL,
                group_id TEXT NOT NULL,
                group_name TEXT NOT NULL,
                from_nick TEXT NOT NULL,
                to_nick TEXT NOT NULL,
                envelope BLOB NOT NULL,
                at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_secure_group_messages_group_at ON secure_group_messages(channel, group_id, at_utc);
            """;
        cmd.ExecuteNonQuery();
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
