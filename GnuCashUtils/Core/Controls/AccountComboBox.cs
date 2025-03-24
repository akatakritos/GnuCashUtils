using System;
using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using GnuCashUtils.BulkEdit;

namespace GnuCashUtils.Core.Controls;

public class AccountComboBox : ComboBox
{
    protected override Type StyleKeyOverride => typeof(ComboBox);

    private int _lastIndex = 0;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly StringBuilder _searchText = new();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeySymbol is not null)
        {
            if (_sw.ElapsedMilliseconds > 250)
            {
                _searchText.Clear();
            }

            _sw.Restart();
            _searchText.Append(e.KeySymbol);
            var searchText = _searchText.ToString();

            for (var i = 0; i < Items.Count; i++)
            {
                var index = (_lastIndex + i) % Items.Count;
                var account = Items[index] as Account;
                Debug.Assert(account != null);

                if (account.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedIndex = index;
                    _lastIndex = index + 1;
                    break;
                }
            }
        }

        base.OnKeyDown(e);
    }
}