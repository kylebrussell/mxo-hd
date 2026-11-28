using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using hds;
using hds.databases;
using hds.shared;
using Xunit;

namespace hds.Tests;

[Collection("ConcentrationEnvironment")]
public class WorldDbSafetyAndIgnoreCleanupTests
{
    private sealed class RecordingParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    private sealed class RecordingParameterCollection : List<object>, IDataParameterCollection
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

    private sealed class RecordingReader : IDataReader
    {
        private readonly object?[][] rows;
        private readonly bool throwOnRead;
        private int index = -1;
        public bool Closed { get; private set; }
        public bool Disposed { get; private set; }
        public Action? OnClose;

        public RecordingReader(object?[][] rows, bool throwOnRead = false)
        {
            this.rows = rows;
            this.throwOnRead = throwOnRead;
        }

        public bool Read()
        {
            if (throwOnRead) throw new InvalidOperationException("Simulated reader failure");
            index++;
            return index < rows.Length;
        }

        public void Close()
        {
            if (!Closed)
            {
                Closed = true;
                OnClose?.Invoke();
            }
        }

        public void Dispose()
        {
            Close();
            Disposed = true;
        }

        public object GetValue(int i) => rows[index][i] ?? DBNull.Value;
        public bool IsDBNull(int i) => rows[index][i] == null || rows[index][i] == DBNull.Value;
        public string GetString(int i) => (string)rows[index][i]!;
        public int GetInt32(int i) => Convert.ToInt32(rows[index][i]);
        public short GetInt16(int i) => Convert.ToInt16(rows[index][i]);
        public long GetInt64(int i) => Convert.ToInt64(rows[index][i]);
        public float GetFloat(int i) => Convert.ToSingle(rows[index][i]);
        public double GetDouble(int i) => Convert.ToDouble(rows[index][i]);
        public bool GetBoolean(int i) => Convert.ToBoolean(rows[index][i]);
        public DateTime GetDateTime(int i) => (DateTime)rows[index][i]!;
        public byte GetByte(int i) => Convert.ToByte(rows[index][i]);
        public decimal GetDecimal(int i) => Convert.ToDecimal(rows[index][i]);
        public char GetChar(int i) => Convert.ToChar(rows[index][i]);
        public Guid GetGuid(int i) => (Guid)rows[index][i]!;
        public int FieldCount => rows.Length > 0 ? rows[0].Length : 0;
        public int Depth => 0;
        public bool IsClosed => Closed;
        public int RecordsAffected => 0;
        public object this[int i] => GetValue(i);
        public object this[string name] => throw new NotSupportedException();
        public DataTable GetSchemaTable() => throw new NotSupportedException();
        public bool NextResult() => false;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => "";
        public Type GetFieldType(int i) => typeof(object);
        public string GetName(int i) => "";
        public int GetOrdinal(string name) => 0;
        public int GetValues(object[] values) => 0;
    }

    private sealed class ExecutedStatement
    {
        public string Sql { get; init; } = "";
        public Dictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
        public bool Disposed { get; set; }
    }

    private sealed class RecordingCommand : IDbCommand
    {
        private readonly RecordingConnection conn;
        private readonly RecordingParameterCollection parameters = new RecordingParameterCollection();
        public bool Disposed { get; private set; }
        private ExecutedStatement? lastRecorded;

        public RecordingCommand(RecordingConnection conn) => this.conn = conn;

        public IDbConnection? Connection { get => conn; set { } }
        public IDbTransaction? Transaction { get; set; }
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDataParameterCollection Parameters => parameters;
        public UpdateRowSource UpdatedRowSource { get; set; }

        public IDbDataParameter CreateParameter() => new RecordingParameter();
        public void Cancel() { }
        public void Prepare() { }

        private ExecutedStatement Record()
        {
            var snap = new ExecutedStatement
            {
                Sql = CommandText,
                Parameters = parameters.Cast<IDataParameter>().ToDictionary(p => p.ParameterName, p => p.Value)
            };
            conn.Executed.Add(snap);
            lastRecorded = snap;
            return snap;
        }

        public int ExecuteNonQuery()
        {
            if (conn.ActiveReaders > 0) throw new InvalidOperationException("Cannot execute while another DataReader is open.");
            Record();
            return 1;
        }

