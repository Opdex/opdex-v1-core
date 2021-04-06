using Stratis.SmartContracts;

public struct ExitStakingPoolLog
{
    [Index] public Address Staker;
    public UInt256 Amount;
}