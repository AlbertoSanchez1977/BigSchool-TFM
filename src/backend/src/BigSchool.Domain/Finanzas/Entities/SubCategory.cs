using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Domain.Finanzas.Entities;

public class SubCategory : BaseEntity, IAggregateRoot
{
    public int IdSubCategory { get; private set; }
    public MainCategory IdMainCategory { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int? IdUser { get; private set; }   // NULL = global predefinida (referencia suave por Id, sin FK dura)
    public bool IsDefault { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public bool IsGlobal => IdUser is null;

    protected SubCategory() { } // EF Core

    private SubCategory(MainCategory mainCategory, string name, int? idUser, bool isDefault, EntityStatus idStatus, DateTime createdAt)
    {
        IdMainCategory = mainCategory;
        Name = name;
        IdUser = idUser;
        IsDefault = isDefault;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static SubCategory Create(MainCategory mainCategory, string name, int? idUser)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        return new SubCategory(mainCategory, name.Trim(), idUser, isDefault: false, EntityStatus.Active, DateTime.UtcNow);
    }

    /// <summary>Solo el propietario puede borrar su subcategoría; nunca una predefinida (IsDefault) ni una global.</summary>
    public void Delete(int requestingUserId)
    {
        if (IsDefault)
            throw new InvalidOperationException("No se puede borrar una subcategoría predefinida.");
        if (IdUser != requestingUserId)
            throw new InvalidOperationException("Solo el propietario puede borrar su subcategoría.");
        IdStatus = EntityStatus.Deleted;
    }
}