        public IDataReader ExecuteReader()
        {
            if (conn.ActiveReaders > 0) throw new InvalidOperationException("Cannot open a nested DataReader on the same connection.");
            var stmt = Record();
            var reader = conn.NextReaderFactory != null
                ? conn.NextReaderFactory(stmt)
                : new RecordingReader(Array.Empty<object?[]>());
            conn.ActiveReaders++;
            reader.OnClose = () => conn.ActiveReaders--;
            conn.CreatedReaders.Add(reader);
            return reader;
        }

        public IDataReader ExecuteReader(CommandBehavior behavior) => ExecuteReader();

        public object ExecuteScalar()
        {
            if (conn.ActiveReaders > 0) throw new InvalidOperationException("Cannot execute scalar while another DataReader is open.");
            var stmt = Record();
            return conn.NextScalarFactory != null ? conn.NextScalarFactory(stmt) : 0;
        }

        public void Dispose()
        {
            Disposed = true;
            if (lastRecorded != null) lastRecorded.Disposed = true;
        }
    }

    private sealed class RecordingConnection : IDbConnection
    {
        public ConnectionState State { get; set; } = ConnectionState.Closed;
        public string ConnectionString { get; set; }
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public bool Disposed { get; private set; }
        public int ActiveReaders { get; set; }
        public List<ExecutedStatement> Executed { get; } = new List<ExecutedStatement>();
        public List<RecordingReader> CreatedReaders { get; } = new List<RecordingReader>();
        public Func<ExecutedStatement, RecordingReader>? NextReaderFactory { get; set; }
        public Func<ExecutedStatement, object>? NextScalarFactory { get; set; }

        public RecordingConnection(string cs) => ConnectionString = cs;
        public int ConnectionTimeout => 15;
        public string Database => "test";

        public void Open()
        {
            OpenCount++;
            State = ConnectionState.Open;
        }

        public void Close()
        {
            CloseCount++;
            State = ConnectionState.Closed;
        }

        public IDbCommand CreateCommand() => new RecordingCommand(this);
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Dispose()
        {
            Disposed = true;
            State = ConnectionState.Closed;
        }
    }

    private static (MyWorldDbAccess db, List<RecordingConnection> connections) CreateStubDb(string dbName)
    {
        var connections = new List<RecordingConnection>();
        var db = new MyWorldDbAccess("Server=127.0.0.1;Database=" + dbName + ";", cs =>
        {
            var c = new RecordingConnection(cs);
            connections.Add(c);
            return c;
        });
        return (db, connections);
    }

