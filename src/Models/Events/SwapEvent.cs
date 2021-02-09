using Stratis.SmartContracts;

public struct SwapEvent
{
    [Index] public Address Sender;
    [Index] public Address To;
    public ulong AmountCrsIn;
    public UInt256 AmountTokenIn;
    public ulong AmountCrsOut;
    public UInt256 AmountTokenOut;
    public byte EventTypeId;
}