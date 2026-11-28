using System;
using System.Collections.Generic;
using System.Data;
using hds;
using hds.accounts;
using hds.databases;
using hds.databases.Entities;
using hds.shared;

namespace hds.Tests;

public class DatabaseConnectionReliabilityTests
{
    private sealed class StubReader : IDataReader
    {
        public bool Closed { get; private set; }
        public bool Disposed { get; private set; }
        public int Depth => 0;
        public bool IsClosed => Closed;
        public int RecordsAffected => 0;
        public int FieldCount => 0;
        public object this[int i] => throw new NotImplementedException();
        public object this[string name] => throw new NotImplementedException();
        public void Close() => Closed = true;
        public void Dispose() { Closed = true; Disposed = true; }
        public bool Read() => false;
        public bool NextResult() => false;
        public DataTable? GetSchemaTable() => null;
        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotImplementedException();
        public string GetDataTypeName(int i) => "";
        public DateTime GetDateTime(int i) => default;
        public decimal GetDecimal(int i) => 0;
        public double GetDouble(int i) => 0;
        public Type GetFieldType(int i) => typeof(object);
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => default;
        public short GetInt16(int i) => 0;
        public int GetInt32(int i) => 0;
        public long GetInt64(int i) => 0;
        public string GetName(int i) => "";
        public int GetOrdinal(string name) => -1;
        public string GetString(int i) => "";
        public object GetValue(int i) => DBNull.Value;
        public int GetValues(object[] values) => 0;
        public bool IsDBNull(int i) => true;
    }

