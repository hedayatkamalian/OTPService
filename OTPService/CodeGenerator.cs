using System.Text;

namespace HedKam.Services;

public class CodeGenerator
{
    public string Generate(int digitsCount, bool allowDuplicateDigit, bool allowZero)
    {
        if (digitsCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(digitsCount), "Digits count must be greater than 0");
        }
        if (digitsCount > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(digitsCount), "Digits count must be less than 10");
        }

        var random = new Random(DateTime.Now.Second);
        var digits = new List<int>();

        while (digits.Count < digitsCount)
        {
            var digit = random.Next(allowZero ? 0 : 1, 10);

            if (allowDuplicateDigit)
            {
                digits.Add(digit);
            }
            else
            {
                if (digits.All(p => p != digit))
                {
                    digits.Add(digit);
                }
            }
        }

        var stringBuilder = new StringBuilder();
        foreach (var digit in digits)
        {
            stringBuilder.Append(digit);
        }

        return stringBuilder.ToString();
    }
}
