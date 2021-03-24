using Stratis.SmartContracts;

public class OpdexStakingPool : OpdexStandardPool, IOpdexStakingPool
{
    public OpdexStakingPool(ISmartContractState smartContractState, Address token, Address stakeToken) 
        : base(smartContractState, token)
    {
        StakeToken = stakeToken;
    }

    public Address StakeToken
    {
        get => State.GetAddress(nameof(StakeToken));
        private set => State.SetAddress(nameof(StakeToken), value);
    }
    
    public UInt256 TotalStaked
    {
        get => State.GetUInt256(nameof(TotalStaked));
        private set => State.SetUInt256(nameof(TotalStaked), value);
    }
    
    public UInt256 TotalStakedApplicable
    {
        get => State.GetUInt256(nameof(TotalStakedApplicable));
        private set => State.SetUInt256(nameof(TotalStakedApplicable), value);
    }
    
    public UInt256 StakingRewardsBalance
    {
        get => State.GetUInt256(nameof(StakingRewardsBalance));
        private set => State.SetUInt256(nameof(StakingRewardsBalance), value);
    }

    public UInt256 GetStakedBalance(Address address) 
        => State.GetUInt256($"StakedBalance:{address}");
    
    private void SetStakedBalance(Address address, UInt256 weight) 
        => State.SetUInt256($"StakedBalance:{address}", weight);
    
    public UInt256 GetStakedWeight(Address address) 
        => State.GetUInt256($"StakedWeight:{address}");

    private void SetStakedWeight(Address address, UInt256 weightK) 
        => State.SetUInt256($"StakedWeight:{address}", weightK);

    public UInt256 GetStakingRewards(Address staker) 
        => GetStakingRewardsExecute(staker, GetStakedBalance(staker));

    public void Stake(UInt256 amount)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        MintStakingRewards(ReserveCrs, ReserveSrc);

        var stakedBalance = GetStakedBalance(Message.Sender);
        if (stakedBalance > 0)
        {
            WithdrawStakingRewardsExecute(Message.Sender, stakedBalance, false);
            ExitStakingExecute(Message.Sender, stakedBalance, false);
        }
        
        stakedBalance += amount;

        SafeTransferFrom(StakeToken, Message.Sender, Address, amount);
        SetStakedBalance(Message.Sender, stakedBalance);
        SetStakingWeightExecute(stakedBalance);
        NominatePool();
        Unlock();
    }

    public void WithdrawStakingRewards(Address to, bool liquidate)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var stakedBalance = GetStakedBalance(Message.Sender);
        
        WithdrawStakingRewardsExecute(to, stakedBalance, liquidate);
        SetStakingWeightExecute(stakedBalance);
        Unlock();
    }
    
    public void ExitStaking(Address to, bool liquidate)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var stakedBalance = GetStakedBalance(Message.Sender);
        
        WithdrawStakingRewardsExecute(to, stakedBalance, liquidate);
        ExitStakingExecute(to, stakedBalance, true);
        NominatePool();
        Unlock();
    }
    
    public override UInt256 Mint(Address to)
    {
        EnsureUnlocked();
        
        MintStakingRewards(ReserveCrs, ReserveSrc);

        var liquidity = MintExecute(to);
        
        Unlock();

        return liquidity;
    }

    public override UInt256[] Burn(Address to)
    {
        EnsureUnlocked();
        
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var amounts = BurnExecute(to, GetBalance(Address) - StakingRewardsBalance);
        
        Unlock();

        return amounts;
    }
    
    public override void Skim(Address to)
    {
        EnsureUnlocked();
    
        SkimExecute(to);

        TransferTokensExecute(Address, to, GetBalance(Address) - StakingRewardsBalance);
    
        Unlock();
    }

    public override void Sync()
    {
        EnsureUnlocked();

        UpdateReserves(Balance, GetSrcBalance(Token, Address));
        
        StakingRewardsBalance = GetBalance(Address);
    
        Unlock();
    }

    private void NominatePool() => Call(StakeToken, 0ul, "Nominate");
    
    private void SetStakingWeightExecute(UInt256 balance)
    {
        UInt256 weight = 0;
        
        if (balance > 0)
        {
            var totalStaked = TotalStaked;
            var stakingRewardsBalance = StakingRewardsBalance;
            
            weight = totalStaked > 0 && stakingRewardsBalance > 0
                ? balance * stakingRewardsBalance / totalStaked
                : 0;

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
        var currentWeight = balance * StakingRewardsBalance / TotalStakedApplicable;

        return currentWeight <= stakedWeight ? 0 : currentWeight - stakedWeight;
    }

    private void WithdrawStakingRewardsExecute(Address to, UInt256 stakedBalance, bool liquidate)
    {
        var rewards = GetStakingRewardsExecute(Message.Sender, stakedBalance);
        
        StakingRewardsBalance -= rewards;
        TotalStaked -= stakedBalance;
        TotalStakedApplicable -= stakedBalance;
        
        if (liquidate) BurnExecute(to, rewards);
        else TransferTokensExecute(Address, to, rewards);
        
        Log(new OpdexRewardEvent
        {
            Sender = Message.Sender,
            Amount = stakedBalance,
            Reward = rewards
        });
    }

    private void ExitStakingExecute(Address to, UInt256 stakedBalance, bool transfer)
    {
        if (transfer)
        {
            SafeTransferTo(StakeToken, to, stakedBalance);
            SetStakedBalance(Message.Sender, 0);
        }
        
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