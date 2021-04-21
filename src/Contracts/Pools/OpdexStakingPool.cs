using Stratis.SmartContracts;

/// <summary>
/// Standard liquidity pool with added staking capabilities. Inflates liquidity pool token supply
/// by .05% according to the difference between root K and root KLast. Stakers deposit the staking
/// token and earn the inflated fees according to their weight staked.
/// </summary>
public class OpdexStakingPool : OpdexPool, IOpdexStakingPool
{
    /// <summary>
    /// Constructor initializing a staking pool contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The SRC token address in the liquidity pool.</param>
    /// <param name="stakingToken">The SRC staking token address.</param>
    /// <param name="fee">The market transaction fee, 0-10 equal to 0-1%.</param>
    public OpdexStakingPool(ISmartContractState state, Address token, Address stakingToken, uint fee) : base(state, token, fee) 
    {
        StakingToken = stakingToken;
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
        
        SafeTransferFrom(StakingToken, Message.Sender, Address, amount);
        
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
        
        StakingRewardsBalance = GetBalance(Address);
        
        Unlock();
    }

    private void NominateLiquidityPool()
    {
        Call(StakingToken, 0ul, nameof(NominateLiquidityPool));
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
            
            Log(new StartStakingLog { Staker = Message.Sender, Amount = balance, TotalStaked = TotalStaked});
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

    private static UInt256 CalculateStakingWeight(UInt256 stakedBalance, UInt256 rewardsBalance, UInt256 totalStaked)
    {
        if (rewardsBalance == 0 || totalStaked == 0) return 0;
        return stakedBalance * rewardsBalance / totalStaked;
    }

    private void CollectStakingRewardsExecute(Address to, UInt256 stakedBalance, bool liquidate)
    {
        var rewards = GetStakingRewardsExecute(Message.Sender, stakedBalance);
        
        StakingRewardsBalance -= rewards;
        TotalStaked -= stakedBalance;
        TotalStakedApplicable -= stakedBalance;
        
        if (liquidate) BurnExecute(to, rewards);
        else TransferTokensExecute(Address, to, rewards);
        
        Log(new CollectStakingRewardsLog { Staker = Message.Sender, Reward = rewards });
    }

    private void UnstakeExecute(Address to, UInt256 stakedBalance, bool transfer)
    {
        if (transfer)
        {
            SafeTransferTo(StakingToken, to, stakedBalance);
            Log(new StopStakingLog { Staker = Message.Sender, Amount = stakedBalance, TotalStaked = TotalStaked});
        }
        
        SetStakedBalance(Message.Sender, 0);
        SetStakedWeight(Message.Sender, 0);
    }

    private void EnsureStakingEnabled()
    {
        var stakingToken = StakingToken;

        var enabled = stakingToken != Token && stakingToken != Address.Zero;
        
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