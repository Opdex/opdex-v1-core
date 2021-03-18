using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

public class OpdexPair : ContractBase, IStandardToken256
{
    private const ulong MinimumLiquidity = 1000;
    private const string TokenSymbol = "OLPT";
    private const string TokenName = "Opdex Liquidity Pool Token";
    private const byte TokenDecimals = 8;
    
    public OpdexPair(ISmartContractState smartContractState, Address token, Address stakeToken) : base(smartContractState)
    {
        Controller = Message.Sender;
        Token = token;
        StakeToken = stakeToken;
    }
    
    public byte Decimals => TokenDecimals;
    
    public string Name => TokenName;
    
    public string Symbol => TokenSymbol;

    public Address Controller
    {
        get => State.GetAddress(nameof(Controller));
        private set => State.SetAddress(nameof(Controller), value);
    }
    
    public Address Token
    {
        get => State.GetAddress(nameof(Token));
        private set => State.SetAddress(nameof(Token), value);
    }

    public Address StakeToken
    {
        get => State.GetAddress(nameof(StakeToken));
        private set => State.SetAddress(nameof(StakeToken), value);
    }
    
    public ulong ReserveCrs
    {
        get => State.GetUInt64(nameof(ReserveCrs));
        private set => State.SetUInt64(nameof(ReserveCrs), value);
    }
    
    public UInt256 ReserveSrc
    {
        get => State.GetUInt256(nameof(ReserveSrc));
        private set => State.SetUInt256(nameof(ReserveSrc), value);
    }
    
    public byte[][] Reserves => new [] { Serializer.Serialize(ReserveCrs), Serializer.Serialize(ReserveSrc) };
    
    public UInt256 KLast
    {
        get => State.GetUInt256(nameof(KLast));
        private set => State.SetUInt256(nameof(KLast), value);
    }

