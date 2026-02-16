using AwesomeAssertions;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Categorization;

namespace GnuCashUtils.Tests.Categorization;

public class CategorizationRowViewModelTests
{
    [Fact]
    public void ItRequiresAnAccount()
    {
        var vm = new CategorizationRowViewModel(DateOnly.FromDateTime(DateTime.Now), "Test", 100M);
        vm.IsValid.Should().BeFalse();
        vm.StatusIcon.Should().Be("❌");
        
        vm.SelectedAccount = new Account() { FullName = "Test" };
        vm.IsValid.Should().BeTrue();
        vm.StatusIcon.Should().Be("✅");
    }
}