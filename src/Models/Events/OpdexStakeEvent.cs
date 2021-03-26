using Stratis.SmartContracts;

public struct OpdexStakeEvent
{
    [Index] public Address Sender;
    public UInt256 Amount;
    public UInt256 Weight;
}