using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using GnuCashUtils.BulkEdit;
using ReactiveUI;

namespace GnuCashUtils.Core.Controls;

public class AccountComboBox : ComboBox
{
    private int _lastIndex = 0;
    private readonly Subject<string> _searches = new();
    
    protected override Type StyleKeyOverride => typeof(ComboBox);

    public AccountComboBox() : base()
    {
        _searches
            .Where(s =>
            {
                Console.WriteLine((string?)s);
                return true;
            })
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(chars =>
            {
                var searchText = new string(chars.ToArray());
                Console.WriteLine("Search for " + searchText);

                for (int i = 0; i < Items.Count; i++)
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

            });

    }

    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly StringBuilder _searchText = new();
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeySymbol is not null)
        {
            Console.WriteLine(_sw.ElapsedMilliseconds);
            if (_sw.ElapsedMilliseconds > 250)
            {
                _searchText.Clear();
            }

            _sw.Restart();
            _searchText.Append(e.KeySymbol);
            _searches.OnNext(_searchText.ToString());
        }
        
        base.OnKeyDown(e);
    }
    
}