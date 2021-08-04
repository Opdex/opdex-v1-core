using Stratis.SmartContracts;

public interface IOpdexStakingPool : IOpdexLiquidityPool
{
    /// <summary>
    /// The address of the liquidity pool's staking token.
    /// </summary>
    Address StakingToken { get; }

    /// <summary>
    /// The mining pool associated with this staking pool contract.
    /// </summary>
    Address MiningPool { get; }

    /// <summary>
    /// The total amount of staked tokens.
    /// </summary>
    UInt256 TotalStaked { get; }

    /// <summary>
    /// The balance of liquidity pool tokens belonging to stakers.
    /// </summary>
    UInt256 StakingRewardsBalance { get; }

    /// <summary>
    /// The latest fees that haven't been accounted for when calculating reward per staked token.
    /// </summary>
    UInt256 ApplicableStakingRewards { get; }

    /// <summary>
    /// The amount of liquidity pool token rewards per full token staked from the last time any staker executed a staking action.
    /// </summary>
    UInt256 RewardPerStakedTokenLast { get; }

    /// <summary>
    /// Retrieves the last calculated reward per staked token stored during the last action executed by the staker.
    /// </summary>
    /// <param name="staker">The address of the staker.</param>
    /// <returns>The amount of rewards per staked token from the last action taken by the staker.</returns>
    UInt256 GetStoredRewardPerStakedToken(Address staker);

    /// <summary>
    /// Retrieves the last calculated reward amount stored during the most recent action taken by the staker.
    /// </summary>
    /// <param name="staker">The address of the staker.</param>
    /// <returns>The last calculated reward amount for the staker.</returns>
    UInt256 GetStoredReward(Address staker);

    /// <summary>
    /// Retrieves the current reward per full token staked based on the most recent calculation of total staking pool rewards.
    /// </summary>
    /// <returns>The amount of rewards.</returns>
    UInt256 GetRewardPerStakedToken();

    /// <summary>
    /// Retrieves the amount of tokens staked for an address.
    /// </summary>
    /// <param name="staker">The address to check the staked balance of.</param>
    /// <returns>The amount of staked tokens.</returns>
    UInt256 GetStakedBalance(Address staker);

    /// <summary>
    /// Retrieves the the amount of earned rewards based on the most recent calculation of total staking pool rewards.
    /// </summary>
    /// <param name="staker">The address to check the reward balance of.</param>
    /// <returns>The current amount of rewards to be collected.</returns>
    UInt256 GetStakingRewards(Address staker);

    /// <summary>
    /// Using an allowance, transfers staking tokens to the pool, beginning staking and nominating the
    /// liquidity pool for mining.
    /// </summary>
    /// <param name="amount">The amount of tokens to stake.</param>
    void StartStaking(UInt256 amount);

    /// <summary>
    /// Collect any earned staking rewards while continuing to stake, optionally liquidate the received LP
    /// tokens into the pools reserve tokens.
    /// </summary>
    /// <param name="liquidate">Boolean value to liquidate rewards.</param>
    void CollectStakingRewards(bool liquidate);

    /// <summary>
    /// Stop staking and withdraw rewards. Optionally liquidate the earned LP tokens into the pools
    /// reserve tokens.
    /// </summary>
    /// <param name="amount">The amount of tokens to stop staking.</param>
    /// <param name="liquidate">Boolean value to liquidate rewards.</param>
    void StopStaking(UInt256 amount, bool liquidate);
}