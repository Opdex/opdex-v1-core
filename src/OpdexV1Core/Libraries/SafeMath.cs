public static class SafeMath
{
    public static ulong Add(ulong x, ulong y) => checked(x + y);
    
    public static ulong Sub(ulong x, ulong y) => checked(x - y);

    public static ulong Mul(ulong x, ulong y) => checked(x * y);

    public static ulong Div(ulong x, ulong y) => x / y;

    public static ulong Rem(ulong x, ulong y) => x % y;

    public static ulong Sqrt(ulong y)
    {
        ulong z = 0;
        
        if (y > 3) {
            z = y;
            var x = y / 2 + 1;
            while (x < z) {
                z = x;
                x = (y / x + x) / 2;
            }
        } else if (y != 0) {
            z = 1;
        }

        return z;
    }
}