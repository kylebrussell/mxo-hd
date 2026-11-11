using System.Collections.Generic;
using hds;

namespace hds.Tests;

// 81 90 and 81 91 echo the Message / token the server sent; a registered callback runs once, anything else goes on
// to the tutorial.
public class ClientCallbackTests
{
    [Fact]
    public void AModalAnswerRunsItsCallbackOnce()
    {
        ClientCallbacks.State state = new ClientCallbacks.State();
        List<string> heard = new List<string>();
        uint first = ClientCallbacks.RegisterModal(state, left => heard.Add("first " + left));
        uint second = ClientCallbacks.RegisterModal(state, left => heard.Add("second " + left));

        Assert.True(first >= ClientCallbacks.FirstModalMessage);
        Assert.NotEqual(first, second);
        Assert.True(ClientCallbacks.OnModalAnswer(state, second, false));
        Assert.True(ClientCallbacks.OnModalAnswer(state, first, true));
        Assert.False(ClientCallbacks.OnModalAnswer(state, first, true));   // answered already
        Assert.False(ClientCallbacks.OnModalAnswer(state, 7, true));       // a tutorial prompt's Message
        Assert.Equal(new[] { "second False", "first True" }, heard);
    }

    [Fact]
    public void AnFxTokenRunsItsCallbackOnceAndAFailingOneIsContained()
    {
        ClientCallbacks.State state = new ClientCallbacks.State();
        int ended = 0;
        uint token = ClientCallbacks.RegisterFx(state, () => ended++);
        uint broken = ClientCallbacks.RegisterFx(state, () => throw new System.InvalidOperationException("boom"));

        Assert.True(ClientCallbacks.OnFxEnded(state, token));
        Assert.False(ClientCallbacks.OnFxEnded(state, token));
        Assert.True(ClientCallbacks.OnFxEnded(state, broken));
        Assert.False(ClientCallbacks.OnFxEnded(state, 1));
        Assert.Equal(1, ended);
    }

    [Fact]
    public void OnlyTheNewestCallbacksAreKept()
    {
        ClientCallbacks.State state = new ClientCallbacks.State();
        uint oldest = ClientCallbacks.RegisterModal(state, _ => { });
        for (int i = 0; i < ClientCallbacks.MaxPending; i++)
        {
            ClientCallbacks.RegisterModal(state, _ => { });
        }

        Assert.False(ClientCallbacks.OnModalAnswer(state, oldest, true));
    }
}
