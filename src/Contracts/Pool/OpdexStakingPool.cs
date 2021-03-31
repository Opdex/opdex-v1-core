using Stratis.SmartContracts;

/// <summary>
/// Standard liquidity pool with added staking capabilities. Inflates liquidity pool token supply
/// by .05% according to the difference between root K and root KLast. Stakers deposit the staking
/// token and earn the inflated fees according to their weight staked.
/// </summary>
public class OpdexStakingPool : OpdexStandardPool, IOpdexStakingPool
{
    /// <summary>
    /// Constructor initializing the staking pool.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The SRC token address in the liquidity pool.</param>
    /// <param name="stakeToken">The SRC staking token address.</param>
    public OpdexStakingPool(ISmartContractState state, Address token, Address stakeToken) 
        : base(state, token)
    {
        StakeToken = stakeToken;
    }
    
    /// <inheritdoc cref="IOpdexStakingPool.Receive" />
    public override void Receive() { }

    /// <inheritdoc />
    public Address StakeToken
    {
        get => State.GetAddress(nameof(StakeToken));
        private set => State.SetAddress(nameof(StakeToken), value);
    }
        
    /// <inheritdoc />
    public UInt256 TotalStaked
    {
        get => State.GetUInt256(nameof(TotalStaked));
        private set => State.SetUInt256(nameof(TotalStaked), value);
    }
        
    /// <inheritdoc />
    public UInt256 TotalStakedApplicable
    {
        get => State.GetUInt256(nameof(TotalStakedApplicable));
        private set => State.SetUInt256(nameof(TotalStakedApplicable), value);
    }
        
    /// <inheritdoc />
    public UInt256 StakingRewardsBalance
    {
        get => State.GetUInt256(nameof(StakingRewardsBalance));
        private set => State.SetUInt256(nameof(StakingRewardsBalance), value);
    }

    /// <inheritdoc />
    public UInt256 GetStakedBalance(Address address)
    {
        return State.GetUInt256($"StakedBalance:{address}");
    }

    private void SetStakedBalance(Address address, UInt256 weight)
    {
        State.SetUInt256($"StakedBalance:{address}", weight);
    }

    /// <inheritdoc />
    public UInt256 GetStakedWeight(Address address)
    {
        return State.GetUInt256($"StakedWeight:{address}");
    }

    private void SetStakedWeight(Address address, UInt256 weightK)
    {
        State.SetUInt256($"StakedWeight:{address}", weightK);
    }

    /// <inheritdoc />
    public UInt256 GetStakingRewards(Address staker)
    {
        return GetStakingRewardsExecute(staker, GetStakedBalance(staker));
    }
    
    /// <inheritdoc />
    public void Stake(UInt256 amount)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        MintStakingRewards(ReserveCrs, ReserveSrc);

        var stakedBalance = GetStakedBalance(Message.Sender);
        if (stakedBalance > 0)
        {
            CollectStakingRewardsExecute(Message.Sender, stakedBalance, false);
            UnstakeExecute(Message.Sender, stakedBalance, false);
        }
        
        stakedBalance += amount;

