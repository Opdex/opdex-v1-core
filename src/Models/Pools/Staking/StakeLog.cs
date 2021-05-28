using Stratis.SmartContracts;

public struct StakeLog
{
    [Index] public Address Staker;
    [Index] public StakeEventType EventType;
    public UInt256 Amount;
    public UInt256 TotalStaked;
}