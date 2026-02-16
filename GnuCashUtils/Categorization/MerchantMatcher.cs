using System;
using System.Collections.Generic;
using System.Linq;
using GnuCashUtils.Core;

namespace GnuCashUtils.Categorization;

/// <summary>
/// Compiles a list of MerchantConfig rules and matches rows against them in order,
/// returning the name of the first matching merchant.
/// </summary>
public class MerchantMatcher
{
    private readonly List<(string Name, Func<CategorizationRowViewModel, bool> Predicate)> _rules;

    public MerchantMatcher(IEnumerable<MerchantConfig> configs)
    {
        var parser = new MerchantRuleParser();
        _rules = configs
            .Where(c => !string.IsNullOrWhiteSpace(c.Match))
            .Select(c => (c.Name, parser.Parse(c.Match).Compile()))
            .ToList();
    }

    /// <summary>Returns the name of the first matching merchant rule, or null if none match.</summary>
    public string? Match(CategorizationRowViewModel row)
    {
        foreach (var (name, predicate) in _rules)
        {
            if (predicate(row)) return name;
        }
        return null;
    }
}
