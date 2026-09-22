namespace BlackBox.Core;

public static class HeartbeatPulse
{
    public static long Apply(long now, long last, long frequency, Func<string?> readScene, ref string scene)
    {
        if (frequency <= 0)
            frequency = 1;
        if (last != 0 && now - last < frequency)
            return last;

        try
        {
            var next = readScene();
            if (!string.IsNullOrEmpty(next))
                scene = next;
        }
        catch
        {
            // Timestamp already committed by returning now.
        }

        return now;
    }
}
