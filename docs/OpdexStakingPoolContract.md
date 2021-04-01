# Opdex Staking Pool Contract

Staking liquidity pool contract that inherits for OpdexStandardPool and IOpdexStakingPool. Used for staking pools that include all functionality from standard pools but adds staking abilities.

Transaction fees remain a total 0.3%, same as standard pools. Staking pools distribute the fee between providers and stakers. 
5/6 of transaction fees (0.25% tx fee) goes to liquidity providers. 1/6 of transaction fees (0.05%) goes to stakers.

## Overview

## Stakers Positions

### Get Staked Balance

```C#
/// <summary>
/// Retrieves the amount of tokens staked for an address.
/// </summary>
/// <param name="address">The address to check the staked balance of.</param>
/// <returns>Amount of staked tokens</returns>
UInt256 GetStakedBalance(Address address);
```

### Get Staked Weight

```C#
/// <summary>
/// Retrieves the recorded weight of stakers entry position.
/// </summary>
/// <param name="address">The address to check the weight of.</param>
/// <returns>Stakers entry weight</returns>
UInt256 GetStakedWeight(Address address);
```

### Get Staking Rewards

```C#
/// <summary>
/// Retrieves the amount of earned rewards of a staker.
/// </summary>
/// <param name="staker">The address to check the reward balance of.</param>
/// <returns>Amount of earned rewards</returns>
UInt256 GetStakingRewards(Address staker);
```

## Liquidity Pool Staking

### Stake

```C#
/// <summary>
/// Using an allowance, transfers stake tokens to the pool and records staking weight.
/// </summary>
/// <param name="amount">The amount of tokens to stake.</param>
void Stake(UInt256 amount);
```

### Collect

```C#
/// <summary>
/// Collect any earned staking rewards while continuing to stake, optionally liquidate the earned LP
/// tokens into the pools reserve tokens.
/// </summary>
/// <param name="to">The address to send rewards to.</param>
/// <param name="liquidate">Boolean value to liquidate rewards.</param>
void Collect(Address to, bool liquidate);
```

### Unstake

```C#
/// <summary>
/// Discontinue staking and withdraw rewards. Optionally liquidate the earned LP tokens into the pools
/// reserve tokens.
/// </summary>
/// <param name="to">The address to send rewards to.</param>
/// <param name="liquidate">Boolean value to liquidate rewards.</param>
void Unstake(Address to, bool liquidate);
```
