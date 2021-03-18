using Stratis.SmartContracts;

public struct OpdexRewardEvent
{
    [Index] public Address Sender;
    public UInt256 Amount;
    public UInt256 Reward;
    public UInt256 Weight;
}