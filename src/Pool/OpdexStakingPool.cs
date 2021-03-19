using Stratis.SmartContracts;

public class OpdexStakingPool : OpdexStandardPool
{
    public OpdexStakingPool(ISmartContractState smartContractState, Address token, Address stakeToken) : base(smartContractState, token)
    {
        Controller = Message.Sender;
        StakeToken = stakeToken;
    }

    public Address Controller
    {
        get => State.GetAddress(nameof(Controller));
        private set => State.SetAddress(nameof(Controller), value);
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
    {
        return State.GetUInt256($"StakedBalance:{address}");
    }
    
    private void SetStakedBalance(Address address, UInt256 weight)
    {
        State.SetUInt256($"StakedBalance:{address}", weight);
    }
    
    public UInt256 GetStakedWeight(Address address)
    {
        return State.GetUInt256($"StakedWeight:{address}");
    }
    
    private void SetStakedWeight(Address address, UInt256 weightK)
    {
        State.SetUInt256($"StakedWeight:{address}", weightK);
    }

    public override UInt256 Mint(Address to)
    {
        EnsureUnlocked();
        
        // Todo: Maybe only if staking is enabled
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var liquidity = MintExecute(to);
        
        Unlock();

        return liquidity;
    }

    public override UInt256[] Burn(Address to)
    {
        EnsureUnlocked();
        
        // Todo: Maybe only if staking is enabled
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var amounts = BurnExecute(to, GetBalance(Address) - StakingRewardsBalance);
        
        Unlock();

        return amounts;
    }

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
        Unlock();
    }

    public void WithdrawStakingRewards(Address to, bool burn)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var stakedBalance = GetStakedBalance(Message.Sender);
        
        WithdrawStakingRewardsExecute(to, stakedBalance, burn);
        SetStakingWeightExecute(stakedBalance);
        Unlock();
    }
    
    public void ExitStaking(Address to, bool burn)
    {
        EnsureUnlocked();
        EnsureStakingEnabled();
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var stakedBalance = GetStakedBalance(Message.Sender);
        
        WithdrawStakingRewardsExecute(to, stakedBalance, burn);
        ExitStakingExecute(to, stakedBalance, true);
        Unlock();
    }

    public UInt256 GetStakingRewards(Address staker)
    {
        return GetStakingRewardsExecute(staker, GetStakedBalance(staker));
    }
    
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
            
            LogStakeEvent(Message.Sender, balance, weight);
        }
        
        SetStakedWeight(Message.Sender, weight);
    }

    private UInt256 GetStakingRewardsExecute(Address staker, UInt256 balance)
    {
        var stakedWeight = GetStakedWeight(staker);
        var currentWeight = balance * StakingRewardsBalance / TotalStakedApplicable;

        return currentWeight <= stakedWeight ? 0 : currentWeight - stakedWeight;
    }

    private void WithdrawStakingRewardsExecute(Address to, UInt256 stakedBalance, bool burn)
    {
        var rewards = GetStakingRewardsExecute(Message.Sender, stakedBalance);
        
        StakingRewardsBalance -= rewards;
        TotalStaked -= stakedBalance;
        TotalStakedApplicable -= stakedBalance;
        
        if (burn) BurnExecute(to, rewards);
        else TransferTokensExecute(Address, to, rewards);
        
        LogRewardEvent(Message.Sender, stakedBalance, rewards);
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

    private bool StakingEnabled()
    {
        var stakeToken = StakeToken;

        if (stakeToken == Token) return false;

        if (stakeToken != Address.Zero) return true;
        
        // Todo: Waste of a call if staking is intentionally delayed
        StakeToken = (Address)Call(Controller, 0, "get_StakeToken").ReturnValue;
        
        return StakeToken != Address.Zero;
    }

    private void EnsureStakingEnabled()
    {
        Assert(StakingEnabled(), "OPDEX: STAKING_UNAVAILABLE");
    }

    private void MintStakingRewards(ulong reserveCrs, UInt256 reserveSrc)
    {
        if (!StakingEnabled()) return;
        
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

    private void LogStakeEvent(Address sender, UInt256 amount, UInt256 weight)
    {
        Log(new OpdexStakeEvent
        {
            Sender = sender,
            Amount = amount,
            Weight = weight
        });
    }
    
    private void LogRewardEvent(Address sender, UInt256 amount, UInt256 reward)
    {
        Log(new OpdexRewardEvent
        {
            Sender = sender,
            Amount = amount,
            Reward = reward
        });
    }
}