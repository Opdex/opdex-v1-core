using Stratis.SmartContracts;

public struct OpdexMintEvent
{
    [Index] public Address Sender;
    public ulong AmountCrs;
    public UInt256 AmountSrc;
}