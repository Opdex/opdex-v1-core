using Stratis.SmartContracts;

public struct OpdexStakeEvent
{
    [Index] public Address Sender;
    public string Amount;
    public string Weight;
}