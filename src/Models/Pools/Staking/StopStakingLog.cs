using Stratis.SmartContracts;

public struct StopStakingLog
{
    [Index] public Address Staker;
    public UInt256 Amount;
}