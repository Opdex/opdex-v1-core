using Stratis.SmartContracts;

/// <summary>
/// Staking liquidity pool including CRS and an SRC20 token along with a Liquidity Pool token (SRC20) in this contract.
/// Additional staking methods support a staking token used to nominate this pool for liquidity mining in return for partial transaction fees.
/// Mint, Swap and Burn methods should be called through an integrated Router contract.
/// </summary>
public class OpdexStakingPool : OpdexLiquidityPool, IOpdexStakingPool
{
    private const ulong SatsPerToken = 100_000_000;

    /// <summary>
    /// Constructor initializing a staking pool contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The SRC token address in the liquidity pool.</param>
    /// <param name="fee">The market transaction fee, 0-10 equal to 0-1%.</param>
    /// <param name="stakingToken">The SRC staking token address.</param>
    public OpdexStakingPool(ISmartContractState state, Address token, uint fee, Address stakingToken) : base(state, token, fee)
    {
        StakingToken = stakingToken;
        MiningPool = InitializeMiningPool(token, stakingToken);
    }

    /// <inheritdoc />
    public override void Receive() { }

    /// <inheritdoc />
    public Address StakingToken
    {
        get => State.GetAddress(PoolStateKeys.StakingToken);
        private set => State.SetAddress(PoolStateKeys.StakingToken, value);
    }

    /// <inheritdoc />
    public Address MiningPool
    {
        get => State.GetAddress(PoolStateKeys.MiningPool);
        private set => State.SetAddress(PoolStateKeys.MiningPool, value);
    }

    /// <inheritdoc />
    public UInt256 TotalStaked
    {
        get => State.GetUInt256(PoolStateKeys.TotalStaked);
        private set => State.SetUInt256(PoolStateKeys.TotalStaked, value);
    }

    /// <inheritdoc />
    public UInt256 StakingRewardsBalance
    {
        get => State.GetUInt256(PoolStateKeys.StakingRewardsBalance);
        private set => State.SetUInt256(PoolStateKeys.StakingRewardsBalance, value);
    }

