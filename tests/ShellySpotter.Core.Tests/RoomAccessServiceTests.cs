using ShellySpotter.Core.Models;
using ShellySpotter.Core.Services;
using Xunit;

namespace ShellySpotter.Core.Tests;

public class RoomAccessServiceTests
{
    private const int RoomId = 1001;

    private static async Task<RoomAccessService> SetupAsync(string ownerId)
    {
        var db = TestSupport.NewDb();
        db.Rooms.Add(new Room { Id = RoomId, Name = "Room", OwnerId = ownerId });
        await db.SaveChangesAsync();
        return new RoomAccessService(db);
    }

    [Fact]
    public async Task Customer_can_access_own_room()
    {
        var svc = await SetupAsync("alice");
        Assert.True(await svc.CanAccessRoomAsync(TestSupport.User("Customer", "alice"), RoomId));
    }

    [Fact]
    public async Task Customer_cannot_access_another_customers_room()
    {
        var svc = await SetupAsync("alice");
        Assert.False(await svc.CanAccessRoomAsync(TestSupport.User("Customer", "bob"), RoomId));
    }

    [Fact]
    public async Task Customer_without_sub_claim_is_denied()
    {
        var svc = await SetupAsync("alice");
        Assert.False(await svc.CanAccessRoomAsync(TestSupport.User("Customer"), RoomId));
    }

    [Theory]
    [InlineData("Employee")]
    [InlineData("Admin")]
    [InlineData("Agent")]
    public async Task Privileged_roles_can_access_any_room(string role)
    {
        var svc = await SetupAsync("alice");
        Assert.True(await svc.CanAccessRoomAsync(TestSupport.User(role, "not-the-owner"), RoomId));
    }

    [Fact]
    public async Task Customer_denied_for_nonexistent_room()
    {
        var svc = await SetupAsync("alice");
        Assert.False(await svc.CanAccessRoomAsync(TestSupport.User("Customer", "alice"), 99999));
    }
}
