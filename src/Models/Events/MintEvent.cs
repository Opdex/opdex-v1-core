using Stratis.SmartContracts;

public struct MintEvent
{
    [Index] public Address Sender;
    public ulong AmountCrs;
    public UInt256 AmountSrc;
    public byte EventTypeId;
}