using hds;

namespace hds.Tests;

public class ConfigParserTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "hds-tests-" + Guid.NewGuid());

    public ConfigParserTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void LoadDbParamsReadsCurrentConfigShape()
    {
        string configPath = WriteConfig("""
            <?xml version="1.0" encoding="UTF-8"?>
            <Config>
              <Database>
                <Host>db-host</Host>
                <Port>3307</Port>
                <Username>mxo</Username>
                <Password>secret</Password>
                <Database>reality_hd</Database>
                <DbType>mysql</DbType>
                <Motd>welcome</Motd>
              </Database>
            </Config>
            """);

        XmlParser.loadDBParams(configPath, out DbParams dbParams);

        Assert.Equal("db-host", dbParams.Host);
        Assert.Equal(3307, dbParams.Port);
        Assert.Equal("mxo", dbParams.Username);
        Assert.Equal("secret", dbParams.Password);
        Assert.Equal("reality_hd", dbParams.DatabaseName);
        Assert.Equal("mysql", dbParams.DbType);
        Assert.Equal("welcome", dbParams.Motd);
    }

    [Fact]
    public void LoadDbParamsReadsLegacyConfigShape()
    {
        string configPath = WriteConfig("""
            <?xml version="1.0" encoding="UTF-8"?>
            <Config>
              <DBConfig>
                <serverHost>legacy-host</serverHost>
                <serverPort>3308</serverPort>
                <databaseName>legacy_db</databaseName>
                <databaseUser>legacy-user</databaseUser>
                <databasePassword>legacy-pass</databasePassword>
                <dbType>mysql</dbType>
                <motd>legacy motd</motd>
              </DBConfig>
            </Config>
            """);

        XmlParser.loadDBParams(configPath, out DbParams dbParams);

        Assert.Equal("legacy-host", dbParams.Host);
        Assert.Equal(3308, dbParams.Port);
        Assert.Equal("legacy-user", dbParams.Username);
        Assert.Equal("legacy-pass", dbParams.Password);
        Assert.Equal("legacy_db", dbParams.DatabaseName);
        Assert.Equal("mysql", dbParams.DbType);
        Assert.Equal("legacy motd", dbParams.Motd);
    }

    [Fact]
    public void LoadServerParamsUsesDefaultsForCurrentConfigShape()
    {
        string configPath = WriteConfig("""
            <?xml version="1.0" encoding="UTF-8"?>
            <Config>
              <Server>
              </Server>
            </Config>
            """);

        XmlParser.loadServerParams(configPath, out ServerParams serverParams);

        Assert.False(serverParams.AdminConsoleEnabled);
        Assert.Equal("cr2", serverParams.ServerType);
    }

    private string WriteConfig(string content)
    {
        string path = Path.Combine(tempDirectory, Guid.NewGuid() + ".xml");
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        Directory.Delete(tempDirectory, true);
    }
}
