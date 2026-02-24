using System.Collections.Generic;

namespace GnuCashUtils.Core;

public class AppConfig
{
    public string Database { get; set; } = "";
    public List<BankConfig> Banks { get; set; } = [];
}

public class BankConfig
{
    public string Name { get; set; } = "";
    public string Match { get; set; } = "";
    public int Skip { get; set; }
    public string Headers { get; set; } = "";
    public string Account { get; set; } = "";
    public SignConvention SignConvention { get; set; }
}

/// <summary>
/// Indicates how to understand transaction amounts in the context of the bank account
/// </summary>
public enum SignConvention
{
    /// <summary>
    /// This bank's CSV export treats positive numbers as debits
    /// </summary>
    Debit,
    
    /// <summary>
    /// This bank's CSV export treats positive numbers as credits
    /// </summary>
    Credit
}
