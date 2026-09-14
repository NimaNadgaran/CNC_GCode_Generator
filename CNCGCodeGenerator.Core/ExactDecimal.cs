using System.Numerics;

namespace CNCGCodeGenerator.Core;

// Decimal arithmetic may silently round even inside checked expressions. Verify the
// exact scaled-integer result, including at extreme magnitudes, before using it.
internal static class ExactDecimal
{
    public static (decimal FullSteps, decimal Remainder) DivideIntoSteps(decimal coverage, decimal step)
    {
        var scale = Math.Max(Scale(coverage), Scale(step));
        var quotient = BigInteger.DivRem(Scaled(coverage, scale), Scaled(step, scale), out var exactRemainder);
        var remainder = coverage % step;
        Verify(Scaled(remainder, scale) == exactRemainder);
        return ((decimal)quotient, remainder);
    }
    public static decimal Add(decimal left, decimal right)
    {
        var result = checked(left + right);
        var scale = Math.Max(Scale(left), Math.Max(Scale(right), Scale(result)));
        Verify(Scaled(left, scale) + Scaled(right, scale) == Scaled(result, scale));
        return result;
    }
    public static decimal Subtract(decimal left, decimal right) => Add(left, -right);
    public static decimal Multiply(decimal left, decimal right)
    {
        var result = checked(left * right);
        var scale = Scale(left) + Scale(right);
        var common = Math.Max(scale, Scale(result));
        Verify(Scaled(left, Scale(left)) * Scaled(right, Scale(right)) * BigInteger.Pow(10, common - scale) == Scaled(result, common));
        return result;
    }
    private static int Scale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xff;
    private static BigInteger Scaled(decimal value, int scale)
    {
        var bits = decimal.GetBits(value);
        var integer = (BigInteger)(uint)bits[0] + ((BigInteger)(uint)bits[1] << 32) + ((BigInteger)(uint)bits[2] << 64);
        if (bits[3] < 0) integer = -integer;
        return integer * BigInteger.Pow(10, scale - Scale(value));
    }
    private static void Verify(bool exact)
    {
        if (!exact) throw new ValidationException("Calculation", "Exact arithmetic", "The requested arithmetic is not exactly representable as decimal; export blocked instead of rounding motion.");
    }
}
