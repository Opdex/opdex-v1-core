using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

public interface IOpdexStakingPool : IOpdexStandardPool, IStandardToken256
{
    Address StakeToken { get; }
    UInt256 TotalStaked { get; }
    UInt256 TotalStakedApplicable { get; }
    UInt256 StakingRewardsBalance { get; }

    UInt256 GetStakedBalance(Address address);
    UInt256 GetStakedWeight(Address address);
    UInt256 GetStakingRewards(Address staker);
    void Stake(UInt256 amount);
    void WithdrawStakingRewards(Address to, bool burn);
    void ExitStaking(Address to, bool burn);
}