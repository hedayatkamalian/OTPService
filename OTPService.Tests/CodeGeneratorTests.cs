using Shouldly;

namespace HedKam.Services.Tests
{
    public class CodeGeneratorTests
    {
        private const int GENERATE_COUNT = 100;

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        [InlineData(8)]
        public void Codes_Length_Must_Have_True_Length(int codeLength)
        {
            var generator = new CodeGenerator();

            var code = generator.Generate(codeLength, true, true);

            code.Length.ShouldBe(codeLength);
        }

        [Fact]
        public void Codes_Must_Not_Include_Zero()
        {
            var generator = new CodeGenerator();

            for (var i = 0; i < GENERATE_COUNT; i++)
            {
                generator.Generate(8, true, false).ToArray().ShouldNotContain('0');
            }
        }

        [Fact]
        public void Codes_Must_Not_Include_Duplicate_Digits()
        {
            var generator = new CodeGenerator();

            for (var i = 0; i < GENERATE_COUNT; i++)
            {
                var code = generator.Generate(8, false, false);

                code.Distinct().Count().ShouldBe(code.Length);
            }
        }

        [Fact]
        public void Codes_Must_Not_Repeat_When_Generated_Rapidly()
        {
            var generator = new CodeGenerator();

            var codes = new HashSet<string>();
            for (var i = 0; i < GENERATE_COUNT; i++)
            {
                codes.Add(generator.Generate(6, true, true));
            }

            codes.Count.ShouldBeGreaterThan(GENERATE_COUNT * 9 / 10);
        }

        [Fact]
        public void Codes_Must_Not_Repeat_When_Every_Call_Uses_A_New_Generator()
        {
            var codes = new HashSet<string>();
            for (var i = 0; i < GENERATE_COUNT; i++)
            {
                codes.Add(new CodeGenerator().Generate(6, true, true));
            }

            codes.Count.ShouldBeGreaterThan(GENERATE_COUNT * 9 / 10);
        }

        [Fact]
        public void Generator_Must_Throw_When_Unique_Digits_Are_Not_Enough()
        {
            var generator = new CodeGenerator();

            Should.Throw<ArgumentOutOfRangeException>(() => generator.Generate(10, false, false));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(11)]
        public void Generator_Must_Throw_When_Digits_Count_Is_Out_Of_Range(int codeLength)
        {
            var generator = new CodeGenerator();

            Should.Throw<ArgumentOutOfRangeException>(() => generator.Generate(codeLength, true, true));
        }

        [Theory]
        [InlineData(9, false)]
        [InlineData(10, true)]
        public void Codes_Must_Be_Generated_At_The_Unique_Digits_Boundary(int codeLength, bool allowZero)
        {
            var generator = new CodeGenerator();

            var code = generator.Generate(codeLength, false, allowZero);

            code.Length.ShouldBe(codeLength);
            code.Distinct().Count().ShouldBe(codeLength);
        }

        [Fact]
        public void Codes_Must_Be_Generated_When_Duplicates_Exceed_The_Digit_Pool()
        {
            var generator = new CodeGenerator();

            var code = generator.Generate(10, true, false);

            code.Length.ShouldBe(10);
            code.ToArray().ShouldNotContain('0');
        }
    }
}
