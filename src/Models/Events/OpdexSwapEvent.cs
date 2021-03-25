using Stratis.SmartContracts;

public struct OpdexSwapEvent
{
    [Index] public Address Sender;
    [Index] public Address To;
    public ulong AmountCrsIn;
    public string AmountSrcIn;
    public ulong AmountCrsOut;
    public string AmountSrcOut;
}