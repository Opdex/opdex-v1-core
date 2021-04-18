using Stratis.SmartContracts;

public struct BurnLog
{
    [Index] public Address Sender;
    [Index] public Address To;
    public ulong AmountCrs;
    public UInt256 AmountSrc;
    public UInt256 AmountLpt;
}