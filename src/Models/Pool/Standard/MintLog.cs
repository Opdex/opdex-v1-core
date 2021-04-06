using Stratis.SmartContracts;

public struct MintLog
{
    [Index] public Address Sender;
    public ulong AmountCrs;
    public UInt256 AmountSrc;
}