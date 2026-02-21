using System;
using Avalonia.Controls;
using Avalonia.ReactiveUI;

namespace GnuCashUtils.Tagger;

public partial class TaggerWindow : ReactiveWindow<TaggerWindowViewModel>
{
    public TaggerWindow()
    {
        InitializeComponent();

        var tagBox = this.FindControl<AutoCompleteBox>("TagBox")!;
        tagBox.DropDownClosed += (_, _) =>
        {
            if (tagBox.SelectedItem is Tag tag)
            {
                ViewModel!.AddTagCommand.Execute(tag).Subscribe();
                tagBox.SelectedItem = null;
                tagBox.Text = "";
                tagBox.IsDropDownOpen = false;
            }
        };
    }
}