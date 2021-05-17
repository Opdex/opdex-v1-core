using Stratis.SmartContracts;

public struct CreateLiquidityPoolLog
{
    [Index] public Address Token;
    [Index] public Address Pool;
}