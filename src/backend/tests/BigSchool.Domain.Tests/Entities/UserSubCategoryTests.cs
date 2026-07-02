using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities;

public class UserSubCategoryTests
{
    [Fact]
    public void AddSubCategory_WithValidData_AddsToCollection()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");

        var sub = user.AddSubCategory(MainCategory.Luxuries, "Conciertos");

        user.SubCategories.Should().HaveCount(1);
        sub.IdMainCategory.Should().Be(MainCategory.Luxuries);
        sub.Name.Should().Be("Conciertos");
        sub.IsDefault.Should().BeFalse();
        sub.IdStatus.Should().Be(EntityStatus.Active);
        sub.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AddSubCategory_DuplicateNameSameCategory_ThrowsDuplicateException()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");
        user.AddSubCategory(MainCategory.Luxuries, "Conciertos");

        var act = () => user.AddSubCategory(MainCategory.Luxuries, "Conciertos");

        act.Should().Throw<DuplicateSubCategoryDomainException>();
    }

    [Fact]
    public void AddSubCategory_SameNameDifferentCategory_Succeeds()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");
        user.AddSubCategory(MainCategory.Luxuries, "Otros");

        var act = () => user.AddSubCategory(MainCategory.EssentialExpenses, "Otros");

        act.Should().NotThrow();
        user.SubCategories.Should().HaveCount(2);
    }

    [Fact]
    public void AddSubCategory_DuplicateNameCaseInsensitive_Throws()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");
        user.AddSubCategory(MainCategory.Luxuries, "Conciertos");

        var act = () => user.AddSubCategory(MainCategory.Luxuries, "CONCIERTOS");

        act.Should().Throw<DuplicateSubCategoryDomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void AddSubCategory_WithEmptyName_ThrowsArgumentException(string? name)
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");

        var act = () => user.AddSubCategory(MainCategory.EssentialExpenses, name!);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void AddSubCategory_TrimsName()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");

        var sub = user.AddSubCategory(MainCategory.Luxuries, "  Conciertos  ");

        sub.Name.Should().Be("Conciertos");
    }
}
