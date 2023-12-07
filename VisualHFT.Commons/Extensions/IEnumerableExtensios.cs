namespace VisualHFT;

public static class IEnumerableExtensios
{
    public static double Quantile(this IEnumerable<double> sequence, double quantile)
    {
        var elements = sequence.ToArray();
        Array.Sort(elements);
        var realIndex = quantile * (elements.Length - 1);
        var index = (int)realIndex;
        var frac = realIndex - index;
        if (index + 1 < elements.Length)
            return elements[index] * (1 - frac) + elements[index + 1] * frac;
        return elements[index];
    }
}