    [Fact]
    public void QueryRows_ClosesConnectionAndDisposesReaderOnReadFailure()
    {
        var (db, connections) = CreateStubDb("db_query_err");
        try
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
            bool shouldThrow = true;
            db.OpenConnection();
            var conn = connections[0];
            conn.NextReaderFactory = _ => shouldThrow
                ? new RecordingReader(Array.Empty<object?[]>(), throwOnRead: true)
                : new RecordingReader(new[] { new object?[] { 42 } });
            db.CloseConnection();

            Assert.Throws<InvalidOperationException>(() => db.GetCharIdByHandle("Neo"));
            Assert.Equal(ConnectionState.Closed, conn.State);
            Assert.Equal(0, conn.ActiveReaders);
            Assert.All(conn.CreatedReaders, r => Assert.True(r.Closed && r.Disposed));
            Assert.All(conn.Executed, s => Assert.True(s.Disposed));

            // Next query on the same thread reuses the clean connection without resetting it
            shouldThrow = false;
            Assert.Equal(42u, db.GetCharIdByHandle("Neo"));
            Assert.Single(connections);
            Assert.Equal(ConnectionState.Closed, conn.State);
        }
        finally
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void IsCrewNameAvailable_ClosesFirstReaderBeforeFollowUpQueriesAndBindsQuotes()
    {
        var (db, connections) = CreateStubDb("db_crew_avail");
        try
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
            const string trickyName = "O'Brien's \"Crew\"";
            DateTime oldDeletedAt = DateTime.Now.AddHours(-48);

            db.OpenConnection();
            var conn = connections[0];
            conn.NextReaderFactory = stmt =>
            {
                if (stmt.Sql.Contains("FROM crews WHERE crew_name = @crewName"))
                {
                    return new RecordingReader(new[] { new object?[] { 77, oldDeletedAt, oldDeletedAt } });
                }
                if (stmt.Sql.Contains("COUNT(cm.id)"))
                {
                    return new RecordingReader(new[] { new object?[] { (short)1 } });
                }
                return new RecordingReader(Array.Empty<object?[]>());
            };
            db.CloseConnection();

            // Must not throw nested-reader exception; expires and deletes crew 77 -> available
            bool available = db.IsCrewNameAvailable(trickyName);
            Assert.True(available);
            Assert.Single(connections);
            Assert.Equal(0, conn.ActiveReaders);
            Assert.Equal(ConnectionState.Closed, conn.State);

            // Verify raw crewName passed verbatim as @crewName parameter (no SQL concat or backslash mangling)
            Assert.Equal(5, conn.Executed.Count); // SELECT crew + SELECT count + 3 DeleteCrew statements
            Assert.DoesNotContain("O'Brien", conn.Executed[0].Sql);
            Assert.Equal(trickyName, conn.Executed[0].Parameters["@crewName"]);
            Assert.Equal(trickyName, conn.Executed[1].Parameters["@crewName"]);
            Assert.Equal(77u, conn.Executed[2].Parameters["@crewId"]);
        }
        finally
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void SetReputation_Org_And_PvpFlag_ValidateColumnAndOpenConnectionOnce()
    {
        var (db, connections) = CreateStubDb("db_rep_once");
        try
        {
            MyWorldDbAccess.ClearThreadStateForTesting();

            Assert.Throws<ArgumentException>(() => db.SetReputation(10, "repZion = 1; DROP TABLE characters; --", 500));
            Assert.Throws<ArgumentException>(() => db.SetReputation(10, "", 500));
            Assert.Throws<ArgumentException>(() => db.SetReputation(10, null!, 500));
            Assert.Empty(connections);

            db.SetReputation(10, "repZion", 250);
            db.SetOrgId(10, 3);
            db.SetPvpFlag(10, true);

            Assert.Single(connections);
            var conn = connections[0];
            Assert.False(conn.Disposed);
            Assert.Equal(3, conn.OpenCount); // Exactly one open per call, no double-OpenConnection reset
            Assert.Equal(3, conn.CloseCount);
            Assert.Equal(250, conn.Executed[0].Parameters["@value"]);
            Assert.Equal(10u, conn.Executed[0].Parameters["@charId"]);
            Assert.Equal(3, conn.Executed[1].Parameters["@orgId"]);
            Assert.Equal(1, conn.Executed[2].Parameters["@flagged"]);
        }
        finally
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void GetCharInfoByHandle_ParameterizesInputAndHandlesEmptySafely()
    {
        var (db, connections) = CreateStubDb("db_handle_info");
        try
        {
            MyWorldDbAccess.ClearThreadStateForTesting();

            Assert.Empty(db.GetCharInfoByHandle(""));
            Assert.Empty(db.GetCharInfoByHandle(null!));
            Assert.Empty(connections);

            const string payload = "Neo' OR '1'='1\0";
            db.GetCharInfoByHandle(payload);

            Assert.Single(connections);
            var conn = connections[0];
            Assert.Single(conn.Executed);
            Assert.DoesNotContain("OR '1'='1", conn.Executed[0].Sql);
            Assert.Equal("Neo' OR '1'='1", conn.Executed[0].Parameters["@handle"]);
            Assert.Equal(ConnectionState.Closed, conn.State);
        }
        finally
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void GetCrewsForFaction_ResolvesMasterCharIdInSingleQuery()
    {
        var (db, connections) = CreateStubDb("db_faction_crews");
        try
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
            db.OpenConnection();
            var conn = connections[0];
            conn.NextReaderFactory = _ => new RecordingReader(new[]
            {
                new object?[] { 10, "CrewAlpha", "CaptainA", 5, (short)1, (short)1, 1001 },
                new object?[] { 11, "CrewBeta", "CaptainB", 5, (short)0, DBNull.Value, DBNull.Value }
            });
            db.CloseConnection();

            var crews = db.GetCrewsForFaction(5);
            Assert.Equal(2, crews.Count);
            Assert.Equal(1001u, crews[0].masterPlayerCharId);
            Assert.Equal((ushort)1, crews[0].masterIsOnline);
            Assert.Equal(0u, crews[1].masterPlayerCharId);
            Assert.Equal((ushort)0, crews[1].masterIsOnline);
            Assert.Single(conn.Executed); // No N+1 GetCharIdByHandle follow-up queries
        }
        finally
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void EnsureMissionTable_RunsCreateTableOncePerInstance()
    {
        var (db, connections) = CreateStubDb("db_missions_once");
        try
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
            db.GetCompletedMissions(101);
            db.SaveMissionCompleted(101, 55);
            db.GetCompletedMissions(102);

            Assert.Single(connections);
            var conn = connections[0];
            int ddlCount = conn.Executed.Count(s => s.Sql.StartsWith("CREATE TABLE IF NOT EXISTS char_missions", StringComparison.Ordinal));
            Assert.Equal(1, ddlCount);
            Assert.Equal(4, conn.Executed.Count); // 1 DDL + 2 SELECT + 1 INSERT
        }
        finally
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void UpdateAbilityLoadOut_SkipsEmptyListAndUsesSingleConnection()
    {
        var (db, connections) = CreateStubDb("db_loadout");
        WorldClient oldClient = Store.currentClient;
        try
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
            var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
            client.playerData = new ClientData();
            client.playerData.setCharID(77);
            Store.currentClient = client;

            db.UpdateAbilityLoadOut(new List<ushort>(), 1);
            db.UpdateAbilityLoadOut(null!, 1);
            Assert.Empty(connections);

            db.UpdateAbilityLoadOut(new List<ushort> { 2, 5, 9 }, 1);
            Assert.Single(connections);
            var conn = connections[0];
            Assert.Equal(1, conn.OpenCount);
            Assert.Equal(3, conn.Executed.Count);
            Assert.Equal((ushort)2, conn.Executed[0].Parameters["@slot"]);
            Assert.Equal((ushort)5, conn.Executed[1].Parameters["@slot"]);
            Assert.Equal((ushort)9, conn.Executed[2].Parameters["@slot"]);
            Assert.Equal(ConnectionState.Closed, conn.State);
        }
        finally
        {
            Store.currentClient = oldClient;
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void IgnoreFilter_SkipsEmptySetsAndEvictsOnDisconnect()
    {
        const uint charIdA = 900001;
        const uint charIdB = 900002;
        var clientA = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        clientA.playerData = new ClientData();
        clientA.playerData.setCharID(charIdA);

        try
        {
            IgnoreFilter.RemovePlayer(charIdA);
            IgnoreFilter.RemovePlayer(charIdB);
            int baseline = IgnoreFilter.TrackedCharacterCount;

            // Empty or null ignore lists must not leak an entry into the static dictionary
            IgnoreFilter.Load(charIdA, Array.Empty<string>());
            IgnoreFilter.Load(charIdB, null!);
            Assert.Equal(baseline, IgnoreFilter.TrackedCharacterCount);

            // Non-empty list tracks the character and filters matches case-insensitively
            IgnoreFilter.Load(charIdA, new[] { "Spammer" });
            Assert.Equal(baseline + 1, IgnoreFilter.TrackedCharacterCount);
            Assert.True(IgnoreFilter.Ignores(clientA, "spammer"));
            Assert.False(IgnoreFilter.Ignores(clientA, "Friend"));

            // Reloading with empty clears the entry
            IgnoreFilter.Load(charIdA, Array.Empty<string>());
            Assert.Equal(baseline, IgnoreFilter.TrackedCharacterCount);
            Assert.False(IgnoreFilter.Ignores(clientA, "Spammer"));

            // Add + RemovePlayer (called from SocialHandler.PlayerLeftWorld) evicts cleanly
            IgnoreFilter.Add(charIdA, "Spammer");
            Assert.Equal(baseline + 1, IgnoreFilter.TrackedCharacterCount);
            IgnoreFilter.RemovePlayer(charIdA);
            Assert.Equal(baseline, IgnoreFilter.TrackedCharacterCount);
            Assert.False(IgnoreFilter.Ignores(clientA, "Spammer"));
        }
        finally
        {
            IgnoreFilter.RemovePlayer(charIdA);
            IgnoreFilter.RemovePlayer(charIdB);
        }
    }
}
