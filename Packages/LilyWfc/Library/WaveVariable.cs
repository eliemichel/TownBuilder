namespace LilyWfc
{

/**
 * Index to query the state vector (technically just an int)
 * A variable corresponds to a vertex in the system topology
 */
public struct WaveVariable
{
    int raw;

    public override string ToString()
    {
        return "WaveIndex(" + (IsNull() ? "NULL" : Raw().ToString()) + ")";
    }

    public int Raw()
    {
        return raw;
    }

    public bool IsNull()
    {
        return raw == -1;
    }

    public static WaveVariable FromRaw(int r)
    {
        return new WaveVariable { raw = r };
    }

    public static readonly WaveVariable Null = new WaveVariable { raw = -1 };
}

}