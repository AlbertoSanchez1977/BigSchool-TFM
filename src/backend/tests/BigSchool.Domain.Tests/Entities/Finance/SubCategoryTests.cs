using BigSchool.Domain.Finance.Entities;
using BigSchool.Domain.Finance.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities.Finance;

public class SubCategoryTests
{
    [Fact]
    public void Create_WithIdUser_SetsIdUserAndIsNotGlobalOrDefault()
    {
        var s = SubCategory.Create(MainCategory.Luxuries, " Cine ", idUser: 7);
        s.IdUser.Should().Be(7);
        s.Name.Should().Be("Cine");
        s.IsGlobal.Should().BeFalse();
        s.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Create_NullIdUser_IsGlobal()
        => SubCategory.Create(MainCategory.Salary, "Nómina", null).IsGlobal.Should().BeTrue();

    [Fact]
    public void Create_EmptyName_ThrowsArgumentException()
        => FluentActions.Invoking(() => SubCategory.Create(MainCategory.Other, " ", 1)).Should().Throw<System.ArgumentException>();

    [Fact]
    public void Delete_ByOwner_SoftDeletes()
    {
        var s = SubCategory.Create(MainCategory.Other, "X", idUser: 1);
        s.Delete(requestingUserId: 1);
        s.IdStatus.Should().Be(EntityStatus.Deleted);
    }

    [Fact]
    public void Delete_ByOtherUser_ThrowsInvalidOperationException()
    {
        var s = SubCategory.Create(MainCategory.Other, "X", idUser: 1);
        FluentActions.Invoking(() => s.Delete(requestingUserId: 2)).Should().Throw<System.InvalidOperationException>();
    }

    [Fact]
    public void Delete_GlobalSubCategory_ThrowsInvalidOperationException()
    {
        var s = SubCategory.Create(MainCategory.Other, "X", idUser: null); // global
        FluentActions.Invoking(() => s.Delete(requestingUserId: 1)).Should().Throw<System.InvalidOperationException>();
    }
}
