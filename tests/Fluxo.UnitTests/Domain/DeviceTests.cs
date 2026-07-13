using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;

namespace Fluxo.UnitTests.Domain;

public class DeviceTests
{
    [Fact]
    public void Should_Create_Device_When_Data_Is_Valid()
    {
        var workspaceId = Guid.NewGuid();

        var device = new Device(
            workspaceId,
            "Pump 1",
            "pump-001",
            DeviceCategory.Actuator,
            "{\"floor\":1}");

        Assert.NotEqual(Guid.Empty, device.Id);
        Assert.Equal(workspaceId, device.WorkspaceId);
        Assert.Equal("Pump 1", device.Name);
        Assert.Equal("pump-001", device.Identifier);
        Assert.Equal(DeviceCategory.Actuator, device.Category);
        Assert.True(device.IsActive);
        Assert.Equal(Device.DefaultTenantId, device.TenantId);
        Assert.Equal(DateTimeKind.Utc, device.CreatedAtUtc.Kind);
    }

    [Fact]
    public void Should_Throw_When_WorkspaceId_Is_Empty()
    {
        Assert.Throws<ArgumentException>(() =>
            new Device(Guid.Empty, "Device", "device-001", DeviceCategory.Sensor));
    }

    [Fact]
    public void Should_Throw_When_Name_Is_Empty()
    {
        Assert.Throws<ArgumentException>(() =>
            new Device(Guid.NewGuid(), "", "device-001", DeviceCategory.Sensor));
    }

    [Fact]
    public void Should_Throw_When_Identifier_Is_Empty()
    {
        Assert.Throws<ArgumentException>(() =>
            new Device(Guid.NewGuid(), "Device", "", DeviceCategory.Sensor));
    }

    [Fact]
    public void Should_Register_Telemetry_Snapshot_And_Set_Device_Online()
    {
        var nowUtc = DateTime.UtcNow;
        var device = new Device(Guid.NewGuid(), "Device", "device-001", DeviceCategory.Sensor, tenantId: "acme");

        device.RegisterTelemetrySnapshot(
            "{\"temperature\":22.4}",
            nowUtc,
            nowUtc.AddSeconds(-5),
            21);

        Assert.Equal(nowUtc, device.LastContactAtUtc);
        Assert.Equal(nowUtc, device.LastTelemetryReceivedAtUtc);
        Assert.Equal(21, device.LastTelemetrySequence);
        Assert.Equal(DeviceOperationalStatus.Online, device.GetOperationalStatus(nowUtc.AddSeconds(20), TimeSpan.FromMinutes(2)));
    }
}
