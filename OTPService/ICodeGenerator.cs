namespace HedKam.Services;

public interface ICodeGenerator
{
    string Generate(int digitsCount, bool allowDuplicateDigit, bool allowZero);
}
