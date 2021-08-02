using Stratis.SmartContracts;

public struct MintLog
{
    [Index] public Address Sender;
    [Index] public Address To;
    public ulong AmountCrs;
    public UInt256 AmountSrc;
    public UInt256 AmountLpt;
    public UInt256 TotalSupply;
}