    /// <inheritdoc />
    public UInt256 RewardPerStakedTokenLast
    {
        get => State.GetUInt256(PoolStateKeys.RewardPerStakedTokenLast);
        private set => State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, value);
    }

    /// <inheritdoc />
    public UInt256 ApplicableStakingRewards
    {
        get => State.GetUInt256(PoolStateKeys.ApplicableStakingRewards);
        private set => State.SetUInt256(PoolStateKeys.ApplicableStakingRewards, value);
    }

    /// <inheritdoc />
    public UInt256 GetStoredRewardPerStakedToken(Address staker)
    {
        return State.GetUInt256($"{PoolStateKeys.RewardPerStakedToken}:{staker}");
    }

    private void SetStoredRewardPerStakedToken(Address staker, UInt256 reward)
    {
        State.SetUInt256($"{PoolStateKeys.RewardPerStakedToken}:{staker}", reward);
    }

    /// <inheritdoc />
    public UInt256 GetStoredReward(Address staker)
    {
        return State.GetUInt256($"{PoolStateKeys.Reward}:{staker}");
    }

    private void SetStoredReward(Address staker, UInt256 reward)
    {
        State.SetUInt256($"{PoolStateKeys.Reward}:{staker}", reward);
    }

    /// <inheritdoc />
    public UInt256 GetStakedBalance(Address staker)
    {
        return State.GetUInt256($"{PoolStateKeys.StakedBalance}:{staker}");
    }

    private void SetStakedBalance(Address staker, UInt256 balance)
    {
        State.SetUInt256($"{PoolStateKeys.StakedBalance}:{staker}", balance);
    }

    /// <inheritdoc />
    public void StartStaking(UInt256 amount)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        Assert(amount > 0, "OPDEX: CANNOT_STAKE_ZERO");

        var totalStaked = TotalStaked;

        if (totalStaked > 0) MintStakingRewards();

        UpdateStakingPosition(Message.Sender);

        totalStaked += amount;

        TotalStaked = totalStaked;

        SetStakedBalance(Message.Sender, GetStakedBalance(Message.Sender) + amount);

        SafeTransferFrom(StakingToken, Message.Sender, Address, amount);

        Log(new StakeLog { Staker = Message.Sender, Amount = amount, TotalStaked = totalStaked, EventType = (byte)StakeEventType.StartStaking});

        NominateLiquidityPool();

        if (totalStaked > 0) UpdateKLast();

        Unlock();
    }

    /// <inheritdoc />
    public void CollectStakingRewards(bool liquidate)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();

        var totalStaked = TotalStaked;

        if (totalStaked > 0) MintStakingRewards();

        UpdateStakingPosition(Message.Sender);

        CollectStakingRewardsExecute(Message.Sender, liquidate);

        if (totalStaked > 0) UpdateKLast();

        Unlock();
    }

    /// <inheritdoc />
    public void StopStaking(UInt256 amount, bool liquidate)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();

        var totalStaked = TotalStaked;

        if (totalStaked > 0) MintStakingRewards();

        UpdateStakingPosition(Message.Sender);

        var stakedBalance = GetStakedBalance(Message.Sender);

        Assert(amount <= stakedBalance && amount > 0, "OPDEX: INVALID_AMOUNT");

        totalStaked -= amount;

        TotalStaked = totalStaked;

        SetStakedBalance(Message.Sender, stakedBalance - amount);

        CollectStakingRewardsExecute(Message.Sender, liquidate);

        SafeTransferTo(StakingToken, Message.Sender, amount);

        Log(new StakeLog {Amount = amount, Staker = Message.Sender, TotalStaked = totalStaked, EventType = (byte)StakeEventType.StopStaking});

        NominateLiquidityPool();

        if (totalStaked > 0) UpdateKLast();

        Unlock();
    }

    /// <inheritdoc />
    public override UInt256 Mint(Address to)
    {
        EnsureUnlocked();

        var totalStaked = TotalStaked;

        if (totalStaked > 0) MintStakingRewards();

        var liquidity = MintExecute(to);

        if (totalStaked > 0) UpdateKLast();

        Unlock();

        return liquidity;
    }

    /// <inheritdoc />
    public override UInt256[] Burn(Address to)
    {
        EnsureUnlocked();

        var totalStaked = TotalStaked;

        if (totalStaked > 0) MintStakingRewards();

        var amounts = BurnExecute(to, GetBalance(Address) - StakingRewardsBalance);

        if (totalStaked > 0) UpdateKLast();

        Unlock();

        return amounts;
    }

    /// <inheritdoc />
    public override void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address to, byte[] data)
    {
        EnsureUnlocked();

        SwapExecute(amountCrsOut, amountSrcOut, to, data);

        Unlock();
    }

    /// <inheritdoc />
    public override void Skim(Address to)
    {
        EnsureUnlocked();

        SkimExecute(to);

        TransferTokensExecute(Address, to, GetBalance(Address) - StakingRewardsBalance);

        Unlock();
    }

    /// <inheritdoc />
    public override void Sync()
    {
        EnsureUnlocked();

        UpdateReserves(Balance, GetSrcBalance(Token, Address));

        if (TotalStaked > 0)
        {
            var balance = GetBalance(Address);

            ApplicableStakingRewards += (balance - StakingRewardsBalance);
            StakingRewardsBalance = balance;
        }

        Unlock();
    }

    /// <inheritdoc />
    public UInt256 GetStakingRewards(Address staker)
    {
        return GetStakingRewards(staker, GetRewardPerStakedToken());
    }

    /// <inheritdoc />
    public UInt256 GetRewardPerStakedToken()
    {
        var totalStaked = TotalStaked;

        if (totalStaked == 0) return RewardPerStakedTokenLast;

        return RewardPerStakedTokenLast + (ApplicableStakingRewards * SatsPerToken / totalStaked);
    }

    private UInt256 GetStakingRewards(Address staker, UInt256 rewardPerToken)
    {
        var stakedBalance = GetStakedBalance(staker);
        var rewardPerTokenPaid = GetStoredRewardPerStakedToken(staker);
        var rewardsDifference = rewardPerToken - rewardPerTokenPaid;
        var reward = GetStoredReward(staker);

        return reward + (stakedBalance * rewardsDifference / SatsPerToken);
    }

    private void UpdateStakingPosition(Address address)
    {
        var rewardPerToken = GetRewardPerStakedToken();

        ApplicableStakingRewards = 0;
        RewardPerStakedTokenLast = rewardPerToken;

        SetStoredReward(address, GetStakingRewards(address, rewardPerToken));
        SetStoredRewardPerStakedToken(address, rewardPerToken);
    }

    private void CollectStakingRewardsExecute(Address to, bool liquidate)
    {
        var rewards = GetStoredReward(Message.Sender);

        var totalStaked = TotalStaked;

        if (totalStaked == 0)
        {
            // Dust from rounding with no stakers goes to the next burn,mint,skim or sync
            StakingRewardsBalance = 0;
            ResetKLast();
        }
        else
        {
            StakingRewardsBalance -= rewards;
        }

        if (rewards == 0) return;

        SetStoredReward(Message.Sender, 0);

        if (liquidate) BurnExecute(to, rewards);
        else Assert(TransferTokensExecute(Address, to, rewards), "OPDEX: INVALID_TRANSFER");

        Log(new CollectStakingRewardsLog { Staker = Message.Sender, Amount = rewards });
    }

    private void EnsureStakingEnabled()
    {
        var stakingToken = StakingToken;

        var enabled = stakingToken != Token && stakingToken != Address.Zero;

        Assert(enabled, "OPDEX: STAKING_UNAVAILABLE");
    }

    private void MintStakingRewards()
    {
        var liquidity = CalculateFee();

        if (liquidity == 0) return;

        StakingRewardsBalance += liquidity;
        ApplicableStakingRewards += liquidity;

        MintTokensExecute(Address, liquidity);
    }

    private void NominateLiquidityPool()
    {
        // References external IOpdexMiningGovernance.NominateLiquidityPool method in the Opdex Governance codebase.
        // Limitations prevent re-use of the interface here, using a constant instead.
        const string governanceNominationMethod = "NominateLiquidityPool";

        // Failures shouldn't prevent the staking action
        Call(StakingToken, 0, governanceNominationMethod);
    }

    private Address InitializeMiningPool(Address token, Address stakingToken)
    {
        if (stakingToken == token) return Address.Zero;

        var response = Create<OpdexMiningPool>(0, new object[] {stakingToken, Address});

        Assert(response.Success, "OPDEX: INVALID_MINING_POOL");

        return response.NewContractAddress;
    }
}