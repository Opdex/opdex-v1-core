using Stratis.SmartContracts;

public struct StopStakingLog
{
    [Index] public Address Staker;
    public UInt256 Amount;
    public UInt256 TotalStaked;
    public UInt256 StakerBalance;
}