using System.Collections.Generic;

namespace GnuCashUtils.Core;

public class AppConfig
{
    public string Database { get; set; } = "";
    public List<BankConfig> Banks { get; set; } = [];
    public List<MerchantConfig> Merchants { get; set; } = [];
}

public class BankConfig
{
    public string Name { get; set; } = "";
    public string Match { get; set; } = "";
    public int Skip { get; set; }
    public string Headers { get; set; } = "";
}

public class MerchantConfig
{
    public string Name { get; set; } = "";
    public string Match { get; set; } = "";
}
