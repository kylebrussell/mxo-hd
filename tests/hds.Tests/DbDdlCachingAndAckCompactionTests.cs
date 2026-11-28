using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using hds;
using hds.databases;
using hds.shared;
using Xunit;

namespace hds.Tests;

[Collection("ConcentrationEnvironment")]
public class DbDdlCachingAndAckCompactionTests
{
    private sealed class ExecutedStatement
    {
        public string Sql { get; init; } = "";
        public Dictionary<string, object?> Parameters { get; } = new Dictionary<string, object?>();
        public bool Disposed { get; set; }
    }

    private sealed class StubParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        [AllowNull] public string ParameterName { get; set; } = "";
        [AllowNull] public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    private sealed class StubParameterCollection : List<object>, IDataParameterCollection
    {
        public object this[string parameterName]
        {
            get => this.Cast<IDataParameter>().First(p => p.ParameterName == parameterName);
            set
            {
                int idx = FindIndex(o => ((IDataParameter)o).ParameterName == parameterName);
                if (idx >= 0) this[idx] = value;
                else Add(value);
            }
        }
        public bool Contains(string parameterName) => this.Cast<IDataParameter>().Any(p => p.ParameterName == parameterName);
        public int IndexOf(string parameterName) => FindIndex(o => ((IDataParameter)o).ParameterName == parameterName);
        public void RemoveAt(string parameterName)
        {
            int idx = IndexOf(parameterName);
            if (idx >= 0) RemoveAt(idx);
        }
    }

    private sealed class StubReader : IDataReader
    {
        private readonly object?[][] rows;
        private int index = -1;
        public bool Closed { get; private set; }
        public bool Disposed { get; private set; }

        public StubReader(object?[][] rows) => this.rows = rows;

        public bool Read() => ++index < rows.Length;
        public void Close() => Closed = true;
        public void Dispose() { Closed = true; Disposed = true; }
        public int Depth => 0;
        public bool IsClosed => Closed;
        public int RecordsAffected => 0;
        public int FieldCount => index >= 0 && index < rows.Length ? rows[index].Length : 0;
        public object this[int i] => rows[index][i]!;
        public object this[string name] => throw new NotSupportedException();
        public bool GetBoolean(int i) => Convert.ToBoolean(rows[index][i]);
        public byte GetByte(int i) => Convert.ToByte(rows[index][i]);
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => Convert.ToChar(rows[index][i]);
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => "object";
        public DateTime GetDateTime(int i) => Convert.ToDateTime(rows[index][i]);
        public decimal GetDecimal(int i) => Convert.ToDecimal(rows[index][i]);
        public double GetDouble(int i) => Convert.ToDouble(rows[index][i]);
        public Type GetFieldType(int i) => rows[index][i]?.GetType() ?? typeof(object);
        public float GetFloat(int i) => Convert.ToSingle(rows[index][i]);
        public Guid GetGuid(int i) => Guid.Empty;
        public short GetInt16(int i) => Convert.ToInt16(rows[index][i]);
        public int GetInt32(int i) => Convert.ToInt32(rows[index][i]);
        public long GetInt64(int i) => Convert.ToInt64(rows[index][i]);
        public string GetName(int i) => "c" + i;
        public int GetOrdinal(string name) => 0;
        public DataTable GetSchemaTable() => new DataTable();
        public string GetString(int i) => Convert.ToString(rows[index][i]) ?? "";
        public object GetValue(int i) => rows[index][i]!;
        public int GetValues(object[] values) => 0;
        public bool IsDBNull(int i) => rows[index][i] == null || rows[index][i] == DBNull.Value;
        public bool NextResult() => false;
    }

    private sealed class StubCommand : IDbCommand
    {
        private readonly StubConnection conn;
        private ExecutedStatement? last;

        public StubCommand(StubConnection conn) => this.conn = conn;

        [AllowNull] public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDbConnection? Connection { get => conn; set { } }
        public IDataParameterCollection Parameters { get; } = new StubParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new StubParameter();
        public void Prepare() { }

        private ExecutedStatement Record()
        {
            var stmt = new ExecutedStatement { Sql = CommandText ?? "" };
            foreach (IDataParameter p in Parameters)
            {
                stmt.Parameters[p.ParameterName] = p.Value;
            }
            conn.Executed.Add(stmt);
            last = stmt;
            return stmt;
        }

        public int ExecuteNonQuery() { Record(); return 1; }
        public object ExecuteScalar() { Record(); return 0; }
        public IDataReader ExecuteReader()
        {
            var stmt = Record();
            var reader = conn.ReaderFactory != null ? conn.ReaderFactory(stmt) : new StubReader(Array.Empty<object?[]>());
            conn.Readers.Add(reader);
            return reader;
        }
        public IDataReader ExecuteReader(CommandBehavior behavior) => ExecuteReader();
        public void Dispose() { if (last != null) last.Disposed = true; }
    }

