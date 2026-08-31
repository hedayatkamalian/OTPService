using System.Security.Cryptography;
using System.Text;

namespace HedKam.Services;

public class CodeGenerator : ICodeGenerator
{
    public string Generate(int digitsCount, bool allowDuplicateDigit, bool allowZero)
    {
        if (digitsCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(digitsCount), "Digits count must be between 1 and 10");
        }
        if (digitsCount > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(digitsCount), "Digits count must be between 1 and 10");
        }

        var firstDigit = allowZero ? 0 : 1;
        var availableDigitsCount = 10 - firstDigit;

        if (!allowDuplicateDigit && digitsCount > availableDigitsCount)
        {
            throw new ArgumentOutOfRangeException(nameof(digitsCount), $"Digits count must not be greater than {availableDigitsCount} when duplicate digits are not allowed");
        }

        var digits = new List<int>();

        if (allowDuplicateDigit)
        {
            while (digits.Count < digitsCount)
            {
                digits.Add(RandomNumberGenerator.GetInt32(firstDigit, 10));
            }
        }
        else
        {
            var availableDigits = Enumerable.Range(firstDigit, availableDigitsCount).ToList();

            while (digits.Count < digitsCount)
            {
                var index = RandomNumberGenerator.GetInt32(0, availableDigits.Count);

                digits.Add(availableDigits[index]);
                availableDigits.RemoveAt(index);
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
