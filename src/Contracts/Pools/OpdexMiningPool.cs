using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// Mining pool for staking Opdex liquidity pool tokens in order to earn new mined tokens.
/// </summary>
public class OpdexMiningPool : SmartContract, IOpdexMiningPool
{
    private const ulong SatsPerToken = 100_000_000;
    
    /// <summary>
    /// Constructor initializing a mining pool contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="minedToken">The address of the token being mined.</param>
    /// <param name="stakingToken">The address of the liquidity pool token used for mining.</param>
    public OpdexMiningPool(ISmartContractState state, Address minedToken, Address stakingToken) : base(state)
    {
        MinedToken = minedToken;
        StakingToken = stakingToken;
        MiningGovernance = GetMiningGovernance();
        MiningDuration = GetMiningDuration();
    }

    /// <inheritdoc />
    public Address MiningGovernance
    {
        get => State.GetAddress(nameof(MiningGovernance));
        private set => State.SetAddress(nameof(MiningGovernance), value);
    }

    /// <inheritdoc />
    public Address StakingToken
    {
        get => State.GetAddress(nameof(StakingToken));
        private set => State.SetAddress(nameof(StakingToken), value);
    }

    /// <inheritdoc />
    public Address MinedToken
    {
        get => State.GetAddress(nameof(MinedToken));
        private set => State.SetAddress(nameof(MinedToken), value);
    }

    /// <inheritdoc />
    public ulong MiningPeriodEndBlock
    {
        get => State.GetUInt64(nameof(MiningPeriodEndBlock));
        private set => State.SetUInt64(nameof(MiningPeriodEndBlock), value);
    }
    
    /// <inheritdoc />
    public UInt256 RewardRate
    {
        get => State.GetUInt256(nameof(RewardRate));
        private set => State.SetUInt256(nameof(RewardRate), value);
    }
    
    /// <inheritdoc />
    public ulong MiningDuration
    {
        get => State.GetUInt64(nameof(MiningDuration));
        private set => State.SetUInt64(nameof(MiningDuration), value);
    }
    
    /// <inheritdoc />
    public ulong LastUpdateBlock
    {
        get => State.GetUInt64(nameof(LastUpdateBlock));
        private set => State.SetUInt64(nameof(LastUpdateBlock), value);
    }
    
