using Stratis.SmartContracts;

public interface IOpdexStakingPool : IOpdexPool
{
    /// <summary>
    /// The address of the pools staking token.
    /// </summary>
    Address StakingToken { get; }
    
    /// <summary>
    /// The total amount of staked tokens.
    /// </summary>
    UInt256 TotalStaked { get; }
    
    /// <summary>
    /// The total amount of staked tokens that have earned rewards.
    /// </summary>
    UInt256 TotalStakedApplicable { get; }
    
    /// <summary>
    /// The balance of liquidity pool tokens earned by stakers to be collected.
    /// </summary>
    UInt256 StakingRewardsBalance { get; }

    /// <summary>
    /// Retrieves the amount of tokens staked for an address.
    /// </summary>
    /// <param name="address">The address to check the staked balance of.</param>
    /// <returns>The amount of staked tokens.</returns>
    UInt256 GetStakedBalance(Address address);
    
    /// <summary>
    /// Retrieves the recorded weight of stakers entry position.
    /// </summary>
    /// <param name="address">The address to check the weight of.</param>
    /// <returns>The stakers entry weight.</returns>
    UInt256 GetStakedWeight(Address address);
    
    /// <summary>
    /// Retrieves the amount of earned rewards of a staker.
    /// </summary>
    /// <param name="staker">The address to check the reward balance of.</param>
    /// <returns>The amount of earned rewards.</returns>
    UInt256 GetStakingRewards(Address staker);
    
    /// <summary>
    /// Using an allowance, transfers stake tokens to the pool and records staking weight.
    /// </summary>
    /// <param name="amount">The amount of tokens to stake.</param>
    void Stake(UInt256 amount);
    
    /// <summary>
    /// Collect any earned staking rewards while continuing to stake, optionally liquidate the earned LP
    /// tokens into the pools reserve tokens.
    /// </summary>
    /// <param name="to">The address to send rewards to.</param>
    /// <param name="liquidate">Boolean value to liquidate rewards.</param>
    void Collect(Address to, bool liquidate);
    
    /// <summary>
    /// Stop staking and withdraw rewards. Optionally liquidate the earned LP tokens into the pools
    /// reserve tokens.
    /// </summary>
    /// <param name="to">The address to send rewards to.</param>
    /// <param name="liquidate">Boolean value to liquidate rewards.</param>
    void Unstake(Address to, bool liquidate);
}