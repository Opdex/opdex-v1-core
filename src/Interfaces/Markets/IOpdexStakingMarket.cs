using Stratis.SmartContracts;

public interface IOpdexStakingMarket : IOpdexMarket
{
    /// <summary>
    /// The address of the staking token.
    /// </summary>
    Address StakingToken { get; }
}