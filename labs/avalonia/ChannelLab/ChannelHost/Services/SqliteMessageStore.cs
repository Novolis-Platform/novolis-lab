using Microsoft.Data.Sqlite;
using Novolis.Chat.Abstractions;
using Novolis.Chat.Hosting.AspNetCore;
using Novolis.Messaging.SecureText;

namespace ChannelHost.Services;

/// <summary>
/// Product-owned append-only scrollback. Protected bodies stay byte-for-byte
/// opaque while the public ChatFrame is persisted beside each envelope.
/// </summary>
public sealed class SqliteMessageStore : IChatHistoryStore, IAsyncDisposable
{
    readonly string _connectionString;
    readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteMessageStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "ChannelLab");
        Directory.CreateDirectory(directory);
        _connectionString = $"Data Source={Path.Combine(directory, "messages.db")}";
        EnsureSchema();
    }

    public async Task AppendAsync(
        SecureTextRelayEnvelopeDto message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _ = SecureTextEnvelopeCodec.Deserialize(message.Envelope);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO secure_messages
                    (id, channel, from_nick, to_nick, thread_id, parent_id, reaction, body_format, envelope, at_utc)
                VALUES
                    ($id, $channel, $fromNick, $toNick, $threadId, $parentId, $reaction, $bodyFormat, $envelope, $at);
                """;
            AddFrameParameters(command, message.Frame);
            command.Parameters.Add("$envelope", SqliteType.Blob).Value = message.Envelope;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, channel, from_nick, to_nick, thread_id, parent_id, reaction, body_format, envelope, at_utc
                FROM secure_messages
                WHERE channel = $channel AND (from_nick = $nick OR to_nick = $nick)
                ORDER BY at_utc DESC
                LIMIT $take;
                """;
            command.Parameters.AddWithValue("$channel", channel);
            command.Parameters.AddWithValue("$nick", nick);
            command.Parameters.AddWithValue("$take", take);

            var rows = new List<SecureTextRelayEnvelopeDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new SecureTextRelayEnvelopeDto(
                    ReadFrame(reader),
                    reader.GetFieldValue<byte[]>(8)));
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
        _ = SecureTextEnvelopeCodec.Deserialize(message.Envelope);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO secure_group_messages
                    (id, channel, group_id, group_name, from_nick, to_nick, thread_id, parent_id, reaction, body_format, envelope, at_utc)
                VALUES
                    ($id, $channel, $groupId, $groupName, $fromNick, $toNick, $threadId, $parentId, $reaction, $bodyFormat, $envelope, $at);
                """;
            AddFrameParameters(command, message.Frame);
            command.Parameters.AddWithValue("$groupId", message.GroupId.ToString("D"));
            command.Parameters.AddWithValue("$groupName", message.GroupName);
            command.Parameters.Add("$envelope", SqliteType.Blob).Value = message.Envelope;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, channel, group_id, group_name, from_nick, to_nick,
                       thread_id, parent_id, reaction, body_format, envelope, at_utc
                FROM secure_group_messages
                WHERE channel = $channel AND group_id = $groupId
                  AND (from_nick = $nick OR to_nick = $nick)
                ORDER BY at_utc DESC
                LIMIT $take;
                """;
            command.Parameters.AddWithValue("$channel", channel);
            command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
            command.Parameters.AddWithValue("$nick", nick);
            command.Parameters.AddWithValue("$take", take);

            var rows = new List<SecureTextGroupRelayEnvelopeDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new SecureTextGroupRelayEnvelopeDto(
                    ReadGroupFrame(reader),
                    Guid.Parse(reader.GetString(2)),
                    reader.GetString(3),
                    reader.GetFieldValue<byte[]>(10)));
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
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS secure_messages (
                id TEXT PRIMARY KEY,
                channel TEXT NOT NULL,
                from_nick TEXT NOT NULL,
                to_nick TEXT NOT NULL,
                thread_id TEXT NULL,
                parent_id TEXT NULL,
                reaction TEXT NULL,
                body_format INTEGER NOT NULL DEFAULT 0,
                envelope BLOB NOT NULL,
                at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_secure_messages_channel_at
                ON secure_messages(channel, at_utc);
            CREATE TABLE IF NOT EXISTS secure_group_messages (
                id TEXT PRIMARY KEY,
                channel TEXT NOT NULL,
                group_id TEXT NOT NULL,
                group_name TEXT NOT NULL,
                from_nick TEXT NOT NULL,
                to_nick TEXT NOT NULL,
                thread_id TEXT NULL,
                parent_id TEXT NULL,
                reaction TEXT NULL,
                body_format INTEGER NOT NULL DEFAULT 0,
                envelope BLOB NOT NULL,
                at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_secure_group_messages_group_at
                ON secure_group_messages(channel, group_id, at_utc);
            """;
        command.ExecuteNonQuery();

        EnsureColumn(connection, "secure_messages", "thread_id", "TEXT NULL");
        EnsureColumn(connection, "secure_messages", "parent_id", "TEXT NULL");
        EnsureColumn(connection, "secure_messages", "reaction", "TEXT NULL");
        EnsureColumn(connection, "secure_messages", "body_format", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "secure_group_messages", "thread_id", "TEXT NULL");
        EnsureColumn(connection, "secure_group_messages", "parent_id", "TEXT NULL");
        EnsureColumn(connection, "secure_group_messages", "reaction", "TEXT NULL");
        EnsureColumn(connection, "secure_group_messages", "body_format", "INTEGER NOT NULL DEFAULT 0");
    }

    static void AddFrameParameters(SqliteCommand command, ChatFrame frame)
    {
        command.Parameters.AddWithValue("$id", frame.MessageId.ToString("D"));
        command.Parameters.AddWithValue("$channel", frame.Conversation);
        command.Parameters.AddWithValue("$fromNick", frame.FromNick);
        command.Parameters.AddWithValue("$toNick", frame.ToNick);
        command.Parameters.AddWithValue(
            "$threadId",
            frame.Thread is { } thread ? thread.Value.ToString("D") : DBNull.Value);
        command.Parameters.AddWithValue(
            "$parentId",
            frame.Parent is { } parent ? parent.Value.ToString("D") : DBNull.Value);
        command.Parameters.AddWithValue(
            "$reaction",
            frame.Reaction is null ? DBNull.Value : frame.Reaction);
        command.Parameters.AddWithValue("$bodyFormat", (int)frame.BodyFormat);
        command.Parameters.AddWithValue("$at", frame.SentAtUtc.UtcDateTime.ToString("O"));
    }

    static ChatFrame ReadFrame(SqliteDataReader reader, int offset = 0)
    {
        var messageId = Guid.Parse(reader.GetString(offset));
        var conversation = reader.GetString(offset + 1);
        var fromNick = reader.GetString(offset + 2);
        var toNick = reader.GetString(offset + 3);
        var thread = ReadGuid(reader, offset + 4);
        var parent = ReadGuid(reader, offset + 5);
        var reaction = reader.IsDBNull(offset + 6) ? null : reader.GetString(offset + 6);
        var bodyFormat = reader.GetInt32(offset + 7);
        var sentAt = DateTimeOffset.Parse(reader.GetString(offset + 9));
        return ChatFrame.Create(
            conversation,
            messageId,
            fromNick,
            toNick,
            sentAt,
            thread is { } threadId ? ThreadId.FromGuid(threadId) : null,
            parent is { } parentId ? MessageRef.FromGuid(parentId) : null,
            reaction,
            (ChatBodyFormat)bodyFormat);
    }

    static ChatFrame ReadGroupFrame(SqliteDataReader reader)
    {
        var messageId = Guid.Parse(reader.GetString(0));
        var conversation = reader.GetString(1);
        var fromNick = reader.GetString(4);
        var toNick = reader.GetString(5);
        var thread = ReadGuid(reader, 6);
        var parent = ReadGuid(reader, 7);
        var reaction = reader.IsDBNull(8) ? null : reader.GetString(8);
        var bodyFormat = reader.GetInt32(9);
        var sentAt = DateTimeOffset.Parse(reader.GetString(11));
        return ChatFrame.Create(
            conversation,
            messageId,
            fromNick,
            toNick,
            sentAt,
            thread is { } threadId ? ThreadId.FromGuid(threadId) : null,
            parent is { } parentId ? MessageRef.FromGuid(parentId) : null,
            reaction,
            (ChatBodyFormat)bodyFormat);
    }

    static Guid? ReadGuid(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Guid.Parse(reader.GetString(ordinal));

    static void EnsureColumn(
        SqliteConnection connection,
        string table,
        string column,
        string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
