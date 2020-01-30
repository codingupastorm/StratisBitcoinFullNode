namespace Stratis.Bitcoin.Features.MemoryPool.Broadcasting
{
    public enum State
    {
        CantBroadcast,
        ToBroadcast,
        Broadcasted,
        Propagated
    }
}