        SafeTransferFrom(StakeToken, Message.Sender, Address, amount);
        SetStakedBalance(Message.Sender, stakedBalance);
        SetStakingWeightExecute(stakedBalance);
        NominateLiquidityPool();
        Unlock();
    }
    
    /// <inheritdoc />
    public void Collect(Address to, bool liquidate)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var stakedBalance = GetStakedBalance(Message.Sender);
        
        CollectStakingRewardsExecute(to, stakedBalance, liquidate);
        SetStakingWeightExecute(stakedBalance);
        Unlock();
    }
        
    /// <inheritdoc />
    public void Unstake(Address to, bool liquidate)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var stakedBalance = GetStakedBalance(Message.Sender);
        
        CollectStakingRewardsExecute(to, stakedBalance, liquidate);
        UnstakeExecute(to, stakedBalance, true);
        NominateLiquidityPool();
        Unlock();
    }
        
    /// <inheritdoc />
    public override UInt256 Mint(Address to)
    {
        EnsureUnlocked();
        
        MintStakingRewards(ReserveCrs, ReserveSrc);

        var liquidity = MintExecute(to);
        
        Unlock();

        return liquidity;
    }
    
    /// <inheritdoc />
    public override UInt256[] Burn(Address to)
    {
        EnsureUnlocked();
        
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var amounts = BurnExecute(to, GetBalance(Address) - StakingRewardsBalance);
        
        Unlock();

        return amounts;
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
        
        StakingRewardsBalance = GetBalance(Address);
    
        Unlock();
    }

    private void NominateLiquidityPool()
    {
        Call(StakeToken, 0ul, nameof(NominateLiquidityPool));
    }

    private void SetStakingWeightExecute(UInt256 balance)
    {
        UInt256 weight = 0;
        
        if (balance > 0)
        {
            var totalStaked = TotalStaked;
            var stakingRewardsBalance = StakingRewardsBalance;

            weight = CalculateStakingWeight(balance, stakingRewardsBalance, totalStaked);

            TotalStaked += balance;
            
            Log(new OpdexStakeEvent
            {
                Sender = Message.Sender,
                Amount = balance,
                Weight = weight
            });
        }
        
        SetStakedWeight(Message.Sender, weight);
    }

    private UInt256 GetStakingRewardsExecute(Address staker, UInt256 balance)
    {
        var stakedWeight = GetStakedWeight(staker);
        var stakingRewardsBalance = StakingRewardsBalance;
        var totalStakedApplicable = TotalStakedApplicable;
        var currentWeight = CalculateStakingWeight(balance, stakingRewardsBalance, totalStakedApplicable);

        return currentWeight <= stakedWeight ? 0 : currentWeight - stakedWeight;
    }

    private UInt256 CalculateStakingWeight(UInt256 stakedBalance, UInt256 rewardsBalance, UInt256 totalStaked)
    {
        return rewardsBalance > 0 && totalStaked > 0
            ? stakedBalance * rewardsBalance / totalStaked
            : 0;
    }

    private void CollectStakingRewardsExecute(Address to, UInt256 stakedBalance, bool liquidate)
    {
        var rewards = GetStakingRewardsExecute(Message.Sender, stakedBalance);
        
        StakingRewardsBalance -= rewards;
        TotalStaked -= stakedBalance;
        TotalStakedApplicable -= stakedBalance;
        
        if (liquidate) BurnExecute(to, rewards);
        else TransferTokensExecute(Address, to, rewards);
        
        Log(new OpdexCollectEvent
        {
            Sender = Message.Sender,
            Amount = stakedBalance,
            Reward = rewards
        });
    }

    private void UnstakeExecute(Address to, UInt256 stakedBalance, bool transfer)
    {
        if (transfer)
        {
            SafeTransferTo(StakeToken, to, stakedBalance);
            
            Log(new OpdexUnstakeEvent
            {
                Staker = Message.Sender,
                Amount = stakedBalance
            });
        }
        
        SetStakedBalance(Message.Sender, 0);
        SetStakedWeight(Message.Sender, 0);
    }

    private void EnsureStakingEnabled()
    {
        var stakeToken = StakeToken;

        var enabled = stakeToken != Token && stakeToken != Address.Zero;
        
        Assert(enabled, "OPDEX: STAKING_UNAVAILABLE");
    }

    private void MintStakingRewards(ulong reserveCrs, UInt256 reserveSrc)
    {
        if (TotalStaked == 0) return;
        
        var kLast = KLast;
        
        if (kLast == 0) return;
        
        var rootK = Sqrt(reserveCrs * reserveSrc);
        var rootKLast = Sqrt(kLast);
        
        if (rootK <= rootKLast) return;
        
        var numerator = TotalSupply * (rootK - rootKLast);
        var denominator = (rootK * 5) + rootKLast;
        var liquidity = numerator / denominator;
        
        if (liquidity == 0) return;

        StakingRewardsBalance += liquidity;
        TotalStakedApplicable = TotalStaked;
        
        MintTokensExecute(Address, liquidity);
    }
}