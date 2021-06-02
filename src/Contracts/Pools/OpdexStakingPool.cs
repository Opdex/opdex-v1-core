using Stratis.SmartContracts;

/// <summary>
/// Standard liquidity pool with added staking capabilities. Inflates liquidity pool token supply
/// by .05% according to the difference between root K and root KLast. Stakers deposit the staking
/// token and receive the inflated fees according to their weight staked for governance participation.
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
        get => State.GetAddress(nameof(StakingToken));
        private set => State.SetAddress(nameof(StakingToken), value);
    }
    
    /// <inheritdoc />
    public Address MiningPool
    {
        get => State.GetAddress(nameof(MiningPool));
        private set => State.SetAddress(nameof(MiningPool), value);
    }
        
    /// <inheritdoc />
    public UInt256 TotalStaked
    {
        get => State.GetUInt256(nameof(TotalStaked));
        private set => State.SetUInt256(nameof(TotalStaked), value);
    }
        
    /// <inheritdoc />
    public UInt256 StakingRewardsBalance
    {
        get => State.GetUInt256(nameof(StakingRewardsBalance));
        private set => State.SetUInt256(nameof(StakingRewardsBalance), value);
    }

    /// <inheritdoc />
    public UInt256 RewardPerStakedTokenLast
    {
        get => State.GetUInt256(nameof(RewardPerStakedTokenLast));
        private set => State.SetUInt256(nameof(RewardPerStakedTokenLast), value);
    }
    
    /// <inheritdoc />
    public UInt256 ApplicableStakingRewards
    {
        get => State.GetUInt256(nameof(ApplicableStakingRewards));
        private set => State.SetUInt256(nameof(ApplicableStakingRewards), value);
    }
    
    /// <inheritdoc />
    public UInt256 GetStoredRewardPerStakedToken(Address staker)
    {
        return State.GetUInt256($"RewardPerStakedToken:{staker}");
    }

    private void SeStoredRewardPerStakedToken(Address staker, UInt256 reward)
    {
        State.SetUInt256($"RewardPerStakedToken:{staker}", reward);
    }
    
    /// <inheritdoc />
    public UInt256 GetStoredReward(Address staker)
    {
        return State.GetUInt256($"Reward:{staker}");
    }

    private void SetStoredReward(Address staker, UInt256 reward)
    {
        State.SetUInt256($"Reward:{staker}", reward);
    }
    
    /// <inheritdoc />
    public UInt256 GetStakedBalance(Address staker)
    {
        return State.GetUInt256($"StakedBalance:{staker}");
    }

    private void SetStakedBalance(Address staker, UInt256 balance)
    {
        State.SetUInt256($"StakedBalance:{staker}", balance);
    }

    /// <inheritdoc />
    public void StartStaking(UInt256 amount)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        Assert(amount > 0, "OPDEX: CANNOT_STAKE_ZERO");

        MintStakingRewards();
        
        UpdateStakingPosition(Message.Sender);

        var totalStaked = TotalStaked;
        
        totalStaked += amount;

        TotalStaked = totalStaked;
        
        SetStakedBalance(Message.Sender, GetStakedBalance(Message.Sender) + amount);
        
        SafeTransferFrom(StakingToken, Message.Sender, Address, amount);
        
        Log(new StakeLog { Staker = Message.Sender, Amount = amount, TotalStaked = totalStaked, EventType = (byte)StakeEventType.StartStaking});
        
        NominateLiquidityPool();
        
        UpdateKLast();
        
        Unlock();
    }
    
    /// <inheritdoc />
    public void CollectStakingRewards(bool liquidate)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        
        MintStakingRewards();
        
        UpdateStakingPosition(Message.Sender);
        
        CollectStakingRewardsExecute(Message.Sender, liquidate);
        
        UpdateKLast();
        
        Unlock();
    }
        
    /// <inheritdoc />
    public void StopStaking(UInt256 amount, bool liquidate)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        
        MintStakingRewards();
        
        UpdateStakingPosition(Message.Sender);
        
        var stakedBalance = GetStakedBalance(Message.Sender);
        
        Assert(amount <= stakedBalance && amount > 0, "OPDEX: INVALID_AMOUNT");

        var totalStaked = TotalStaked;
        
        totalStaked -= amount;

        TotalStaked = totalStaked;
        
        SetStakedBalance(Message.Sender, stakedBalance - amount);
        
        CollectStakingRewardsExecute(Message.Sender, liquidate);
        
        SafeTransferTo(StakingToken, Message.Sender, amount);
        
        Log(new StakeLog {Amount = amount, Staker = Message.Sender, TotalStaked = totalStaked, EventType = (byte)StakeEventType.StopStaking});
        
        NominateLiquidityPool();
        
        UpdateKLast();
        
        Unlock();
    }
        
    /// <inheritdoc />
    public override UInt256 Mint(Address to)
    {
        EnsureUnlocked();

        MintStakingRewards();
        
        var liquidity = MintExecute(to);
        
        UpdateKLast();

        Unlock();
        
        return liquidity;
    }
    
    /// <inheritdoc />
    public override UInt256[] Burn(Address to)
    {
        EnsureUnlocked();
        
        MintStakingRewards();
        
        var amounts = BurnExecute(to, GetBalance(Address) - StakingRewardsBalance);
        
        UpdateKLast();
        
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

            ApplicableStakingRewards += balance - StakingRewardsBalance;
            StakingRewardsBalance = balance;
        }

        Unlock();
    }
    
    /// <inheritdoc />
    public UInt256 GetStakingRewards(Address staker)
    {
        var stakedBalance = GetStakedBalance(staker);
        var rewardPerTokenStaked = GetRewardPerStakedToken();
        var rewardPerTokenPaid = GetStoredRewardPerStakedToken(staker);
        var rewardsDifference = rewardPerTokenStaked - rewardPerTokenPaid;
        var reward = GetStoredReward(staker);

        return reward + (stakedBalance * rewardsDifference / SatsPerToken);
    }
    
    /// <inheritdoc />
    public UInt256 GetRewardPerStakedToken()
    {
        var totalStaked = TotalStaked;
        
        if (totalStaked == 0) return RewardPerStakedTokenLast;
        
        return RewardPerStakedTokenLast + (ApplicableStakingRewards * SatsPerToken / totalStaked);
    }

    private void UpdateStakingPosition(Address address)
    {
        var rewardPerToken = GetRewardPerStakedToken();

        ApplicableStakingRewards = 0;
        RewardPerStakedTokenLast = rewardPerToken;
        
        SetStoredReward(address, GetStakingRewards(address));
        SeStoredRewardPerStakedToken(address, rewardPerToken);
    }
    
    private void CollectStakingRewardsExecute(Address to, bool liquidate)
    {
        var rewards = GetStoredReward(Message.Sender);
        
        // Dust from rounding with no stakers goes to the next burn,mint,skim or sync
        StakingRewardsBalance = TotalStaked > 0 ? StakingRewardsBalance - rewards : 0;
        
        if (rewards == 0) return;
        
        SetStoredReward(Message.Sender, 0);
        
        if (liquidate) BurnExecute(to, rewards);
        else Assert(TransferTokensExecute(Address, to, rewards), "OPDEX: INVALID_TRANSFER");

        Log(new CollectStakingRewardsLog { Staker = Message.Sender, Reward = rewards });
    }

    private void EnsureStakingEnabled()
    {
        var stakingToken = StakingToken;

        var enabled = stakingToken != Token && stakingToken != Address.Zero;
        
        Assert(enabled, "OPDEX: STAKING_UNAVAILABLE");
    }

    private void MintStakingRewards()
    {
        var kLast = KLast;
        
        if (kLast == 0) return;

        if (TotalStaked == 0)
        {
            // stakers to 0 stakers, klast 100 should be reset to 0
            // prevent new stakers minting fees they don't yet deserve
            //
            // Realistically, UpdateKLast and this method should never run when TotalStaked = 0
            // Todo: Refactor
            ResetKLast();
            return;
        }
        
        var rootK = Sqrt(ReserveCrs * ReserveSrc);
        var rootKLast = Sqrt(kLast);
        
        if (rootK <= rootKLast) return;
        
        var numerator = TotalSupply * (rootK - rootKLast);
        var denominator = (rootK * 5) + rootKLast;
        var liquidity = numerator / denominator;
        
        if (liquidity == 0) return;
        
        StakingRewardsBalance += liquidity;
        ApplicableStakingRewards += liquidity;
        
        MintTokensExecute(Address, liquidity);
    }
    
    private void NominateLiquidityPool()
    {
        // Failures shouldn't prevent the staking action
        Call(StakingToken, 0, nameof(NominateLiquidityPool));
    }

    private Address InitializeMiningPool(Address token, Address stakingToken)
    {
        if (stakingToken == token) return Address.Zero;
        
        var response = Create<OpdexMiningPool>(0, new object[] {stakingToken, Address});

        Assert(response.Success && response.NewContractAddress != Address.Zero, "OPDEX: INVALID_MINING_POOL");

        return response.NewContractAddress;
    }
}