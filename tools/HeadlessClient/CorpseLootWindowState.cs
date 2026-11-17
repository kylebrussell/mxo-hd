namespace HeadlessClient;

// Client6202088b returns the cached window without showing it again. Server811a destroys that
// window through621ca200/62020400; a following8119 can create a visible window again.
public sealed class CorpseLootWindowState
{
    public bool Cached { get; private set; }
    public bool Visible { get; private set; }
    public void CloseLocally() => Visible=false;
    public void Receive(byte[] rpc)
    {
        if(rpc.Length==2&&rpc[0]==0x81&&rpc[1]==0x1a){Cached=false;Visible=false;}
        if(rpc.Length>=18&&rpc[0]==0x81&&rpc[1]==0x19)
        {
            if(!Cached){Cached=true;Visible=true;}
        }
    }
}
