using Stratis.SmartContracts;

public struct EnterStakingPoolLog
{
    [Index] public Address Staker;
    public UInt256 Amount;
}