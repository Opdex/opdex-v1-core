using Stratis.SmartContracts;

public struct LiquidityPoolCreatedLog
{
    [Index] public Address Token;
    [Index] public Address Pool;
}