    private sealed class StubCommand : IDbCommand
    {
        private readonly StubConnection _connection;
        public bool ThrowOnExecute { get; set; }
        public bool Disposed { get; private set; }
        public int ExecuteCount { get; private set; }
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters => throw new NotImplementedException();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }
        public StubCommand(StubConnection connection) { _connection = connection; Connection = connection; }
        public void Cancel() { }
        public IDbDataParameter CreateParameter() => throw new NotImplementedException();
        public void Dispose() => Disposed = true;
        public int ExecuteNonQuery()
        {
            ExecuteCount++;
            if (ThrowOnExecute) throw new InvalidOperationException("Simulated SQL error");
            return 1;
        }
        public IDataReader ExecuteReader() => new StubReader();
        public IDataReader ExecuteReader(CommandBehavior behavior) => new StubReader();
        public object ExecuteScalar() => 0;
        public void Prepare() { }
    }

    private sealed class StubConnection : IDbConnection
    {
        public string ConnectionString { get; set; }
        public ConnectionState State { get; set; } = ConnectionState.Closed;
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public bool Disposed { get; private set; }
        public bool NextCommandThrows { get; set; }
        public List<StubCommand> CreatedCommands { get; } = new List<StubCommand>();

        public StubConnection(string cs) => ConnectionString = cs;
        public int ConnectionTimeout => 15;
        public string Database => "";
        public IDbTransaction BeginTransaction() => throw new NotImplementedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();
        public void ChangeDatabase(string databaseName) { }
        public void Open()
        {
            if (State != ConnectionState.Closed)
                throw new InvalidOperationException("Cannot open non-closed connection");
            OpenCount++;
            State = ConnectionState.Open;
        }
        public void Close()
        {
            CloseCount++;
            State = ConnectionState.Closed;
        }
        public IDbCommand CreateCommand()
        {
            var cmd = new StubCommand(this) { ThrowOnExecute = NextCommandThrows };
            CreatedCommands.Add(cmd);
            return cmd;
        }
        public void Dispose()
        {
            Disposed = true;
            State = ConnectionState.Closed;
        }
    }

    [Fact]
    public void OpenConnectionRecoversFromAbandonedReaderAndBrokenState()
    {
        var created = new List<StubConnection>();
        var db = new MyWorldDbAccess("Server=127.0.0.1;Database=db_a;", cs =>
        {
            var c = new StubConnection(cs);
            created.Add(c);
            return c;
        });

        try
        {
            MyWorldDbAccess.ClearThreadStateForTesting();

            db.OpenConnection();
            Assert.Single(created);
            var first = created[0];
            Assert.Equal(ConnectionState.Open, first.State);

            // Simulate a legacy method throwing mid-read and leaving an unclosed reader + command.
            var abandonedReader = new StubReader();
            var abandonedCmd = new StubCommand(first);
            MyWorldDbAccess.SetAbandonedReaderForTesting(abandonedReader, abandonedCmd);

            // Next OpenConnection on the same thread must dispose the abandoned reader/command
            // and replace the poisoned connection instead of failing with "open DataReader".
            db.OpenConnection();
            Assert.True(abandonedReader.Closed);
            Assert.True(abandonedReader.Disposed);
            Assert.True(abandonedCmd.Disposed);
            Assert.True(first.Disposed);
            Assert.Equal(2, created.Count);
            Assert.Equal(ConnectionState.Open, created[1].State);

            // Simulate MySQL dropping the connection into Broken state.
            created[1].State = ConnectionState.Broken;
            db.OpenConnection();
            Assert.True(created[1].Disposed);
            Assert.Equal(3, created.Count);
            Assert.Equal(ConnectionState.Open, created[2].State);

            // CloseConnection returns clean connection to Closed state for reuse.
            db.CloseConnection();
            Assert.Equal(ConnectionState.Closed, created[2].State);
            Assert.False(created[2].Disposed);
        }
        finally
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void OpenConnectionReplacesConnectionWhenDatabaseChangesOnSameThread()
    {
        var created = new List<StubConnection>();
        var dbA = new MyWorldDbAccess("Server=127.0.0.1;Database=mxotest_a;", cs =>
        {
            var c = new StubConnection(cs);
            created.Add(c);
            return c;
        });
        var dbB = new MyWorldDbAccess("Server=127.0.0.1;Database=mxotest_b;", cs =>
        {
            var c = new StubConnection(cs);
            created.Add(c);
            return c;
        });

        try
        {
            MyWorldDbAccess.ClearThreadStateForTesting();

            dbA.OpenConnection();
            dbA.CloseConnection();
            Assert.Single(created);
            Assert.Equal("Server=127.0.0.1;Database=mxotest_a;", created[0].ConnectionString);

            dbB.OpenConnection();
            Assert.Equal(2, created.Count);
            Assert.True(created[0].Disposed);
            Assert.Equal("Server=127.0.0.1;Database=mxotest_b;", created[1].ConnectionString);
            dbB.CloseConnection();
        }
        finally
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void ExecuteNonResultQueryAlwaysClosesAndDisposesCommandOnException()
    {
        var created = new List<StubConnection>();
        var db = new MyWorldDbAccess("Server=127.0.0.1;Database=db_err;", cs =>
        {
            var c = new StubConnection(cs) { NextCommandThrows = true };
            created.Add(c);
            return c;
        });

        try
        {
            MyWorldDbAccess.ClearThreadStateForTesting();

            Assert.Throws<InvalidOperationException>(() => db.ExecuteNonResultQuery("UPDATE broken"));
            Assert.Single(created);
            Assert.Equal(ConnectionState.Closed, created[0].State);
            Assert.Single(created[0].CreatedCommands);
            Assert.True(created[0].CreatedCommands[0].Disposed);

            // Subsequent query on the same thread succeeds cleanly.
            created[0].NextCommandThrows = false;
            db.ExecuteNonResultQuery("UPDATE ok");
            Assert.Equal(ConnectionState.Closed, created[0].State);
            Assert.Equal(2, created[0].OpenCount);
        }
        finally
        {
            MyWorldDbAccess.ClearThreadStateForTesting();
        }
    }

    [Fact]
    public void MatrixDbContextCachesOptionsPerConnectionString()
    {
        var paramsA1 = new DbParams { Host = "127.0.0.1", Port = 3306, Username = "u", Password = "p", DatabaseName = "mxo_cache_a" };
        var paramsA2 = new DbParams { Host = "127.0.0.1", Port = 3306, Username = "u", Password = "p", DatabaseName = "mxo_cache_a" };
        var paramsB = new DbParams { Host = "127.0.0.1", Port = 3306, Username = "u", Password = "p", DatabaseName = "mxo_cache_b" };

        var optsA1 = MatrixDbContext.GetOrCreateOptions(paramsA1);
        var optsA2 = MatrixDbContext.GetOrCreateOptions(paramsA2);
        var optsB = MatrixDbContext.GetOrCreateOptions(paramsB);

        Assert.Same(optsA1, optsA2);
        Assert.NotSame(optsA1, optsB);
    }

    [Fact]
    public void RsiColumnAllowListAcceptsValidColumnsAndRejectsInjection()
    {
        Assert.True(MyMarginDBAccess.IsAllowedRsiColumn("sex"));
        Assert.True(MyMarginDBAccess.IsAllowedRsiColumn("ShirtColor"));
        Assert.True(MyMarginDBAccess.IsAllowedRsiColumn("leggins"));
        Assert.True(MyMarginDBAccess.IsAllowedRsiColumn("facialdetailcolor"));

        Assert.False(MyMarginDBAccess.IsAllowedRsiColumn(null!));
        Assert.False(MyMarginDBAccess.IsAllowedRsiColumn(""));
        Assert.False(MyMarginDBAccess.IsAllowedRsiColumn("charid"));
        Assert.False(MyMarginDBAccess.IsAllowedRsiColumn("sex=1; DROP TABLE characters; --"));
        Assert.False(MyMarginDBAccess.IsAllowedRsiColumn("unknown_col"));
    }
}
