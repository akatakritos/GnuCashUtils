using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GnuCashUtils.Categorization;

public class NaiveBayesianClassifier
{
    private class LabelData
    {
        public Dictionary<string, int> WordCounts { get; set; } = new();
        public int TotalWords { get; set; }
        public int DocCount { get; set; }

        public void CountToken(string token)
        {
            WordCounts.TryAdd(token, 0);
            WordCounts[token]++;
        }

        public override string ToString() => $"LabelData(DocCount={DocCount}, WordCounts={WordCounts})";
    }
    
    private readonly ITokenizer _tokenizer;
    private readonly HashSet<string> _vocabulary = new();
    private readonly Dictionary<string, LabelData> _labels = new();
    private int _totalDocs = 0;

    public NaiveBayesianClassifier(ITokenizer tokenizer)
    {
        _tokenizer = tokenizer;
    }

    public void Train(string description, decimal amount, string account)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(account);
        
        if (!_labels.ContainsKey(account))
        {
            _labels.Add(account, new());
        }
        
        var label = _labels[account];
        label.DocCount++;
        _totalDocs++;

        foreach (var token in _tokenizer.Tokenize(description, amount))
        {
            label.CountToken(token);
            label.TotalWords++;
            _vocabulary.Add(token);
        }
    }

    public Prediction Predict(string description, decimal amount)
    {
        var tokens = _tokenizer.Tokenize(description, amount).ToList();
        var scores = new Dictionary<string, double>(_labels.Count);

        foreach (var (label, data) in _labels)
        {
            var score = Math.Log(data.DocCount / (double)_totalDocs);

            foreach (var token in tokens)
            {
                var wordCount = data.WordCounts.GetValueOrDefault(token, 0);
                score += Math.Log((wordCount + 1) / ((double)data.TotalWords + _vocabulary.Count));
            }

            scores[label] = score;
        }

        var best = scores.MaxBy(kvp => kvp.Value);
        var maxScore = best.Value;

        // Softmax with numerical stability (subtract max to avoid overflow)
        var sumExp = scores.Values.Sum(s => Math.Exp(s - maxScore));
        var confidence = 1.0 / sumExp; // exp(maxScore - maxScore) / sumExp

        return new Prediction(best.Key, confidence);
    }
    
}

public record Prediction(string Label, double Confidence);

public interface ITokenizer
{
    public IEnumerable<string> Tokenize(string description, decimal amount);
}

public partial class Tokenizer : ITokenizer
{
    private static HashSet<string> _skipWords = ["ENDING", "TST"];
    private static Regex[] _skipRegexes = [
        DigitsRegex(),
    ];
    
    public IEnumerable<string> Tokenize(string description, decimal amount)
    {
        var tokens = new TokenIterator(SplitRegex().Split(description.ToUpperInvariant()));
        do
        {
            if (tokens.Current.Length <= 2)
                continue;

            if (ApplePayRegex().Match(tokens.Current).Success && tokens.Peek() == "PAY")
            {
                tokens.MoveNext();
                continue;
            }

            if (tokens.Current == "BLUE" && tokens.Peek() == "SPRINGS")
                continue;
            

            if (_skipWords.Contains(tokens.Current))
                continue;

            if (_skipRegexes.Any(r => r.Match(tokens.Current).Success))
                continue;

            yield return tokens.Current;
        } while (tokens.MoveNext());
    }

    class TokenIterator
    {
        private readonly string[] _tokens;
        public int Index { get; private set; } = 0;
        public string Current => _tokens[Index];
        public string? Peek() => Index + 1 < _tokens.Length ? _tokens[Index + 1] : null;
        public TokenIterator(string[] tokens)
        {
            _tokens = tokens;
        }
        
        public bool MoveNext() => ++Index < _tokens.Length;
        
    }

    [GeneratedRegex(@"[\s\-*#]+", RegexOptions.Compiled)]
    private static partial Regex SplitRegex();
    
    [GeneratedRegex(@"^[0-9-]+$", RegexOptions.Compiled)]
    private static partial Regex DigitsRegex();
    
    [GeneratedRegex(@"^\w\wAPPLE$")]
    private static partial Regex ApplePayRegex();
}