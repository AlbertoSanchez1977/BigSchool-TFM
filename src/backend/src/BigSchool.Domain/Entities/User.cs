using BigSchool.Domain.Enums;

namespace BigSchool.Domain.Entities;

public class User : BaseEntity, IAggregateRoot
{
    public int IdUser { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string PasswordSalt { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public DateTime? LastLoginDate { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<SubCategory> _subCategories = [];
    public IReadOnlyCollection<SubCategory> SubCategories => _subCategories.AsReadOnly();

    protected User() { } // EF Core

    private User(string email, string passwordHash, string passwordSalt,
        string fullName, EntityStatus idStatus, DateTime createdAt)
    {
        Email = email;
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        FullName = fullName;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static User Create(string email, string passwordHash, string passwordSalt, string fullName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(passwordSalt))
            throw new ArgumentException("Password salt is required.", nameof(passwordSalt));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        return new User(
            email.Trim().ToLowerInvariant(),
            passwordHash,
            passwordSalt,
            fullName.Trim(),
            EntityStatus.Active,
            DateTime.UtcNow);
    }

    public void UpdateLastLogin()
    {
        LastLoginDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public SubCategory AddSubCategory(MainCategory mainCategory, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var trimmedName = name.Trim();

        if (_subCategories.Any(s => s.IdMainCategory == mainCategory
            && s.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)
            && s.IdStatus != EntityStatus.Deleted))
        {
            throw new Exceptions.DuplicateSubCategoryDomainException(trimmedName, mainCategory);
        }

        var subCategory = SubCategory.Create(mainCategory, trimmedName);
        _subCategories.Add(subCategory);
        return subCategory;
    }
}
