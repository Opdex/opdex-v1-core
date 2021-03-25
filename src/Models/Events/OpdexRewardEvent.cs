using Stratis.SmartContracts;

public struct OpdexRewardEvent
{
    [Index] public Address Sender;
    public string Amount;
    public string Reward;
}