using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace GnuCashUtils.Categorization;

public class NaiveBayesianClassifier
{
    class LabelData
    {
        public Dictionary<string, int> WordCounts { get; set; } = new();
        public int TotalWords { get; set; }
        public int DocCount { get; set; }

        public void CountToken(string token)
        {
            WordCounts.TryAdd(token, 0);
            WordCounts[token]++;
        }

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

    public string Predict(string description, decimal amount)
    {
        var tokens = _tokenizer.Tokenize(description, amount).ToList();
        string? bestLabel = null;
        var bestScore = double.NegativeInfinity;

        foreach (var (label, data) in _labels)
        {
            var score = Math.Log(data.DocCount / (double)_totalDocs);
            
            foreach (var token in tokens)
            {
                var wordCount = data.WordCounts.GetValueOrDefault(token, 0);
                score += Math.Log(wordCount + 1 / ((double)data.TotalWords + _vocabulary.Count));
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestLabel = label;
            }
        }
        
        Debug.Assert(bestLabel != null);
        return bestLabel;

    }
    
}

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
        var tokens = SplitRegex().Split(description.ToUpperInvariant());
        
        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].Length <= 2)
                continue;
            
            if (ApplePayRegex().Match(tokens[i]).Success && i < tokens.Length - 1 && tokens[i + 1] == "PAY")
            {
                i++; // skip PAY
                continue;
            }

            if (_skipWords.Contains(tokens[i]))
                continue;
            
            if (_skipRegexes.Any(r => r.Match(tokens[i]).Success))
                continue;

            yield return tokens[i];
        }
    }

    [GeneratedRegex(@"[\s\-*#]+", RegexOptions.Compiled)]
    private static partial Regex SplitRegex();
    
    [GeneratedRegex(@"^[0-9-]+$", RegexOptions.Compiled)]
    private static partial Regex DigitsRegex();
    
    [GeneratedRegex(@"^\w\wAPPLE$")]
    private static partial Regex ApplePayRegex();
}