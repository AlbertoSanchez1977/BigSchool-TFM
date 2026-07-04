using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.Notifications.Enums;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities.Notifications;

public class EmailLogTests
{
    [Fact]
    public void CreateWelcome_fija_Type_IdUser_y_recipient()
    {
        var e = EmailLog.CreateWelcome(42, "ada@example.com", "Ada");
        e.Type.Should().Be(EmailType.Welcome);
        e.IdUser.Should().Be(42);
        e.Recipient.Should().Be("ada@example.com");
        e.Subject.Should().NotBeNullOrWhiteSpace();
        e.Body.Should().Contain("Ada");
    }

    [Fact]
    public void CreateContactAck_fija_Type_Contact_y_IdUser_null()
    {
        var e = EmailLog.CreateContactAck("ada@example.com", "Ada");
        e.Type.Should().Be(EmailType.Contact);
        e.IdUser.Should().BeNull();
        e.Recipient.Should().Be("ada@example.com");
    }
}
