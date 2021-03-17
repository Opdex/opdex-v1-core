using Stratis.SmartContracts;

public struct OpdexBurnEvent
{
    [Index] public Address Sender;
    [Index] public Address To;
    public ulong AmountCrs;
    public UInt256 AmountSrc;
}