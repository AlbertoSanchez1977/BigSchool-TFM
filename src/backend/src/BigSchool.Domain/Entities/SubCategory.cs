using BigSchool.Domain.Enums;

namespace BigSchool.Domain.Entities;

public class SubCategory : BaseEntity
{
    public int IdSubCategory { get; private set; }
    public MainCategory IdMainCategory { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SubCategory() { } // EF Core

    internal static SubCategory Create(MainCategory mainCategory, string name)
    {
        return new SubCategory
        {
            IdMainCategory = mainCategory,
            Name = name,
            IsDefault = false,
            IdStatus = EntityStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }
}
