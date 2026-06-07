# Backend Scaffolding — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the .NET 8 backend solution with Clean Architecture (4 layers), configured with Autofac DI (simplified), Swagger, Health Check, Serilog, CustomMediatR (sequential events), base domain types (BaseEntity, IAggregateRoot, IRepository<T,Y>, Enums, IDomainEvent, IUnitOfWork), Infrastructure base (EFRepository, DbContext, DbConnectionMySqlFactory) — runnable with `dotnet run`.

**Architecture:** Clean Architecture with 4 source projects (Domain → Application → Infrastructure → WebApi) and 3 test projects. Domain has zero external NuGet dependencies and contains: Entities, Enums, Events (IDomainEvent + concretes), IAggregateRoot marker, IRepository<T,Y> generic interface, IUnitOfWork. Application depends on Domain + MediatR/AutoMapper/FluentValidation/Dapper and contains specific repository interfaces (IUserRepository : IRepository<User, int>), Commands, Queries, DTOs, service interfaces. Infrastructure implements persistence (EF Core + MySQL via Pomelo, Dapper via DbConnectionMySqlFactory). WebApi is the composition root — Autofac registration simplified to one `RegisterAssemblyTypes` per layer assembly.

**Tech Stack:** .NET 8, C#, Autofac, MediatR (CustomMediatR with sequential dispatch), AutoMapper, FluentValidation, EF Core + Pomelo MySQL, Dapper, Swashbuckle (Swagger), Serilog (file sink), xUnit + FluentAssertions + Moq

---

## Key Architectural Decisions (changes from initial design doc)