    public UInt256 TotalSupply 
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }

    public UInt256 TotalWeight
    {
        get => State.GetUInt256(nameof(TotalWeight));
        private set => State.SetUInt256(nameof(TotalWeight), value);
    }
    
    public UInt256 TotalWeightApplicable
    {
        get => State.GetUInt256(nameof(TotalWeightApplicable));
        private set => State.SetUInt256(nameof(TotalWeightApplicable), value);
    }
    
    public UInt256 StakingRewardsBalance
    {
        get => State.GetUInt256(nameof(StakingRewardsBalance));
        private set => State.SetUInt256(nameof(StakingRewardsBalance), value);
    }
    
    public bool Locked
    {
        get => State.GetBool(nameof(Locked));
        private set => State.SetBool(nameof(Locked), value);
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

    public UInt256 GetBalance(Address address)
    {
        return State.GetUInt256($"Balance:{address}");
    }

    private void SetBalance(Address address, UInt256 amount)
    {
        State.SetUInt256($"Balance:{address}", amount);
    }

    // IStandardToken256 interface compatibility
    public UInt256 Allowance(Address owner, Address spender)
    {
        return GetAllowance(owner, spender);
    }
    
    public UInt256 GetAllowance(Address owner, Address spender)
    {
        return State.GetUInt256($"Allowance:{owner}:{spender}");
    }

    private void SetAllowance(Address owner, Address spender, UInt256 amount)
    {
        State.SetUInt256($"Allowance:{owner}:{spender}", amount);
    }

    public bool TransferTo(Address to, UInt256 amount)
    {
        return TransferExecute(Message.Sender, to, amount);
    }
    
    public bool TransferFrom(Address from, Address to, UInt256 amount)
    {
        var allowance = GetAllowance(from, Message.Sender);
        
        if (allowance > 0) SetAllowance(from, Message.Sender, allowance - amount);
        
        return TransferExecute(from, to, amount);
    }

    // IStandardToken256 interface compatibility
    public bool Approve(Address spender, UInt256 currentAmount, UInt256 amount)
    {
        return Approve(spender, amount);
    }
    
    public bool Approve(Address spender, UInt256 amount)
    {
        SetAllowance(Message.Sender, spender, amount);

        LogApprovalEvent(Message.Sender, spender, amount);
        
        return true;
    }

    public UInt256 Mint(Address to)
    {
        EnsureUnlocked();
        
        var reserveCrs = ReserveCrs;
        var reserveSrc = ReserveSrc;
        var balanceCrs = Balance;
        var balanceSrc = GetSrcBalance(Token, Address);
        var amountCrs = balanceCrs - reserveCrs;
        var amountSrc = balanceSrc - reserveSrc;
        var totalSupply = TotalSupply;
        
        MintStakingRewards(reserveCrs, reserveSrc);
        
        UInt256 liquidity;
        if (totalSupply == 0)
        {
            liquidity = Sqrt(amountCrs * amountSrc) - MinimumLiquidity;
            MintTokensExecute(Address.Zero, MinimumLiquidity);
        }
        else
        {
            var amountCrsLiquidity = amountCrs * totalSupply / reserveCrs;
            var amountSrcLiquidity = amountSrc * totalSupply / reserveSrc;
            liquidity = amountCrsLiquidity > amountSrcLiquidity ? amountSrcLiquidity : amountCrsLiquidity;
        }
        
        Assert(liquidity > 0, "OPDEX: INSUFFICIENT_LIQUIDITY");
        
        MintTokensExecute(to, liquidity);
        Update(balanceCrs, balanceSrc);
        
        KLast = ReserveCrs * ReserveSrc;

        LogMintEvent(amountCrs, amountSrc, Message.Sender);
            
        Unlock();
        
        return liquidity;
    }

    public UInt256[] Burn(Address to)
    {
        EnsureUnlocked();

        var amounts = BurnExecute(to);
        
        Unlock();

        return amounts;
    }

    public void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address to, byte[] data)
    {
        EnsureUnlocked();
        
        var reserveCrs = ReserveCrs;
        var reserveSrc = ReserveSrc;
        var token = Token;
        
        Assert(amountCrsOut > 0 ^ amountSrcOut > 0, "OPDEX: INVALID_OUTPUT_AMOUNT");
        Assert(amountCrsOut < reserveCrs && amountSrcOut < reserveSrc, "OPDEX: INSUFFICIENT_LIQUIDITY");
        Assert(to != token && to != Address, "OPDEX: INVALID_TO");
        
        SafeTransfer(to, amountCrsOut);
        SafeTransferTo(token, to, amountSrcOut);

        if (data.Length > 0)
        {
            var callbackData = Serializer.ToStruct<CallbackData>(data);
            var parameters = callbackData.Data.Length > 0 ? new object[] {callbackData.Data} : null;
            Call(to, 0, callbackData.Method, parameters);
        }
        
        var balanceCrs = Balance;
        var balanceSrc = GetSrcBalance(token, Address);
        var crsDifference = reserveCrs - amountCrsOut;
        var amountCrsIn = balanceCrs > crsDifference ? balanceCrs - crsDifference : 0;
        var srcDifference = reserveSrc - amountSrcOut;
        var amountSrcIn = balanceSrc > srcDifference ? balanceSrc - srcDifference : 0;
        
        Assert(amountCrsIn > 0 || amountSrcIn > 0, "OPDEX: ZERO_INPUT_AMOUNT");
        
        var balanceCrsAdjusted = (balanceCrs * 1_000) - (amountCrsIn * 3);
        var balanceSrcAdjusted = (balanceSrc * 1_000) - (amountSrcIn * 3);
        
        Assert(balanceCrsAdjusted * balanceSrcAdjusted >= reserveCrs * reserveSrc * 1_000_000, "OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        
        Update(balanceCrs, balanceSrc);
        
        LogSwapEvent(amountCrsIn, amountCrsOut, amountSrcIn, amountSrcOut, Message.Sender, to);
        
        Unlock();
    }
    
    public void Stake(UInt256 amount)
    {
        EnsureStakingEnabled();
        EnsureUnlocked();

        MintStakingRewards(ReserveCrs, ReserveSrc);

        var stakedBalance = GetStakedBalance(Message.Sender);
        if (stakedBalance > 0)
        {
            // Todo: ensure not having MintFee run after this process is ok
            // Reset and withdraw existing rewards to reset staker
            WithdrawStakingRewardsExecute(Message.Sender, false, true);
        }
        
        // Todo: Issue here if the user is adding onto their staking balance
        // withdraw currently transfers staking balance back to user,
        // causing allowance issues
        SafeTransferFrom(StakeToken, Message.Sender, Address, amount);
        SetStakedBalance(Message.Sender, stakedBalance + amount);

        var totalWeight = TotalWeight;
        var stakingRewardsBalance = StakingRewardsBalance;

        // Todo: amount should be += stakedBalance
        var weight = totalWeight != UInt256.Zero && stakingRewardsBalance != UInt256.Zero
            ? amount * stakingRewardsBalance / totalWeight
            : 0;
        
        SetStakedWeight(Address, weight);
        
        TotalWeight += amount;
        
        LogStakeEvent(Message.Sender, amount, weight);
        
        Unlock();
    }
    
    public void WithdrawStakingRewards(Address to, bool burn)
    {
        EnsureStakingEnabled();
        EnsureUnlocked();

        WithdrawStakingRewardsExecute(to, burn, false);
        
        Unlock();
    }
    
    public void ExitStaking(Address to, bool burn)
    {
        EnsureStakingEnabled();
        EnsureUnlocked();
        
        WithdrawStakingRewardsExecute(to, burn, true);

        Unlock();
    }

    // Todo: Consider adjusting staked balances and weight
    public void Skim(Address to)
    {
        EnsureUnlocked();
        
        var token = Token;
        var balanceSrc = GetSrcBalance(token, Address) - ReserveSrc;
        var balanceCrs = Balance - ReserveCrs;
        
        SafeTransfer(to, balanceCrs);
        SafeTransferTo(token, to, balanceSrc);
        
        Unlock();
    }

    public void Sync()
    {
        EnsureUnlocked();
        
        Update(Balance, GetSrcBalance(Token, Address));
        
        Unlock();
    }

    private UInt256[] BurnExecute(Address to)
    {
        var reserveCrs = ReserveCrs;
        var reserveSrc = ReserveSrc;
        
        MintStakingRewards(reserveCrs, reserveSrc);
        
        var address = Address;
        var token = Token;
        var balanceCrs = Balance;
        var balanceSrc = GetSrcBalance(token, address);
        var liquidity = GetBalance(Address) - StakingRewardsBalance;
        var totalSupply = TotalSupply;
        var amountCrs = (ulong)(liquidity * balanceCrs / totalSupply);
        var amountSrc = liquidity * balanceSrc / totalSupply;
        
        Assert(amountCrs > 0 && amountSrc > 0, "OPDEX: INSUFFICIENT_LIQUIDITY_BURNED");
        
        BurnTokensExecute(address, liquidity);
        SafeTransfer(to, amountCrs);
        SafeTransferTo(token, to, amountSrc);
        
        balanceCrs = Balance;
        balanceSrc = GetSrcBalance(token, address);
        
        Update(balanceCrs, balanceSrc);
        
        KLast = ReserveCrs * ReserveSrc;

        LogBurnEvent(amountCrs, amountSrc, Message.Sender, to);
        
        return new [] {amountCrs, amountSrc};
    }

    private void WithdrawStakingRewardsExecute(Address to, bool burn, bool exit)
    {
        MintStakingRewards(ReserveCrs, ReserveSrc);
        
        var stakingRewardsBalance = StakingRewardsBalance;
        var totalWeightApplicable = TotalWeightApplicable;
        var stakedBalance = GetStakedBalance(Message.Sender);
        var stakedWeight = GetStakedWeight(Message.Sender);
        var contractBalance = GetBalance(Address);
        
        Assert(contractBalance >= stakingRewardsBalance);
        
        var currentWeight = stakedBalance * contractBalance / totalWeightApplicable;
        
        if (currentWeight <= stakedWeight) return; 
        
        var rewards = currentWeight - stakedWeight;

        stakingRewardsBalance -= rewards;
        StakingRewardsBalance = stakingRewardsBalance;
        
        if (burn) BurnExecute(to);
        else TransferExecute(Address, to, rewards);
        
        LogRewardEvent(Message.Sender, stakedBalance, currentWeight, rewards);
        
        if (exit)
        {
            // Todo: maybe don't have exit in this method, for stakers adding to their balance,
            // we don't want to transfer these tokens. 
            SafeTransferTo(StakeToken, to, stakedBalance);
        
            SetStakedBalance(Message.Sender, 0);
            SetStakedWeight(Message.Sender, 0);
        
            TotalWeight -= stakedBalance;
            
            // Todo: this may not be necessary
            TotalWeightApplicable -= stakedBalance;
        }
        else
        {
            var resetWeight = stakedBalance * stakingRewardsBalance / TotalWeight;
            
            SetStakedWeight(Message.Sender, resetWeight);
            
            LogStakeEvent(Message.Sender, stakedBalance, resetWeight);
        }
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
    
    private void Update(ulong balanceCrs, UInt256 balanceSrc)
    {
        ReserveCrs = balanceCrs;
        
        ReserveSrc = balanceSrc;

        LogSyncEvent(balanceCrs, balanceSrc);
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

        TotalWeightApplicable = TotalWeight;
        
        MintTokensExecute(Address, liquidity);
    }

    private void MintTokensExecute(Address to, UInt256 amount)
    {
        TotalSupply += amount;

        SetBalance(to, GetBalance(to) + amount);

        LogTransferEvent(Address.Zero, to, amount);
    }

    private void BurnTokensExecute(Address from, UInt256 amount)
    {
        TotalSupply -= amount;

        SetBalance(from, GetBalance(from) - amount);
        
        LogTransferEvent(from, Address.Zero, amount);
    }
    
    private UInt256 GetSrcBalance(Address token, Address owner)
    {
        var balanceResponse = Call(token, 0, "GetBalance", new object[] {owner});
        
        Assert(balanceResponse.Success, "OPDEX: INVALID_BALANCE");
        
        return (UInt256)balanceResponse.ReturnValue;
    }
    
    private bool TransferExecute(Address from, Address to, UInt256 amount)
    {
        SetBalance(from, GetBalance(from) - amount);
        SetBalance(to, GetBalance(to) + amount);
        
        LogTransferEvent(from, to, amount);
        
        return true;
    }
    
    private void EnsureUnlocked()
    {
        Assert(!Locked, "OPDEX: LOCKED");
        Locked = true;
    }
    
    private void Unlock()
    {
        Locked = false;
    }
    
    private static UInt256 Sqrt(UInt256 value)
    {
        if (value <= 3) return 1;
        
        var result = value;
        var root = value / 2 + 1;
        
        while (root < result) 
        {
            result = root;
            root = (value / root + root) / 2;
        }
        
        return result;
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
    
    private void LogRewardEvent(Address sender, UInt256 amount, UInt256 weight, UInt256 reward)
    {
        Log(new OpdexRewardEvent
        {
            Sender = sender,
            Amount = amount,
            Weight = weight,
            Reward = reward
        });
    }

    private void LogApprovalEvent(Address owner, Address spender, UInt256 amount)
    {
        Log(new OpdexApprovalEvent
        {
            Owner = owner, 
            Spender = spender, 
            Amount = amount
        });
    }

    private void LogMintEvent(ulong amountCrs, UInt256 amountSrc, Address sender)
    {
        Log(new OpdexMintEvent
        {
            AmountCrs = amountCrs, 
            AmountSrc = amountSrc, 
            Sender = sender
        });
    }

    private void LogBurnEvent(ulong amountCrs, UInt256 amountSrc, Address sender, Address to)
    {
        Log(new OpdexBurnEvent
        {
            AmountCrs = amountCrs, 
            AmountSrc = amountSrc, 
            Sender = sender, 
            To = to
        });
    }

    private void LogSwapEvent(ulong amountCrsIn, ulong amountCrsOut, UInt256 amountSrcIn, UInt256 amountSrcOut, 
        Address from, Address to)
    {
        Log(new OpdexSwapEvent 
        { 
            AmountCrsIn = amountCrsIn, 
            AmountCrsOut = amountCrsOut, 
            AmountSrcIn = amountSrcIn,
            AmountSrcOut = amountSrcOut, 
            Sender = from, 
            To = to
        });
    }

    private void LogSyncEvent(ulong reserveCrs, UInt256 reserveSrc)
    {
        Log(new OpdexSyncEvent
        {
            ReserveCrs = reserveCrs, 
            ReserveSrc = reserveSrc
        });
    }

    private void LogTransferEvent(Address from, Address to, UInt256 amount)
    {
        Log(new OpdexTransferEvent
        {
            From = from, 
            To = to, 
            Amount = amount
        });
    }
}