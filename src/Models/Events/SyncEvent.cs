using Stratis.SmartContracts;

public struct SyncEvent
{
    public ulong ReserveCrs;
    public UInt256 ReserveToken;
    public byte EventTypeId;
}