using Stratis.SmartContracts;

public struct CollectStakingRewardsLog
{
    [Index] public Address Staker;
    public UInt256 Amount;
}