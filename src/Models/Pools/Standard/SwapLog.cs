using Stratis.SmartContracts;

public struct SwapLog
{
    [Index] public Address Sender;
    [Index] public Address To;
    public ulong AmountCrsIn;
    public UInt256 AmountSrcIn;
    public ulong AmountCrsOut;
    public UInt256 AmountSrcOut;
}