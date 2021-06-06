public struct PoolStateKeys
{
    public const string TotalSupply = "PA";
    public const string TransactionFee = "PB";
    public const string Token = "PC";
    public const string ReserveCrs = "PD";
    public const string ReserveSrc = "PE";
    public const string KLast = "PF";
    public const string Locked = "PG";
    public const string Balance = "PH";
    public const string Allowance = "PI";

    // Standard Pool
    public const string Market = "PJ";
    public const string AuthProviders = "PK";
    public const string AuthTraders = "PL";
    public const string MarketFeeEnabled = "PM";

    // Staking Pool
    public const string StakingToken = "PN";
    public const string MiningPool = "PO";
    public const string TotalStaked = "PP";
    public const string StakingRewardsBalance = "PQ";
    public const string RewardPerStakedTokenLast = "PR";
    public const string ApplicableStakingRewards = "PS";
    public const string RewardPerStakedToken = "PT";
    public const string Reward = "PU";
    public const string StakedBalance = "PV";

    // Mining Pool
    public const string MiningGovernance = "PW";
    public const string MinedToken = "PX";
    public const string MiningPeriodEndBlock = "PY";
    public const string RewardRate = "PZ";
    public const string MiningDuration = "PAA";
    public const string LastUpdateBlock = "PAB";
}