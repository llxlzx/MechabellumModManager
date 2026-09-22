namespace BlackBox.Core;

public struct QuitRequestState
{
    public bool DefiniteQuit;
    public bool Requested;
    public int HeartbeatsSinceRequest;

    public void OnQuitRequested()
    {
        Requested = true;
        HeartbeatsSinceRequest = 0;
    }

    public void OnDefiniteQuit() => DefiniteQuit = true;

    public void OnHeartbeatWritten()
    {
        if (!Requested || DefiniteQuit)
            return;
        HeartbeatsSinceRequest++;
        if (HeartbeatsSinceRequest >= StallPolicy.QuitCancelHeartbeats)
        {
            Requested = false;
            HeartbeatsSinceRequest = 0;
        }
    }

    public bool IsExiting =>
        DefiniteQuit || (Requested && HeartbeatsSinceRequest < StallPolicy.QuitCancelHeartbeats);
}
