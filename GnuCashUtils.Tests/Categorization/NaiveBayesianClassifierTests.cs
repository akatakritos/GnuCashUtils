using AwesomeAssertions;
using GnuCashUtils.Categorization;

namespace GnuCashUtils.Tests.Categorization;

public class NaiveBayesianClassifierTests
{
    private readonly NaiveBayesianClassifier _classifier = new(new Tokenizer());

    [Fact]
    public void SpamHamTest()
    {
        _classifier.Train("buy cheap viagra now free money", 0, "spam");
        _classifier.Train("win a prize click here limited offer", 0, "spam");
        _classifier.Train("meeting tomorrow at 3pm let me know", 0, "ham");
        _classifier.Train("can you send me the quarterly report", 0, "ham");

        var spam = _classifier.Predict("free money limited offer", 0);
        spam.Label.Should().Be("spam");
        spam.Confidence.Should().BeInRange(0.0, 1.0);

        var ham = _classifier.Predict("send me the report by friday", 0);
        ham.Label.Should().Be("ham");
        ham.Confidence.Should().BeInRange(0.0, 1.0);
    }
}

public class TokenizerTests
{
    private static readonly Tokenizer Tokenizer = new();

    [Theory]
    [InlineData("CF* CRUMBL INDEPENDENC 8014101313 UTAPPLE PAY ENDING IN 7495", new[] { "CRUMBL", "INDEPENDENC" })]
    [InlineData("SONIC DRIVE IN #1694 8162242212 MOAPPLE PAY ENDING IN 1622", new[] { "SONIC", "DRIVE" })]
    [InlineData("SEPHORA.COM 877-SEPHORA CA69255921544", new[] { "SEPHORA.COM", "SEPHORA", "CA69255921544" })]
    [InlineData("AMZN MKTP US*R01RG9QZ1 AMZN.COM/BILLWA5HHN55BR09A", new[]{"AMZN", "MKTP", "R01RG9QZ1", "AMZN.COM/BILLWA5HHN55BR09A"})]
    [InlineData("APPLE.COM/BILL 111-111-1111 CAAPPLE PAY ENDING IN 1622", new[]{"APPLE.COM/BILL"})]
    public void TokenizerTest(string input, string[] tokens)
    {
        var actualTokens = Tokenizer.Tokenize(input, 0);
        actualTokens.Should().BeEquivalentTo(tokens);
    }
}