    private sealed class StubConnection : IDbConnection
    {
        public ConnectionState State { get; set; } = ConnectionState.Closed;
        [AllowNull] public string ConnectionString { get; set; }
        public List<ExecutedStatement> Executed { get; } = new List<ExecutedStatement>();
        public List<StubReader> Readers { get; } = new List<StubReader>();
        public Func<ExecutedStatement, StubReader>? ReaderFactory { get; set; }

        public StubConnection(string cs) => ConnectionString = cs;
        public int ConnectionTimeout => 15;
        public string Database => "test";
        public void Open() => State = ConnectionState.Open;
        public void Close() => State = ConnectionState.Closed;
        public IDbCommand CreateCommand() => new StubCommand(this);
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Dispose() => State = ConnectionState.Closed;
    }

    private static (MyWorldDbAccess db, StubConnection conn) CreateDb(string name)
    {
        StubConnection? created = null;
        var db = new MyWorldDbAccess("Server=127.0.0.1;Database=" + name + ";", cs =>
        {
            created = new StubConnection(cs);
            return created;
        });
        MyWorldDbAccess.ClearThreadStateForTesting();
        db.OpenConnection();
        db.CloseConnection();
        return (db, created!);
    }

    [Fact]
    public void EnsureRecipes_DeathPenalties_And_LocalSupport_CacheDdlOnceAndParameterizeQueries()
    {
        var (db, conn) = CreateDb("db_ddl_cache");
        try
        {
            conn.ReaderFactory = stmt =>
            {
                if (stmt.Sql.Contains("char_recipes", StringComparison.OrdinalIgnoreCase))
                    return new StubReader(new[] { new object?[] { 1234u } });
                if (stmt.Sql.Contains("char_death_penalty", StringComparison.OrdinalIgnoreCase))
                    return new StubReader(new[] { new object?[] { 0.85f, 42.5d } });
                if (stmt.Sql.Contains("local_support_tickets", StringComparison.OrdinalIgnoreCase))
                    return new StubReader(new[] { new object?[] { 9u, "Neo", "stuck", 100u, 101u, false } });
                return new StubReader(Array.Empty<object?[]>());
            };

            // Repeated recipe loads should run CREATE TABLE only once.
            var r1 = db.GetLearnedRecipes(77);
            var r2 = db.GetLearnedRecipes(78);
            Assert.Contains(1234u, r1);
            Assert.Contains(1234u, r2);
            Assert.Single(conn.Executed, s => s.Sql.StartsWith("CREATE TABLE IF NOT EXISTS char_recipes", StringComparison.Ordinal));
            var recipeSelects = conn.Executed.Where(s => s.Sql.StartsWith("SELECT goid FROM char_recipes", StringComparison.Ordinal)).ToList();
            Assert.Equal(2, recipeSelects.Count);
            Assert.Equal(77u, recipeSelects[0].Parameters["@charId"]);
            Assert.Equal(78u, recipeSelects[1].Parameters["@charId"]);

            // Repeated death penalty reads & saves should run CREATE TABLE only once and parameterize all values.
            var dp1 = db.GetDeathPenalty(55);
            db.SaveDeathPenalty(55, 0.75f, 99.25d);
            var dp2 = db.GetDeathPenalty(56);
            Assert.Equal(0.85f, dp1.Factor);
            Assert.Equal(42.5d, dp1.At);
            Assert.Equal(0.85f, dp2.Factor);
            Assert.Single(conn.Executed, s => s.Sql.StartsWith("CREATE TABLE IF NOT EXISTS char_death_penalty", StringComparison.Ordinal));
            var dpInsert = Assert.Single(conn.Executed, s => s.Sql.StartsWith("INSERT INTO char_death_penalty", StringComparison.Ordinal));
            Assert.Equal(55u, dpInsert.Parameters["@charId"]);
            Assert.Equal(0.75f, dpInsert.Parameters["@factor"]);
            Assert.Equal(99.25d, dpInsert.Parameters["@updatedAt"]);

            // Repeated support ticket reads should run CREATE TABLE only once and parameterize @charId.
            var t1 = db.SupportTickets(88);
            var t2 = db.SupportTickets(89);
            Assert.Single(t1);
            Assert.Single(t2);
            Assert.Single(conn.Executed, s => s.Sql.StartsWith("CREATE TABLE IF NOT EXISTS local_support_tickets", StringComparison.Ordinal));
            var ticketSelects = conn.Executed.Where(s => s.Sql.StartsWith("SELECT id,handle,body", StringComparison.Ordinal)).ToList();
            Assert.Equal(2, ticketSelects.Count);
            Assert.Equal(88u, ticketSelects[0].Parameters["@charId"]);
            Assert.Equal(89u, ticketSelects[1].Parameters["@charId"]);

            Assert.All(conn.Executed, s => Assert.True(s.Disposed));
            Assert.All(conn.Readers, r => Assert.True(r.Disposed));
        }
        finally
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void SetPlayerValues_And_SetRsiValues_UseParameterizedQueriesAndDisposeResources()
    {
        var (db, conn) = CreateDb("db_player_rsi");
        var previousClient = Store.currentClient;
        try
        {
            var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
            client.playerData = new ClientData();
            client.playerData.setCharID(321);
            client.playerInstance = new PlayerCharacter();
            Store.currentClient = client;

            conn.ReaderFactory = stmt =>
            {
                if (stmt.Sql.Contains("FROM characters", StringComparison.OrdinalIgnoreCase))
                {
                    return new StubReader(new[]
                    {
                        new object?[]
                        {
                            "Trinity", 10.0, 20.0, 30.0, (byte)4, 500, 500, 100, 100, (byte)15,
                            0u, (byte)1, 1, "First", "Last", 12345L, 6789L, "Slums", (short)1,
                            (short)2, (short)3, 10, 20, 30, 40, 50, 60, 70
                        }
                    });
                }
                if (stmt.Sql.Contains("FROM rsivalues", StringComparison.OrdinalIgnoreCase))
                {
                    object?[] rsiRow = Enumerable.Range(1, 22).Select(i => (object?)(short)i).ToArray();
                    return new StubReader(new[] { rsiRow });
                }
                return new StubReader(Array.Empty<object?[]>());
            };

            db.SetPlayerValues();
            db.SetRsiValues();

            Assert.Equal("Trinity", StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue()));
            Assert.Equal(12345L, client.playerData.getExperience());
            Assert.Equal(6789L, client.playerData.getInfo());
            Assert.Equal(1u, client.playerData.getDistrictId());
            Assert.Equal(22, client.playerData.getRsiValues().Length);
            Assert.Equal(1, client.playerData.getRsiValues()[0]);
            Assert.Equal(22, client.playerData.getRsiValues()[21]);

            var playerStmt = Assert.Single(conn.Executed, s => s.Sql.Contains("FROM characters", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("@charId", playerStmt.Sql);
            Assert.Equal(321u, playerStmt.Parameters["@charId"]);

            var rsiStmt = Assert.Single(conn.Executed, s => s.Sql.Contains("FROM rsivalues", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("@charId", rsiStmt.Sql);
            Assert.Equal(321u, rsiStmt.Parameters["@charId"]);

            Assert.Equal(ConnectionState.Closed, conn.State);
            Assert.All(conn.Executed, s => Assert.True(s.Disposed));
            Assert.All(conn.Readers, r => Assert.True(r.Disposed));
        }
        finally
        {
            Store.currentClient = previousClient;
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void DeletedAckedPackets_AdvancesFirstUnsentObjectIndexDirectlyAndSkipsEmptyQueues()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setPss(0);
        client.messageQueue = new MessageQueue();

        // Empty queues: must succeed cleanly without touching locks or corrupting cursor.
        client.DeletedAckedPackets(10);
        Assert.Equal(0, client.messageQueue.FirstUnsentObjectIndex);

        var acked1 = new SequencedMessage(new byte[] { 1, 2, 3 });
        acked1.MarkObjectSent(100);
        acked1.setNeededAck(5);

        var unackedSent = new SequencedMessage(new byte[] { 4, 5, 6 });
        unackedSent.MarkObjectSent(100);
        unackedSent.setNeededAck(12);

        var unsent1 = new SequencedMessage(new byte[] { 7, 8, 9 });
        var unsent2 = new SequencedMessage(new byte[] { 10, 11, 12 });

        client.messageQueue.ObjectMessagesQueue.Add(acked1);
        client.messageQueue.ObjectMessagesQueue.Add(unackedSent);
        client.messageQueue.ObjectMessagesQueue.Add(unsent1);
        client.messageQueue.ObjectMessagesQueue.Add(unsent2);

        // ACK sseq=5 removes acked1, keeps unackedSent at index 0, and shifts unsent1 to index 1.
        client.DeletedAckedPackets(5);
        Assert.Equal(3, client.messageQueue.ObjectMessagesQueue.Count);
        Assert.Same(unackedSent, client.messageQueue.ObjectMessagesQueue[0]);
        Assert.Same(unsent1, client.messageQueue.ObjectMessagesQueue[1]);
        Assert.Equal(1, client.messageQueue.FirstUnsentObjectIndex);

        // ACK sseq=12 removes unackedSent, leaving unsent1 at index 0.
        client.DeletedAckedPackets(12);
        Assert.Equal(2, client.messageQueue.ObjectMessagesQueue.Count);
        Assert.Same(unsent1, client.messageQueue.ObjectMessagesQueue[0]);
        Assert.Equal(0, client.messageQueue.FirstUnsentObjectIndex);
    }
}
