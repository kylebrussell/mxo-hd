using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.databases;
using hds.shared;
namespace hds.Tests;
public class PlayerValueReaderCleanupTests
{
    public class DbProxy : DispatchProxy
    {
        public Func<MethodInfo,object?[]?,object?> Handler=null!;
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
    }
    private static T Proxy<T>(Func<MethodInfo,object?[]?,object?> handler) where T:class
    {
        var proxy=DispatchProxy.Create<T,DbProxy>();((DbProxy)(object)proxy).Handler=handler;return proxy;
    }
    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)]
    public void MappingOrQueryFailureAlwaysClosesTheConnectionAndOwnedReader(bool failQuery,bool failClose)
    {
        using var table=new DataTable();for(int i=0;i<6;i++)table.Columns.Add("c"+i,typeof(object));
        table.Rows.Add("CleanupActor",0d,495d,0d,192d,-1); // A different field conversion fails after yaw succeeds.
        using var reader=table.CreateDataReader();using var sentinel=table.CreateDataReader();var state=ConnectionState.Open;int closeCalls=0;
        var ownedReader=Proxy<IDataReader>((method,args)=>
        {
            if(method.Name=="Close"){closeCalls++;reader.Close();if(failClose)throw new InvalidOperationException("reader close fixture failure");return null;}
            return method.Invoke(reader,args);
        });
        var command=Proxy<IDbCommand>((method,args)=>method.Name=="ExecuteReader"
            ? failQuery?throw new InvalidOperationException("query fixture failure"):ownedReader : null);
        var connection=Proxy<IDbConnection>((method,args)=>method.Name switch
        {
            "get_State"=>state,"CreateCommand"=>command,"Close"=>Close(),_=>null
        });
        object? Close(){state=ConnectionState.Closed;return null;}
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var connectionField=typeof(MyWorldDbAccess).GetField("threadConnection",flags)!;
        var commandField=typeof(MyWorldDbAccess).GetField("threadCommand",flags)!;
        var readerField=typeof(MyWorldDbAccess).GetField("threadReader",flags)!;
        var oldReader=readerField.GetValue(null);var oldConnection=connectionField.GetValue(null);var oldCommand=commandField.GetValue(null);var oldClient=Store.currentClient;
        try
        {
            connectionField.SetValue(null,connection);readerField.SetValue(null,sentinel);
            var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
            client.playerData=new ClientData();client.playerData.setCharID(110);client.playerInstance=new PlayerCharacter();Store.currentClient=client;
            var access=(MyWorldDbAccess)RuntimeHelpers.GetUninitializedObject(typeof(MyWorldDbAccess));
            if(failQuery || failClose)Assert.Throws<InvalidOperationException>(()=>access.SetPlayerValues());
            else Assert.Throws<OverflowException>(()=>access.SetPlayerValues());
            Assert.Equal(ConnectionState.Closed,state);Assert.Equal(failQuery?0:1,closeCalls);
            Assert.Same(sentinel,readerField.GetValue(null));Assert.False(sentinel.IsClosed);
            if(!failQuery)Assert.True(reader.IsClosed);
        }
        finally{readerField.SetValue(null,oldReader);connectionField.SetValue(null,oldConnection);commandField.SetValue(null,oldCommand);Store.currentClient=oldClient;}
    }
}
