using Stratis.SmartContracts;

public struct StartStakingLog
{
    [Index] public Address Staker;
    public UInt256 Amount;
}