1. **IUnitOfWork, IAggregateRoot, IDomainEvent** → in **Domain** layer (no MediatR dependency in Domain)
2. **IRepository<T,Y>** → in **Application/Interfaces/** (junto con repos específicos)
3. **IAggregateRoot** is a marker interface. Only Aggregate Roots get repositories: `User`, `Transaction`, `RagDocument`, `Portfolio`, `Holding`
3. **IRepository<T, Y>** generic base with `AddAsync`, `AddRangeAsync`, `GetByIdAsync`, `IUnitOfWork UnitOfWork { get; }`
4. **EFRepository<T, Y>** abstract class in Infrastructure implements the generic base
5. **Specific repos** (e.g. `IUserRepository : IRepository<User, int>`) live in **Application/Interfaces/**
6. **Autofac simplified**: No scattered modules. Single registration point in WebApi `Program.cs` using `RegisterAssemblyTypes(assembly).AsImplementedInterfaces()` per layer
7. **CustomMediatR**: Sequential event dispatch by default (`SyncContinueOnException`)
8. **IUnitOfWork.SaveChangesAsync** has `dispatchEvents = true` parameter (false for test seeding)
9. **DbConnectionMySqlFactory** with `IOptions<AppSettings>` instead of raw IConfiguration
10. **Serilog** for structured logging to file
11. **No appsettings.Development.json** — use `dotnet user-secrets` for local overrides
12. **Directory.Build.props** for centralized NuGet version management

---

## File Structure

```
src/backend/
├── Backend.sln
├── Directory.Build.props
├── .editorconfig
├── src/
│   ├── BigSchool.Domain/
│   │   ├── BigSchool.Domain.csproj
│   │   ├── Entities/
│   │   │   ├── BaseEntity.cs
│   │   │   └── IAggregateRoot.cs
│   │   ├── Enums/
│   │   │   ├── EntityStatus.cs
│   │   │   ├── TransactionType.cs
│   │   │   ├── MainCategory.cs
│   │   │   └── RecurrencePeriod.cs
│   │   ├── Events/
│   │   │   └── IDomainEvent.cs
│   │   └── Interfaces/
│   │       └── IUnitOfWork.cs
│   │
│   ├── BigSchool.Application/
│   │   ├── BigSchool.Application.csproj
│   │   ├── Interfaces/
│   │   │   ├── IRepository.cs
│   │   │   ├── Repositories/
│   │   │   │   ├── IUserRepository.cs
│   │   │   │   ├── ITransactionRepository.cs
│   │   │   │   ├── IPortfolioRepository.cs
│   │   │   │   ├── IHoldingRepository.cs
│   │   │   │   └── IRagDocumentRepository.cs
│   │   │   ├── Services/
│   │   │   │   └── IRagServiceClient.cs
│   │   │   └── IDbConnectionFactory.cs
│   │   ├── Events/
│   │   │   └── DomainEventNotification.cs
│   │   └── Configuration/
│   │       └── AppSettings.cs
│   │
│   ├── BigSchool.Infrastructure/
│   │   ├── BigSchool.Infrastructure.csproj
│   │   └── Persistence/
│   │       ├── BigSchoolDbContext.cs
│   │       ├── DbConnectionMySqlFactory.cs
│   │       └── Repositories/
│   │           └── EFRepository.cs
│   │
│   └── BigSchool.WebApi/
│       ├── BigSchool.WebApi.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       └── Infrastructure/
│           └── CustomMediatR.cs
│
└── tests/
    ├── BigSchool.Domain.Tests/
    │   ├── BigSchool.Domain.Tests.csproj
    │   └── Entities/
    │       └── BaseEntityTests.cs
    ├── BigSchool.Application.Tests/
    │   └── BigSchool.Application.Tests.csproj
    └── BigSchool.Integration.Tests/
        └── BigSchool.Integration.Tests.csproj
```

---

### Task 1: Create Solution, Projects and References

**Files:**
- Create: `src/backend/Backend.sln`
- Create: `src/backend/src/BigSchool.Domain/BigSchool.Domain.csproj`
- Create: `src/backend/src/BigSchool.Application/BigSchool.Application.csproj`
- Create: `src/backend/src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj`
- Create: `src/backend/src/BigSchool.WebApi/BigSchool.WebApi.csproj`
- Create: `src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj`
- Create: `src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj`
- Create: `src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj`

- [x] **Step 1: Create solution and source projects**

```powershell
cd C:\SourceCode\BigSchool-TFM\src\backend
dotnet new sln -n Backend
dotnet new classlib -n BigSchool.Domain -o src/BigSchool.Domain --framework net8.0
dotnet new classlib -n BigSchool.Application -o src/BigSchool.Application --framework net8.0
dotnet new classlib -n BigSchool.Infrastructure -o src/BigSchool.Infrastructure --framework net8.0
dotnet new webapi -n BigSchool.WebApi -o src/BigSchool.WebApi --framework net8.0 --no-openapi
```

- [x] **Step 2: Create test projects**

```powershell
cd C:\SourceCode\BigSchool-TFM\src\backend
dotnet new xunit -n BigSchool.Domain.Tests -o tests/BigSchool.Domain.Tests --framework net8.0
dotnet new xunit -n BigSchool.Application.Tests -o tests/BigSchool.Application.Tests --framework net8.0
dotnet new xunit -n BigSchool.Integration.Tests -o tests/BigSchool.Integration.Tests --framework net8.0
```

- [x] **Step 3: Add all projects to solution**

```powershell
cd C:\SourceCode\BigSchool-TFM\src\backend
dotnet sln add src/BigSchool.Domain/BigSchool.Domain.csproj
dotnet sln add src/BigSchool.Application/BigSchool.Application.csproj
dotnet sln add src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj
dotnet sln add src/BigSchool.WebApi/BigSchool.WebApi.csproj
dotnet sln add tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj
dotnet sln add tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj
dotnet sln add tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj
```

- [x] **Step 4: Add project references (Clean Architecture dependency rules)**

```powershell
cd C:\SourceCode\BigSchool-TFM\src\backend

# Application depends on Domain
dotnet add src/BigSchool.Application/BigSchool.Application.csproj reference src/BigSchool.Domain/BigSchool.Domain.csproj

# Infrastructure depends on Domain + Application
dotnet add src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj reference src/BigSchool.Domain/BigSchool.Domain.csproj
dotnet add src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj reference src/BigSchool.Application/BigSchool.Application.csproj

# WebApi depends on all (composition root)
dotnet add src/BigSchool.WebApi/BigSchool.WebApi.csproj reference src/BigSchool.Application/BigSchool.Application.csproj
dotnet add src/BigSchool.WebApi/BigSchool.WebApi.csproj reference src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj

# Test projects
dotnet add tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj reference src/BigSchool.Domain/BigSchool.Domain.csproj
dotnet add tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj reference src/BigSchool.Application/BigSchool.Application.csproj
dotnet add tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj reference src/BigSchool.Domain/BigSchool.Domain.csproj
dotnet add tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj reference src/BigSchool.WebApi/BigSchool.WebApi.csproj
dotnet add tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj reference src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj
```

- [x] **Step 5: Delete auto-generated boilerplate files**

```powershell
cd C:\SourceCode\BigSchool-TFM\src\backend
Remove-Item src/BigSchool.Domain/Class1.cs -ErrorAction SilentlyContinue
Remove-Item src/BigSchool.Application/Class1.cs -ErrorAction SilentlyContinue
Remove-Item src/BigSchool.Infrastructure/Class1.cs -ErrorAction SilentlyContinue
Remove-Item src/BigSchool.WebApi/Controllers -Recurse -ErrorAction SilentlyContinue
Remove-Item src/BigSchool.WebApi/WeatherForecast.cs -ErrorAction SilentlyContinue
Remove-Item tests/BigSchool.Domain.Tests/UnitTest1.cs -ErrorAction SilentlyContinue
Remove-Item tests/BigSchool.Application.Tests/UnitTest1.cs -ErrorAction SilentlyContinue
Remove-Item tests/BigSchool.Integration.Tests/UnitTest1.cs -ErrorAction SilentlyContinue
```

- [x] **Step 6: Verify solution builds (will have warnings about empty projects, that's fine)**

Run: `cd C:\SourceCode\BigSchool-TFM\src\backend && dotnet build Backend.sln`
Expected: Build succeeded.

- [x] **Step 7: Commit**

```powershell
cd C:\SourceCode\BigSchool-TFM
git add src/backend/
git commit -m "feat: crear solución .NET 8 con proyectos Clean Architecture"
```

---

### Task 2: Directory.Build.props and NuGet Packages

**Files:**
- Create: `src/backend/Directory.Build.props`
- Modify: `src/backend/src/BigSchool.Application/BigSchool.Application.csproj`
- Modify: `src/backend/src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj`
- Modify: `src/backend/src/BigSchool.WebApi/BigSchool.WebApi.csproj`
- Modify: `src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj`
- Modify: `src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj`
- Modify: `src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj`

- [x] **Step 1: Create Directory.Build.props with centralized versions**

Create file `src/backend/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>

  <!-- Centralized NuGet package versions -->
  <PropertyGroup>
    <MediatRVersion>12.4.1</MediatRVersion>
    <AutoMapperVersion>15.1.3</AutoMapperVersion>
    <FluentValidationVersion>11.11.0</FluentValidationVersion>
    <DapperVersion>2.1.35</DapperVersion>
    <EFCoreVersion>8.0.11</EFCoreVersion>
    <PomeloVersion>8.0.2</PomeloVersion>
    <AutofacVersion>8.1.1</AutofacVersion>
    <AutofacDIVersion>9.0.0</AutofacDIVersion>
    <Argon2Version>2.0.0</Argon2Version>
    <SwashbuckleVersion>6.9.0</SwashbuckleVersion>
    <JwtBearerVersion>8.0.11</JwtBearerVersion>
    <SerilogVersion>4.1.0</SerilogVersion>
    <SerilogSinksFileVersion>6.0.0</SerilogSinksFileVersion>
    <SerilogAspNetCoreVersion>8.0.3</SerilogAspNetCoreVersion>
    <!-- Test packages -->
    <xUnitVersion>2.9.2</xUnitVersion>
    <FluentAssertionsVersion>6.12.2</FluentAssertionsVersion>
    <MoqVersion>4.20.72</MoqVersion>
    <TestcontainersVersion>3.10.0</TestcontainersVersion>
    <MvcTestingVersion>8.0.11</MvcTestingVersion>
  </PropertyGroup>
</Project>
```

- [x] **Step 2: Set BigSchool.Domain.csproj (no external NuGet dependencies)**

Replace content of `src/backend/src/BigSchool.Domain/BigSchool.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

Note: TargetFramework, ImplicitUsings, Nullable inherited from Directory.Build.props.

- [x] **Step 3: Set BigSchool.Application.csproj**

Replace content of `src/backend/src/BigSchool.Application/BigSchool.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="MediatR" Version="$(MediatRVersion)" />
    <PackageReference Include="AutoMapper" Version="$(AutoMapperVersion)" />
    <PackageReference Include="FluentValidation" Version="$(FluentValidationVersion)" />
    <PackageReference Include="Dapper" Version="$(DapperVersion)" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BigSchool.Domain\BigSchool.Domain.csproj" />
  </ItemGroup>

</Project>
```

- [x] **Step 4: Set BigSchool.Infrastructure.csproj**

Replace content of `src/backend/src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="$(EFCoreVersion)" />
    <PackageReference Include="Pomelo.EntityFrameworkCore.MySql" Version="$(PomeloVersion)" />
    <PackageReference Include="Dapper" Version="$(DapperVersion)" />
    <PackageReference Include="MediatR" Version="$(MediatRVersion)" />
    <PackageReference Include="Isopoh.Cryptography.Argon2" Version="$(Argon2Version)" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="$(OptionsVersion)" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="$(OptionsVersion)" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BigSchool.Domain\BigSchool.Domain.csproj" />
    <ProjectReference Include="..\BigSchool.Application\BigSchool.Application.csproj" />
  </ItemGroup>

</Project>
```

- [x] **Step 5: Set BigSchool.WebApi.csproj**

Replace content of `src/backend/src/BigSchool.WebApi/BigSchool.WebApi.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <PackageReference Include="Autofac" Version="$(AutofacVersion)" />
    <PackageReference Include="Autofac.Extensions.DependencyInjection" Version="$(AutofacDIVersion)" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="$(SwashbuckleVersion)" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="$(JwtBearerVersion)" />
    <PackageReference Include="Serilog.AspNetCore" Version="$(SerilogAspNetCoreVersion)" />
    <PackageReference Include="Serilog.Sinks.File" Version="$(SerilogSinksFileVersion)" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BigSchool.Application\BigSchool.Application.csproj" />
    <ProjectReference Include="..\BigSchool.Infrastructure\BigSchool.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

- [x] **Step 6: Set test project csproj files**

Replace `src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="$(xUnitVersion)" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="$(FluentAssertionsVersion)" />
    <PackageReference Include="Moq" Version="$(MoqVersion)" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\BigSchool.Domain\BigSchool.Domain.csproj" />
  </ItemGroup>

</Project>
```

Replace `src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="$(xUnitVersion)" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="$(FluentAssertionsVersion)" />
    <PackageReference Include="Moq" Version="$(MoqVersion)" />
    <PackageReference Include="MediatR" Version="$(MediatRVersion)" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\BigSchool.Application\BigSchool.Application.csproj" />
    <ProjectReference Include="..\..\src\BigSchool.Domain\BigSchool.Domain.csproj" />
  </ItemGroup>

</Project>
```

Replace `src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="$(xUnitVersion)" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="$(FluentAssertionsVersion)" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="$(MvcTestingVersion)" />
    <PackageReference Include="Testcontainers" Version="$(TestcontainersVersion)" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\BigSchool.WebApi\BigSchool.WebApi.csproj" />
    <ProjectReference Include="..\..\src\BigSchool.Infrastructure\BigSchool.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

- [x] **Step 7: Restore and verify build**

```powershell
cd C:\SourceCode\BigSchool-TFM\src\backend
dotnet restore Backend.sln
dotnet build Backend.sln
```

Expected: Build succeeded.

- [x] **Step 8: Commit**

```powershell
cd C:\SourceCode\BigSchool-TFM
git add src/backend/
git commit -m "feat: añadir Directory.Build.props con versiones centralizadas y configurar paquetes NuGet"
```

---

### Task 3: Domain Layer — BaseEntity, IAggregateRoot, IUnitOfWork, Enums, IDomainEvent

**Files:**
- Create: `src/backend/src/BigSchool.Domain/Entities/BaseEntity.cs`
- Create: `src/backend/src/BigSchool.Domain/Entities/IAggregateRoot.cs`
- Create: `src/backend/src/BigSchool.Domain/Interfaces/IUnitOfWork.cs`
- Create: `src/backend/src/BigSchool.Domain/Enums/EntityStatus.cs`
- Create: `src/backend/src/BigSchool.Domain/Enums/TransactionType.cs`
- Create: `src/backend/src/BigSchool.Domain/Enums/MainCategory.cs`
- Create: `src/backend/src/BigSchool.Domain/Enums/RecurrencePeriod.cs`
- Create: `src/backend/src/BigSchool.Domain/Events/IDomainEvent.cs`
- Test: `src/backend/tests/BigSchool.Domain.Tests/Entities/BaseEntityTests.cs`

- [x] **Step 1: Write tests for BaseEntity domain event behavior**

Create file `src/backend/tests/BigSchool.Domain.Tests/Entities/BaseEntityTests.cs`:

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Events;
using FluentAssertions;

namespace BigSchool.Domain.Tests.Entities;

public class BaseEntityTests
{
    private class TestEntity : BaseEntity, IAggregateRoot
    {
    }

    private class TestEvent : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    [Fact]
    public void RaiseDomainEvent_ShouldAddEventToCollection()
    {
        var entity = new TestEntity();
        var domainEvent = new TestEvent();

        entity.RaiseDomainEvent(domainEvent);

        entity.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(domainEvent);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        var entity = new TestEntity();
        entity.RaiseDomainEvent(new TestEvent());
        entity.RaiseDomainEvent(new TestEvent());

        entity.ClearDomainEvents();

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void NewEntity_ShouldHaveNoDomainEvents()
    {
        var entity = new TestEntity();

        entity.DomainEvents.Should().BeEmpty();
    }
}
```

- [x] **Step 2: Run tests to verify they fail**

Run: `cd C:\SourceCode\BigSchool-TFM\src\backend && dotnet test tests/BigSchool.Domain.Tests -v minimal`
Expected: FAIL — types `BaseEntity`, `IAggregateRoot`, `IDomainEvent` not found.

- [x] **Step 3: Implement IDomainEvent**

Create file `src/backend/src/BigSchool.Domain/Events/IDomainEvent.cs`:

```csharp
namespace BigSchool.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
```

- [x] **Step 4: Implement BaseEntity**

Create file `src/backend/src/BigSchool.Domain/Entities/BaseEntity.cs`:

```csharp
using BigSchool.Domain.Events;

namespace BigSchool.Domain.Entities;

public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

- [x] **Step 5: Implement IAggregateRoot marker interface**

Create file `src/backend/src/BigSchool.Domain/Entities/IAggregateRoot.cs`:

```csharp
namespace BigSchool.Domain.Entities;

/// <summary>
/// Marcador para Aggregate Roots. Solo las entidades que implementen esta interfaz
/// pueden tener un IRepository asociado.
/// </summary>
public interface IAggregateRoot
{
}
```

- [x] **Step 6: Implement IUnitOfWork**

Create file `src/backend/src/BigSchool.Domain/Interfaces/IUnitOfWork.cs`:

```csharp
namespace BigSchool.Domain.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(bool dispatchEvents = true, CancellationToken cancellationToken = default);
}
```

- [x] **Step 7: Implement Enums**

Create file `src/backend/src/BigSchool.Domain/Enums/EntityStatus.cs`:

```csharp
namespace BigSchool.Domain.Enums;

public enum EntityStatus : short
{
    Pending = 1,
    Active = 2,
    Processing = 3,
    Deleted = 4
}
```

Create file `src/backend/src/BigSchool.Domain/Enums/TransactionType.cs`:

```csharp
namespace BigSchool.Domain.Enums;

public enum TransactionType
{
    Income,
    Expense
}
```

Create file `src/backend/src/BigSchool.Domain/Enums/MainCategory.cs`:

```csharp
namespace BigSchool.Domain.Enums;

public enum MainCategory
{
    // Expense
    EssentialExpenses = 1,
    Investment = 2,
    Savings = 3,
    Donations = 4,
    Luxuries = 5,
    Education = 6,
    Amortizations = 7,

    // Income
    Salary = 10,
    Rentals = 11,
    Dividends = 12,
    Other = 13
}
```

Create file `src/backend/src/BigSchool.Domain/Enums/RecurrencePeriod.cs`:

```csharp
namespace BigSchool.Domain.Enums;

public enum RecurrencePeriod
{
    Monthly,
    Quarterly,
    Yearly
}
```

- [x] **Step 9: Run tests to verify they pass**

Run: `cd C:\SourceCode\BigSchool-TFM\src\backend && dotnet test tests/BigSchool.Domain.Tests -v minimal`
Expected: 3 tests passed.

- [x] **Step 10: Commit**

```powershell
cd C:\SourceCode\BigSchool-TFM
git add src/backend/src/BigSchool.Domain/ src/backend/tests/BigSchool.Domain.Tests/
git commit -m "feat: añadir capa Domain con BaseEntity, IAggregateRoot, IRepository, IUnitOfWork, Enums e IDomainEvent"
```

---

### Task 4: Application Layer — Interfaces, AppSettings, DomainEventNotification

**Files:**
- Create: `src/backend/src/BigSchool.Application/Interfaces/IDbConnectionFactory.cs`
- Create: `src/backend/src/BigSchool.Application/Interfaces/IRepository.cs`
- Create: `src/backend/src/BigSchool.Application/Interfaces/Repositories/IUserRepository.cs`
- Create: `src/backend/src/BigSchool.Application/Interfaces/Repositories/ITransactionRepository.cs`
- Create: `src/backend/src/BigSchool.Application/Interfaces/Repositories/IPortfolioRepository.cs`
- Create: `src/backend/src/BigSchool.Application/Interfaces/Repositories/IHoldingRepository.cs`
- Create: `src/backend/src/BigSchool.Application/Interfaces/Repositories/IRagDocumentRepository.cs`
- Create: `src/backend/src/BigSchool.Application/Interfaces/Services/IRagServiceClient.cs`
- Create: `src/backend/src/BigSchool.Application/Events/DomainEventNotification.cs`
- Create: `src/backend/src/BigSchool.Application/Configuration/AppSettings.cs`

- [x] **Step 1: Create IDbConnectionFactory**

Create file `src/backend/src/BigSchool.Application/Interfaces/IDbConnectionFactory.cs`:

```csharp
using System.Data;

namespace BigSchool.Application.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
```

- [x] **Step 2: Implement IRepository<T, Y>**

Create file `src/backend/src/BigSchool.Application/Interfaces/IRepository.cs`:

```csharp
using BigSchool.Domain.Entities;

namespace BigSchool.Application.Interfaces;

public interface IRepository<T, in Y> where T : IAggregateRoot
{
    IUnitOfWork UnitOfWork { get; }

    Task<T?> GetByIdAsync(Y id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(List<T> entities, CancellationToken cancellationToken = default);
}
```


- [x] **Step 3: Create specific repository interfaces (all Aggregate Roots)**

Create file `src/backend/src/BigSchool.Application/Interfaces/Repositories/IUserRepository.cs`:

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Interfaces;

namespace BigSchool.Application.Interfaces.Repositories;

public interface IUserRepository : IRepository<User, int>
{
}
```

Create file `src/backend/src/BigSchool.Application/Interfaces/Repositories/ITransactionRepository.cs`:

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Interfaces;

namespace BigSchool.Application.Interfaces.Repositories;

public interface ITransactionRepository : IRepository<Transaction, int>
{
}
```

Create file `src/backend/src/BigSchool.Application/Interfaces/Repositories/IPortfolioRepository.cs`:

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Interfaces;

namespace BigSchool.Application.Interfaces.Repositories;

public interface IPortfolioRepository : IRepository<Portfolio, int>
{
}
```

Create file `src/backend/src/BigSchool.Application/Interfaces/Repositories/IHoldingRepository.cs`:

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Interfaces;

namespace BigSchool.Application.Interfaces.Repositories;

public interface IHoldingRepository : IRepository<Holding, int>
{
}
```

Create file `src/backend/src/BigSchool.Application/Interfaces/Repositories/IRagDocumentRepository.cs`:

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Interfaces;

namespace BigSchool.Application.Interfaces.Repositories;

public interface IRagDocumentRepository : IRepository<RagDocument, int>
{
}
```

Note: These will not compile until the entities (User, Transaction, etc.) are created. For the scaffolding phase, create **placeholder entities** in Domain so the solution builds. They will be fully implemented in Phase 2 (Finanzas) and Phase 3 (Inversiones).

- [x] **Step 4: Create placeholder entities in Domain (minimal, just to satisfy compilation)**

Create file `src/backend/src/BigSchool.Domain/Entities/User.cs`:

```csharp
namespace BigSchool.Domain.Entities;

public class User : BaseEntity, IAggregateRoot
{
}
```

Create file `src/backend/src/BigSchool.Domain/Entities/Transaction.cs`:

```csharp
namespace BigSchool.Domain.Entities;

public class Transaction : BaseEntity, IAggregateRoot
{
}
```

Create file `src/backend/src/BigSchool.Domain/Entities/Portfolio.cs`:

```csharp
namespace BigSchool.Domain.Entities;

public class Portfolio : BaseEntity, IAggregateRoot
{
}
```

Create file `src/backend/src/BigSchool.Domain/Entities/Holding.cs`:

```csharp
namespace BigSchool.Domain.Entities;

public class Holding : BaseEntity, IAggregateRoot
{
}
```

Create file `src/backend/src/BigSchool.Domain/Entities/RagDocument.cs`:

```csharp
namespace BigSchool.Domain.Entities;

public class RagDocument : BaseEntity, IAggregateRoot
{
}
```

- [x] **Step 5: Create IRagServiceClient**

Create file `src/backend/src/BigSchool.Application/Interfaces/Services/IRagServiceClient.cs`:

```csharp
namespace BigSchool.Application.Interfaces.Services;

public interface IRagServiceClient
{
    Task<string> ChatAsync(string message, int userId, CancellationToken cancellationToken = default);
}
```

- [x] **Step 6: Create DomainEventNotification**

Create file `src/backend/src/BigSchool.Application/Events/DomainEventNotification.cs`:

```csharp
using BigSchool.Domain.Events;
using MediatR;

namespace BigSchool.Application.Events;

public class DomainEventNotification<T> : INotification where T : IDomainEvent
{
    public T DomainEvent { get; }

    public DomainEventNotification(T domainEvent)
    {
        DomainEvent = domainEvent;
    }
}
```

- [x] **Step 7: Create AppSettings**

Create file `src/backend/src/BigSchool.Application/Configuration/AppSettings.cs`:

```csharp
namespace BigSchool.Application.Configuration;

public class AppSettings
{
    public const string SectionName = "AppSettings";

    public required string ConnectionString { get; set; }

    public required JwtSettings Jwt { get; set; }

    public required RagServiceSettings RagService { get; set; }
}

public class JwtSettings
{
    public required string Secret { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int ExpirationMinutes { get; set; } = 60;
}

public class RagServiceSettings
{
    public required string BaseUrl { get; set; }
}
```

- [x] **Step 8: Verify build**

Run: `cd C:\SourceCode\BigSchool-TFM\src\backend && dotnet build Backend.sln`
Expected: Build succeeded.

- [x] **Step 9: Commit**

```powershell
cd C:\SourceCode\BigSchool-TFM
git add src/backend/src/BigSchool.Application/ src/backend/src/BigSchool.Domain/Entities/
git commit -m "feat: añadir capa Application con interfaces de repositorios, servicios, AppSettings y DomainEventNotification"
```

---

### Task 5: Infrastructure Layer — EFRepository, DbContext, DbConnectionMySqlFactory

**Files:**
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/EFRepository.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/DbConnectionMySqlFactory.cs`

- [ ] **Step 1: Create EFRepository<T, Y> abstract base**

Create file `src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/EFRepository.cs`:

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Persistence.Repositories;

public abstract class EFRepository<T, Y> : IRepository<T, Y> where T : class, IAggregateRoot
{
    protected readonly BigSchoolDbContext Context;

    protected EFRepository(BigSchoolDbContext context)
    {
        Context = context;
    }

    public IUnitOfWork UnitOfWork => Context;

    public virtual async Task<T?> GetByIdAsync(Y id, CancellationToken cancellationToken = default)
    {
        return await Context.Set<T>().FindAsync([id], cancellationToken);
    }

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await Context.Set<T>().AddAsync(entity, cancellationToken);
    }

    public virtual async Task AddRangeAsync(List<T> entities, CancellationToken cancellationToken = default)
    {
        await Context.Set<T>().AddRangeAsync(entities, cancellationToken);
    }
}
```

- [ ] **Step 2: Create BigSchoolDbContext**

Create file `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs`:

```csharp
using BigSchool.Application.Events;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Persistence;

public class BigSchoolDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    public BigSchoolDbContext(DbContextOptions<BigSchoolDbContext> options, IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<RagDocument> RagDocuments => Set<RagDocument>();

    public async Task<int> SaveChangesAsync(bool dispatchEvents = true, CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);

        if (dispatchEvents)
        {
            await DispatchDomainEvents();
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BigSchoolDbContext).Assembly);
    }

    private async Task DispatchDomainEvents()
    {
        var entities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities.SelectMany(e => e.DomainEvents).ToList();
        entities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            var notificationType = typeof(DomainEventNotification<>)
                .MakeGenericType(domainEvent.GetType());
            var notification = Activator.CreateInstance(notificationType, domainEvent);
            await _mediator.Publish(notification!);
        }
    }
}
```

- [ ] **Step 3: Create DbConnectionMySqlFactory**

Create file `src/backend/src/BigSchool.Infrastructure/Persistence/DbConnectionMySqlFactory.cs`:

```csharp
using System.Data;
using BigSchool.Application.Configuration;
using BigSchool.Application.Interfaces;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace BigSchool.Infrastructure.Persistence;

public class DbConnectionMySqlFactory : IDbConnectionFactory
{
    private readonly AppSettings _settings;

    public DbConnectionMySqlFactory(IOptions<AppSettings> settings)
    {
        _settings = settings.Value;
    }

    public IDbConnection CreateConnection()
    {
        return new MySqlConnection(_settings.ConnectionString);
    }
}
```

- [ ] **Step 4: Verify build**

Run: `cd C:\SourceCode\BigSchool-TFM\src\backend && dotnet build Backend.sln`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```powershell
cd C:\SourceCode\BigSchool-TFM
git add src/backend/src/BigSchool.Infrastructure/
git commit -m "feat: añadir capa Infrastructure con EFRepository, BigSchoolDbContext y DbConnectionMySqlFactory"
```

---

### Task 6: WebApi — Program.cs with Autofac (simplified), CustomMediatR, Serilog, Swagger, Health Check

**Files:**
- Create: `src/backend/src/BigSchool.WebApi/Infrastructure/CustomMediatR.cs`
- Create: `src/backend/src/BigSchool.WebApi/Program.cs` (overwrite template)
- Create: `src/backend/src/BigSchool.WebApi/appsettings.json` (overwrite template)

- [ ] **Step 1: Create CustomMediatR with PublishStrategy**

Create file `src/backend/src/BigSchool.WebApi/Infrastructure/CustomMediatR.cs`:

```csharp
using MediatR;
using MediatR.NotificationPublishers;

namespace BigSchool.WebApi.Infrastructure;

public class CustomMediatR : Mediator
{
    private readonly IServiceProvider _serviceFactory;
    private readonly Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> _publish;
    private readonly Dictionary<PublishStrategy, IMediator> _publishStrategies;

    private CustomMediatR(IServiceProvider serviceFactory, Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish) : base(serviceFactory)
    {
        _serviceFactory = serviceFactory;
        _publish = publish;
        _publishStrategies = default!;
    }

    public CustomMediatR(IServiceProvider serviceFactory) : base(serviceFactory)
    {
        _serviceFactory = serviceFactory;
        _publish = base.PublishCore;

        _publishStrategies = new Dictionary<PublishStrategy, IMediator>
        {
            [PublishStrategy.Async] = new CustomMediatR(_serviceFactory, AsyncContinueOnException),
            [PublishStrategy.ParallelNoWait] = new CustomMediatR(_serviceFactory, ParallelNoWait),
            [PublishStrategy.ParallelWhenAll] = new CustomMediatR(_serviceFactory, ParallelWhenAll),
            [PublishStrategy.ParallelWhenAny] = new CustomMediatR(_serviceFactory, ParallelWhenAny),
            [PublishStrategy.SyncContinueOnException] = new CustomMediatR(_serviceFactory, SyncContinueOnException),
            [PublishStrategy.SyncStopOnException] = new CustomMediatR(_serviceFactory, SyncStopOnException)
        };
    }

    protected override Task PublishCore(IEnumerable<NotificationHandlerExecutor> allHandlers, INotification notification, CancellationToken cancellationToken)
    {
        return _publish(allHandlers, notification, cancellationToken);
    }

    public Task Publish<TNotification>(TNotification notification, PublishStrategy strategy, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (!_publishStrategies.TryGetValue(strategy, out var mediator))
        {
            throw new ArgumentException($"Unknown strategy: {strategy}");
        }

        return mediator.Publish(notification, cancellationToken);
    }

    private Task ParallelWhenAll(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var handler in handlers)
        {
            tasks.Add(Task.Run(() => handler.HandlerCallback(notification, cancellationToken)));
        }
        return Task.WhenAll(tasks);
    }

    private Task ParallelWhenAny(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var handler in handlers)
        {
            tasks.Add(Task.Run(() => handler.HandlerCallback(notification, cancellationToken)));
        }
        return Task.WhenAny(tasks);
    }

    private Task ParallelNoWait(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            Task.Run(() => handler.HandlerCallback(notification, cancellationToken));
        }
        return Task.CompletedTask;
    }

    private async Task AsyncContinueOnException(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        foreach (var handler in handlers)
        {
            try
            {
                tasks.Add(handler.HandlerCallback(notification, cancellationToken));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                exceptions.Add(ex);
            }
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (AggregateException ex)
        {
            exceptions.AddRange(ex.Flatten().InnerExceptions);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            exceptions.Add(ex);
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }

    private async Task SyncStopOnException(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SyncContinueOnException(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        var exceptions = new List<Exception>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
            }
            catch (AggregateException ex)
            {
                exceptions.AddRange(ex.Flatten().InnerExceptions);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}

public enum PublishStrategy
{
    /// <summary>
    /// Run each notification handler after one another. Returns when all handlers are finished.
    /// In case of any exception(s), they will be captured in an AggregateException.
    /// </summary>
    SyncContinueOnException = 0,

    /// <summary>
    /// Run each notification handler after one another. Returns when all handlers are finished
    /// or an exception has been thrown. In case of an exception, any handlers after that will not be run.
    /// </summary>
    SyncStopOnException = 1,

    /// <summary>
    /// Run all notification handlers asynchronously. Returns when all handlers are finished.
    /// In case of any exception(s), they will be captured in an AggregateException.
    /// </summary>
    Async = 2,

    /// <summary>
    /// Run each notification handler on its own thread using Task.Run(). Returns immediately
    /// and does not wait for any handlers to finish.
    /// </summary>
    ParallelNoWait = 3,

    /// <summary>
    /// Run each notification handler on its own thread using Task.Run(). Returns when all
    /// threads (handlers) are finished.
    /// </summary>
    ParallelWhenAll = 4,

    /// <summary>
    /// Run each notification handler on its own thread using Task.Run(). Returns when any
    /// thread (handler) is finished.
    /// </summary>
    ParallelWhenAny = 5
}
```

- [ ] **Step 2: Write Program.cs (composition root with simplified Autofac)**

Replace content of `src/backend/src/BigSchool.WebApi/Program.cs`:

```csharp
using Autofac;
using Autofac.Extensions.DependencyInjection;
using BigSchool.Application.Configuration;
using BigSchool.Infrastructure.Persistence;
using BigSchool.WebApi.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/bigschool-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/bigschool-.log", rollingInterval: RollingInterval.Day));

    // AppSettings binding
    builder.Services.Configure<AppSettings>(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
        options.Jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
        options.RagService = builder.Configuration.GetSection("RagService").Get<RagServiceSettings>()
            ?? new RagServiceSettings { BaseUrl = "http://localhost:8000" };
    });

    // Autofac como DI container (simplified: one RegisterAssemblyTypes per layer)
    builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
    builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        // Domain layer (no services to register, but pattern is ready)
        containerBuilder.RegisterAssemblyTypes(typeof(BigSchool.Domain.Entities.BaseEntity).Assembly)
            .AsImplementedInterfaces();

        // Application layer
        containerBuilder.RegisterAssemblyTypes(typeof(AppSettings).Assembly)
            .AsImplementedInterfaces();

        // Infrastructure layer
        containerBuilder.RegisterAssemblyTypes(typeof(BigSchoolDbContext).Assembly)
            .AsImplementedInterfaces();

        // WebApi layer
        containerBuilder.RegisterAssemblyTypes(typeof(Program).Assembly)
            .AsImplementedInterfaces();

        // DbContext
        containerBuilder.Register(ctx =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<BigSchoolDbContext>();
            var configuration = ctx.Resolve<Microsoft.Extensions.Configuration.IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseMySql(connectionString!, ServerVersion.AutoDetect(connectionString!));
            var mediator = ctx.Resolve<IMediator>();
            return new BigSchoolDbContext(optionsBuilder.Options, mediator);
        })
        .AsSelf()
        .As<BigSchool.Domain.Interfaces.IUnitOfWork>()
        .InstancePerLifetimeScope();
    });

    // MediatR con CustomMediatR (dispatch secuencial por defecto: SyncContinueOnException)
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(AppSettings).Assembly);
    });
    builder.Services.AddTransient<IMediator, CustomMediatR>();

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "BigSchool API",
            Version = "v1",
            Description = "API de finanzas personales e inversiones"
        });
    });

    // Controllers
    builder.Services.AddControllers();

    // Health checks
    builder.Services.AddHealthChecks();

    // CORS (desarrollo)
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // Middleware pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "BigSchool API v1"));
    }

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Necesario para Integration Tests (WebApplicationFactory)
public partial class Program { }
```

- [ ] **Step 3: Write appsettings.json**

Replace content of `src/backend/src/BigSchool.WebApi/appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=bigschool;User=bigschool;Password=bigschool_dev;CharSet=utf8mb4;"
  },
  "Jwt": {
    "Secret": "CHANGE_ME_IN_PRODUCTION_minimum_32_chars!!",
    "Issuer": "BigSchool",
    "Audience": "BigSchool",
    "ExpirationMinutes": 60
  },
  "RagService": {
    "BaseUrl": "http://localhost:8000"
  }
}
```

- [ ] **Step 4: Delete appsettings.Development.json if it was auto-generated**

```powershell
Remove-Item C:\SourceCode\BigSchool-TFM\src\backend\src\BigSchool.WebApi\appsettings.Development.json -ErrorAction SilentlyContinue
```

- [ ] **Step 5: Initialize user-secrets for the WebApi project**

```powershell
cd C:\SourceCode\BigSchool-TFM\src\backend\src\BigSchool.WebApi
dotnet user-secrets init
```

- [ ] **Step 6: Verify build**

Run: `cd C:\SourceCode\BigSchool-TFM\src\backend && dotnet build Backend.sln`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```powershell
cd C:\SourceCode\BigSchool-TFM
git add src/backend/src/BigSchool.WebApi/
git commit -m "feat: configurar WebApi con Autofac simplificado, CustomMediatR, Serilog, Swagger y Health Check"
```

---

### Task 7: EditorConfig and Final Cleanup

**Files:**
- Create: `src/backend/.editorconfig`

- [ ] **Step 1: Create .editorconfig**

Create file `src/backend/.editorconfig`:

```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_style_var_for_built_in_types = false:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:suggestion
dotnet_diagnostic.CS8618.severity = warning
```

- [ ] **Step 2: Final build + test run**

```powershell
cd C:\SourceCode\BigSchool-TFM\src\backend
dotnet build Backend.sln
dotnet test Backend.sln -v minimal
```

Expected: Build succeeded. 3 tests passed (BaseEntity tests).

- [ ] **Step 3: Commit**

```powershell
cd C:\SourceCode\BigSchool-TFM
git add src/backend/.editorconfig
git commit -m "chore: añadir editorconfig para el backend"
```

---

### Task 8: Verify Quickstart — Run the API

**Files:**
- No new files. Verification only.

- [ ] **Step 1: Ensure MySQL is running (via Docker Compose)**

```powershell
cd C:\SourceCode\BigSchool-TFM\infra
docker compose up -d mysql
```

Wait ~10 seconds for MySQL to be ready.

- [ ] **Step 2: Run the API**

```powershell
cd C:\SourceCode\BigSchool-TFM\src\backend\src\BigSchool.WebApi
dotnet run
```

Expected: Application starts. Console shows Serilog output with the URL (e.g., `http://localhost:5062`).

- [ ] **Step 3: Verify health endpoint**

In a separate terminal:

```powershell
Invoke-WebRequest -Uri http://localhost:5062/health -UseBasicParsing | Select-Object -ExpandProperty Content
```

Expected: `Healthy`

- [ ] **Step 4: Verify Swagger UI**

```powershell
Invoke-WebRequest -Uri http://localhost:5062/swagger/v1/swagger.json -UseBasicParsing | Select-Object -ExpandProperty StatusCode
```

Expected: `200`

- [ ] **Step 5: Verify Serilog file output**

```powershell
Get-ChildItem C:\SourceCode\BigSchool-TFM\src\backend\src\BigSchool.WebApi\logs\
```

Expected: A `bigschool-YYYYMMDD.log` file exists with log entries.

- [ ] **Step 6: Stop the API (Ctrl+C) and final commit**

```powershell
cd C:\SourceCode\BigSchool-TFM
git add .
git commit -m "feat: scaffolding completo del backend .NET 8 con Clean Architecture - quickstart funcional"
```

---

## Summary

After completing all 8 tasks you will have:

1. ✅ Solution .NET 8 con 4 proyectos fuente + 3 de tests
2. ✅ Clean Architecture con dependencias correctas (Domain ← Application ← Infrastructure ← WebApi)
3. ✅ Directory.Build.props con versiones centralizadas de NuGet
4. ✅ Domain: BaseEntity, IAggregateRoot, IRepository<T,Y>, IUnitOfWork (con dispatchEvents param), IDomainEvent, Enums (MainCategory en inglés)
5. ✅ Application: IDbConnectionFactory, repositorios específicos (IUserRepository, etc.), IRagServiceClient, DomainEventNotification, AppSettings
6. ✅ Infrastructure: EFRepository<T,Y> abstracto, BigSchoolDbContext con dispatch condicional, DbConnectionMySqlFactory con IOptions<AppSettings>
7. ✅ WebApi: Program.cs con Autofac simplificado (un RegisterAssemblyTypes por capa), CustomMediatR (SyncContinueOnException por defecto), Serilog (file sink), Swagger, Health Check
8. ✅ Tests: 3 tests unitarios de BaseEntity pasando
9. ✅ Arrancable con `dotnet run` — Swagger en `/swagger`, Health en `/health`, logs en `/logs/`
10. ✅ Sin appsettings.Development.json — usar `dotnet user-secrets` para overrides locales
