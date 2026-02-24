using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Avalonia.Threading;

namespace GnuCashUtils.Tagger;

public partial class TaggerScreen : ReactiveUserControl<TaggerScreenViewModel>
{
    public TaggerScreen()
    {
        InitializeComponent();

        var tagBox = this.FindControl<AutoCompleteBox>("TagBox")!;

        // Filter by tag name/value rather than the encoded #[...] string
        tagBox.ItemFilter = (search, item) =>
        {
            if (item is not Tag tag || search == null) return false;
            return tag.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (tag.Value != null && tag.Value.Contains(search, StringComparison.OrdinalIgnoreCase));
        };

        // Mouse-click (or arrow key + Enter) selection from the dropdown
        tagBox.DropDownClosed += (_, _) =>
        {
            if (tagBox.SelectedItem is Tag tag)
            {
                ViewModel!.AddNewTagCommand.Execute(tag).Subscribe();
                tagBox.SelectedItem = null;
                tagBox.Text = "";
                // MinimumPrefixLength=0 causes the AutoCompleteBox to re-open the dropdown
                // synchronously when Text is cleared. Post the close to the next dispatcher
                // tick so it wins after all TextChanged processing completes.
                Dispatcher.UIThread.Post(() => tagBox.IsDropDownOpen = false);
            }
        };

        // Enter key when the dropdown is closed: find existing or create new tag
        tagBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            if (tagBox.IsDropDownOpen) return; // let AutoCompleteBox handle dropdown selection

            var text = tagBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(text)) return;

            var existingTag = ViewModel!.Tags.FirstOrDefault(t =>
                t.Name.Equals(text, StringComparison.OrdinalIgnoreCase)
                || (t.Value != null && $"{t.Name}={t.Value}".Equals(text, StringComparison.OrdinalIgnoreCase)));

            // Qualify to avoid ambiguity with StyledElement.Tag (object?) property
            var tagToAdd = existingTag ?? GnuCashUtils.Tagger.Tag.FromInput(text);
            if (tagToAdd == null) return;

            ViewModel!.AddNewTagCommand.Execute(tagToAdd).Subscribe();
            tagBox.Text = "";
            tagBox.SelectedItem = null;
            e.Handled = true;
        };

        // Sync DataGrid multi-selection to ViewModel.SelectedTransactions
        var grid = this.FindControl<DataGrid>("TransactionsGrid")!;
        grid.SelectionChanged += (_, _) =>
        {
            ViewModel!.SelectedTransactions.Clear();
            foreach (var item in grid.SelectedItems.OfType<TaggedTransaction>())
                ViewModel!.SelectedTransactions.Add(item);
        };
    }
}
