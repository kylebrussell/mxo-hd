using hds;
using System.Text;

namespace hds.Tests;

public class ClientActionTests
{
    [Theory]
    [InlineData("110",110,true)]
    [InlineData("1, 110, 200",110,true)]
    [InlineData("1110,1100",110,false)]
    [InlineData("110",0,false)]
    [InlineData("CSR_Tester",110,false)]
    [InlineData("",110,false)]
    public void GmAuthorityIsExactServerAllowlist(string list, uint id, bool allowed) =>
        Assert.Equal(allowed, ClientActionRequests.TrustedGm(id,list));

    [Fact]
    public void PetitionAcceptsUtf8AndRejectsMalformedPointersChunksAndTrailingData()
    {
        byte[] b = { 0,0,0,0, 10,0, 0,0, 4,0, 0xc3,0xa9,0x78,0 };
        Assert.Equal("éx",ClientActionRequests.ParsePetition(b).Text);
        foreach (int offset in new[] { 4,6,7,8,13 })
        {
            byte[] corrupt=(byte[])b.Clone(); corrupt[offset]=0xff;
            Assert.Null(ClientActionRequests.ParsePetition(corrupt));
        }
        Assert.Null(ClientActionRequests.ParsePetition(b.Concat(new byte[]{0}).ToArray()));
        Assert.Null(ClientActionRequests.ParsePetition(new byte[]{0,0,0,0,0,0,0,0}));
        var cancel=ClientActionRequests.ParsePetition(new byte[]{7,0,0,0,0,0,0,1});
        Assert.True(cancel.Cancel); Assert.Equal(7u,cancel.Ticket);
    }
    [Fact]
    public void CompileIsExactStorageRecordAndNeverTreatsBagFlagAsArchiveAuthority()
    {
        byte[] b={0xa2,0x1c,0,0, 0x34,0x12,0,3,1};
        var r=ClientActionRequests.ParseStoredCompile(b);
        Assert.Equal(7330u,r.Item); Assert.Equal(0x1234,r.Slot); Assert.Equal(3,r.Tool); Assert.True(r.FromCrafting);
        b[6]=1; Assert.Null(ClientActionRequests.ParseStoredCompile(b));
        b[6]=0; b[8]=2; Assert.Null(ClientActionRequests.ParseStoredCompile(b));
        Assert.Null(ClientActionRequests.ParseStoredCompile(new byte[10]));
    }
    [Fact]
    public void EchoKeepsUnsignedElapsedTimeWithoutMutatingInput()
    {
        byte[] b={0xff,0xff,0xff,0x80};
        Assert.Equal(new byte[]{0x81,0x4d,0xff,0xff,0xff,0x80},ClientActionRequests.Echo(b));
        Assert.Null(ClientActionRequests.Echo(new byte[3]));
    }
    private static string Text(byte[] b,int pointer,int basis=0)
    {
        int at=BitConverter.ToUInt16(b,pointer)+basis;
        int size=BitConverter.ToUInt16(b,at);
        Assert.Equal(0,b[at+1+size]);
        return Encoding.UTF8.GetString(b,at+2,size-1);
    }
    [Fact]
    public void SupportPacketsHaveNativeOffsetsAndEmbeddedRecordBases()
    {
        var t=new LocalSupport.Ticket{Id=42,Created=123,Modified=456,Handle="Owned",Text="Details"};
        byte[] list=LocalSupport.Header(t,false);
        Assert.Equal(42u,BitConverter.ToUInt32(list,3)); Assert.Equal("Owned",Text(list,27,2)); Assert.Equal(0,list[29]);
        Assert.Equal(30,LocalSupport.Header(null,false).Length);
        byte[] detail=LocalSupport.Detail(t,42); Assert.Equal("Details",Text(detail,10));
        Assert.Equal(0u,BitConverter.ToUInt32(detail,6));
        Assert.NotEqual(0u,BitConverter.ToUInt32(LocalSupport.Detail(null,42),6));
        byte[] categories=LocalSupport.Categories();
        Assert.Equal("Local server support",Text(categories,4,2));
        Assert.Equal("General assistance",Text(categories,21,19));
        Assert.Equal(17,BitConverter.ToUInt16(categories,14)); Assert.Equal(1,BitConverter.ToUInt16(categories,17));
        var a=LocalSupport.Articles[0]; byte[] search=LocalSupport.SearchResult(a,false);
        Assert.Equal(a.Title,Text(search,6)); Assert.Equal(a.Id,Text(search,8));
        byte[] article=LocalSupport.ArticleResult(a); Assert.Equal(a.Text,Text(article,8));
    }
    [Fact]
    public void KnowledgeBaseIsLocalAndSearchesCaseInsensitively()
    {
        Assert.Single(LocalSupport.Search("STUCK")); Assert.Empty(LocalSupport.Search("unfindable-query"));
        Assert.Contains("no SOE", LocalSupport.Articles[0].Text);
    }
}