    /// <inheritdoc />
    public UInt256 RewardPerStakedTokenLast
    {
        get => State.GetUInt256(nameof(RewardPerStakedTokenLast));
        private set => State.SetUInt256(nameof(RewardPerStakedTokenLast), value);
    }
    
    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }
    
    /// <inheritdoc />
    public bool Locked
    {
        get => State.GetBool(nameof(Locked));
        private set => State.SetBool(nameof(Locked), value);
    }

    /// <inheritdoc />
    public UInt256 GetStoredRewardPerStakedToken(Address address)
    {
        return State.GetUInt256($"RewardPerStakedToken:{address}");
    }

    private void SetStoredRewardPerStakedToken(Address address, UInt256 reward)
    {
        State.SetUInt256($"RewardPerStakedToken:{address}", reward);
    }
    
    /// <inheritdoc />
    public UInt256 GetStoredReward(Address address)
    {
        return State.GetUInt256($"Reward:{address}");
    }

    private void SetStoredReward(Address address, UInt256 reward)
    {
        State.SetUInt256($"Reward:{address}", reward);
    }

    /// <inheritdoc />
    public UInt256 GetBalance(Address address)
    {
        return State.GetUInt256($"Balance:{address}");
    }

    private void SetBalance(Address address, UInt256 reward)
    {
        State.SetUInt256($"Balance:{address}", reward);
    }

    /// <inheritdoc />
    public ulong LatestBlockApplicable()
    {
        return Block.Number > MiningPeriodEndBlock ? MiningPeriodEndBlock : Block.Number;
    }

    /// <inheritdoc />
    public UInt256 GetRewardForDuration()
    {
        return RewardRate * MiningDuration;
    }

    /// <inheritdoc />
    public UInt256 GetRewardPerStakedToken()
    {
        var totalSupply = TotalSupply;
        
        if (totalSupply == 0) return RewardPerStakedTokenLast;

        var remainingRewards = (LatestBlockApplicable() - LastUpdateBlock) * RewardRate;
        
        return RewardPerStakedTokenLast + (remainingRewards * SatsPerToken / totalSupply);
    }

    /// <inheritdoc />
    public UInt256 GetMiningRewards(Address address)
    {
        var balance = GetBalance(address);
        var rewardPerToken = GetRewardPerStakedToken();
        var addressRewardPaid = GetStoredRewardPerStakedToken(address);
        var remainingReward = rewardPerToken - addressRewardPaid;
        var reward = GetStoredReward(address);
        
        return reward + (balance * remainingReward / SatsPerToken);
    }

    /// <inheritdoc />
    public void StartMining(UInt256 amount)
    {
        EnsureUnlocked();
        Assert(amount > 0, "OPDEX: INVALID_AMOUNT");

        UpdateMiningPosition(Message.Sender);
        
        TotalSupply += amount;
        
        SetBalance(Message.Sender, GetBalance(Message.Sender) + amount);

        SafeTransferFrom(StakingToken, Message.Sender, Address, amount);

        Log(new MineLog { Miner = Message.Sender, Amount = amount, TotalSupply = TotalSupply, EventType = MineEventType.StartMining });
        
        Unlock();
    }

    /// <inheritdoc />
    public void CollectMiningRewards()
    {
        EnsureUnlocked();
        
        UpdateMiningPosition(Message.Sender);

        CollectMiningRewardsExecute();
        
        Unlock();
    }

    /// <inheritdoc />
    public void StopMining(UInt256 amount)
    {
        EnsureUnlocked();

        UpdateMiningPosition(Message.Sender);

        var balance = GetBalance(Message.Sender);
        
        Assert(amount > 0 && balance >= amount, "OPDEX: INVALID_AMOUNT");

        TotalSupply -= amount;
        
        SetBalance(Message.Sender, balance - amount);
        
        SafeTransferTo(StakingToken, Message.Sender, amount);
        
        CollectMiningRewardsExecute();
        
        Log(new MineLog { Miner = Message.Sender, Amount = amount, TotalSupply = TotalSupply, EventType = MineEventType.StopMining });

        Unlock();
    }
    
    /// <inheritdoc />
    public void NotifyRewardAmount(UInt256 reward)
    {
        EnsureUnlocked();
        
        Assert(Message.Sender == MiningGovernance, "OPDEX: UNAUTHORIZED");
        
        UpdateMiningPosition(Address.Zero);

        var miningDuration = MiningDuration;
        
        if (Block.Number >= MiningPeriodEndBlock)
        {
            RewardRate = reward / miningDuration;
        }
        else
        {
            var remaining = MiningPeriodEndBlock - Block.Number;
            var leftover = remaining * RewardRate;
            
            RewardRate = (reward + leftover) / miningDuration;
        }

        var balanceResult = Call(MinedToken, 0, nameof(IStandardToken.GetBalance), new object[] {Address});
        var balance = (UInt256)balanceResult.ReturnValue;
        
        Assert(balanceResult.Success && balance > 0, "OPDEX: INVALID_BALANCE");
        Assert(RewardRate <= balance / miningDuration, "OPDEX: PROVIDED_REWARD_TOO_HIGH");
        
        var miningPeriodEnd = Block.Number + miningDuration;
        
        MiningPeriodEndBlock = miningPeriodEnd;
        LastUpdateBlock = Block.Number;

        Log(new EnableMiningLog { Amount = reward, RewardRate = RewardRate, MiningPeriodEndBlock = miningPeriodEnd});
        
        Unlock(); 
    }

    private void CollectMiningRewardsExecute()
    {
        var reward = GetStoredReward(Message.Sender);

        if (reward == 0) return;

        SetStoredReward(Message.Sender, 0);
            
        SafeTransferTo(MinedToken, Message.Sender, reward);
            
        Log(new CollectMiningRewardsLog { Miner = Message.Sender, Amount = reward });
    }

    private void UpdateMiningPosition(Address address)
    {
        var rewardPerToken = GetRewardPerStakedToken();
        
        RewardPerStakedTokenLast = rewardPerToken;
        LastUpdateBlock = LatestBlockApplicable();

        if (address == Address.Zero) return;
        
        SetStoredReward(address, GetMiningRewards(address));
        SetStoredRewardPerStakedToken(address, rewardPerToken);
    }
    
    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(IStandardToken.TransferTo), new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
    
    private void SafeTransferFrom(Address token, Address from, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(IStandardToken.TransferFrom), new object[] {from, to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_FROM");
    }

    private Address GetMiningGovernance()
    {
        var response = Call(MinedToken, 0, "get_MiningGovernance");
        
        Assert(response.Success, "OPDEX: INVALID_MINING_GOVERNANCE");

        var address = (Address)response.ReturnValue;

        Assert(address != Address.Zero, "OPDEX: INVALID_GOVERNANCE_ADDRESS");

        return address;
    }

    private ulong GetMiningDuration()
    {
        var response = Call(MiningGovernance, 0, "get_MiningDuration");
        
        Assert(response.Success, "OPDEX: INVALID_MINING_DURATION");
        
        var duration = (ulong)response.ReturnValue;

        Assert(duration != 0ul, "OPDEX: INVALID_DURATION_AMOUNT");

        return duration;
    }
    
    private void EnsureUnlocked()
    {
        Assert(!Locked, "OPDEX: LOCKED");
        Locked = true;
    }

    private void Unlock() => Locked = false;
}