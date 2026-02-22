using AwesomeAssertions;
using GnuCashUtils.Core;
using GnuCashUtils.Tagger;

namespace GnuCashUtils.Tests.Tagger;

public class TagOperationTests
{
    private static readonly Account SampleAccount = new() { Guid = "acc-1", Name = "Checking", FullName = "Assets:Checking" };
    private static readonly Tag FoodTag = new("food");
    private static readonly Tag TravelTag = new("travel");

    private static TaggedTransaction MakeTxn() => new() { Account = SampleAccount };

    [Fact]
    public void Add_TagNotPresent_AddsTag()
    {
        var txn = MakeTxn();
        var op = new TagOperation { Tag = FoodTag, Operation = OperationType.Add };

        op.Apply(txn);

        txn.Tags.Should().ContainSingle(t => t == FoodTag);
    }

    [Fact]
    public void Add_TagAlreadyPresent_NoDuplicate()
    {
        var txn = MakeTxn();
        txn.Tags.Add(FoodTag);
        txn.IsDirty = false;
        var op = new TagOperation { Tag = FoodTag, Operation = OperationType.Add };

        op.Apply(txn);

        txn.Tags.Should().ContainSingle(t => t == FoodTag);
    }

    [Fact]
    public void Delete_TagPresent_RemovesTag()
    {
        var txn = MakeTxn();
        txn.Tags.Add(FoodTag);
        txn.Tags.Add(TravelTag);
        txn.IsDirty = false;
        var op = new TagOperation { Tag = FoodTag, Operation = OperationType.Delete };

        op.Apply(txn);

        txn.Tags.Should().ContainSingle(t => t == TravelTag);
    }

    [Fact]
    public void Delete_TagNotPresent_NoOp()
    {
        var txn = MakeTxn();
        txn.Tags.Add(TravelTag);
        txn.IsDirty = false;
        var op = new TagOperation { Tag = FoodTag, Operation = OperationType.Delete };

        op.Apply(txn);

        txn.Tags.Should().ContainSingle(t => t == TravelTag);
        txn.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void None_TagPresent_TagsUnchanged()
    {
        var txn = MakeTxn();
        txn.Tags.Add(FoodTag);
        txn.IsDirty = false;
        var op = new TagOperation { Tag = FoodTag, Operation = OperationType.None };

        op.Apply(txn);

        txn.Tags.Should().ContainSingle(t => t == FoodTag);
        txn.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void None_TagNotPresent_TagsUnchanged()
    {
        var txn = MakeTxn();
        var op = new TagOperation { Tag = FoodTag, Operation = OperationType.None };

        op.Apply(txn);

        txn.Tags.Should().BeEmpty();
        txn.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Add_TagNotPresent_MarksTransactionDirty()
    {
        var txn = MakeTxn();
        txn.IsDirty = false;
        var op = new TagOperation { Tag = FoodTag, Operation = OperationType.Add };

        op.Apply(txn);

        txn.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Delete_TagPresent_MarksTransactionDirty()
    {
        var txn = MakeTxn();
        txn.Tags.Add(FoodTag);
        txn.IsDirty = false;
        var op = new TagOperation { Tag = FoodTag, Operation = OperationType.Delete };

        op.Apply(txn);

        txn.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void None_DoesNotMarkTransactionDirty()
    {
        var txn = MakeTxn();
        txn.IsDirty = false;
        var op = new TagOperation { Tag = FoodTag, Operation = OperationType.None };

        op.Apply(txn);

        txn.IsDirty.Should().BeFalse();
    }
}
