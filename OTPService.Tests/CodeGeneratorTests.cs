using Shouldly;

namespace HedKam.Services.Tests
{
    public class CodeGeneratorTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        [InlineData(8)]
        public void Codes_Lenght_Must_Have_True_Lenght(int codeLenght)
        {
            var generator = new CodeGenerator();

            var code = generator.Generate(codeLenght, true, true);

            code.Length.ShouldBe(codeLenght);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(8)]
        [InlineData(8)]
        [InlineData(8)]
        [InlineData(8)]
        public void Codes_Must_Not_Include_Zero(int codeLenght)
        {
            var generator = new CodeGenerator();

            var code = generator.Generate(codeLenght, true, false);

            code.ToArray().ShouldNotContain('0');
        }

        [Theory]
        [InlineData(8)]
        [InlineData(8)]
        [InlineData(8)]
        [InlineData(8)]
        [InlineData(8)]
        public void Codes_Must_Not_Include_Duplicate_Digits(int codeLenght)
        {
            var generator = new CodeGenerator();

            var code = generator.Generate(codeLenght, false, false);

            code.ToArray().Length.ShouldBe(code.ToArray().Distinct().Count());
        }
    }
}