using Stratis.SmartContracts;

public struct OpdexCollectEvent
{
    [Index] public Address Sender;
    public UInt256 Amount;
    public UInt256 Reward;
}