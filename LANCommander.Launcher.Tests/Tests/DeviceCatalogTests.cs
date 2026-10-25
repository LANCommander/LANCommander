using System;
using System.Linq;
using LANCommander.Launcher.Services.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Lists this machine's devices through the real Windows APIs. What a machine has varies, so these check
/// that each list is read without failing and is well formed, and print it for a person to look at.
/// </summary>
public class DeviceCatalogTests(ITestOutputHelper output)
{
    [Fact]
    public void NetworkAdaptersAreListedWithTheirSubnets()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var adapters = new WindowsDeviceCatalog(NullLogger<WindowsDeviceCatalog>.Instance).GetNetworkAdapters();

        foreach (var adapter in adapters)
            output.WriteLine($"{adapter.Name} | {adapter.Description} | {adapter.MacAddress} | {adapter.Subnet} | up: {adapter.IsConnected}");

        Assert.NotEmpty(adapters);
        Assert.All(adapters.Where(a => a.MacAddress != null), a => Assert.Matches("^([0-9A-F]{2}:){5}[0-9A-F]{2}$", a.MacAddress));
    }

    [Fact]
    public void AudioDevicesAreListedByEndpointId()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var devices = new WindowsDeviceCatalog(NullLogger<WindowsDeviceCatalog>.Instance).GetAudioDevices();

        foreach (var device in devices)
            output.WriteLine($"{device.Direction} | {device.Name} | {device.Id} | default: {device.IsDefault}");

        Assert.All(devices, d => Assert.StartsWith("{0.0.", d.Id));
        Assert.True(devices.Count(d => d.Direction == AudioDirection.Output && d.IsDefault) <= 1);
    }

    [Fact]
    public void ControllersAreListedWithoutFailing()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var controllers = new WindowsDeviceCatalog(NullLogger<WindowsDeviceCatalog>.Instance).GetControllers();

        foreach (var controller in controllers)
            output.WriteLine($"{controller.Class} | {controller.ProductName} | {controller.InstanceName}");

        Assert.All(controllers, c => Assert.False(string.IsNullOrWhiteSpace(c.ProductName)));
    }

    [Theory]
    [InlineData("192.168.1.20", 24, "192.168.1.0/24")]
    [InlineData("10.13.37.5", 8, "10.0.0.0/8")]
    [InlineData("172.16.5.4", 32, "172.16.5.4/32")]
    [InlineData("172.16.5.4", 0, "0.0.0.0/0")]
    public void AnAdaptersSubnetIsItsNetworkAddress(string address, int prefix, string expected)
    {
        Assert.Equal(expected, new NetworkAdapterDevice("Ethernet", "", null, address, prefix, true).Subnet);
    }

    [Fact]
    public void AnAdapterWithoutAnIPv4AddressHasNoSubnet()
    {
        Assert.Null(new NetworkAdapterDevice("Bluetooth", "", null, null, null, false).Subnet);
    }

    [Fact]
    public void ASelfAssignedAddressHasNoSubnet()
    {
        // Every unconnected adapter has one; as a rule it would keep them all
        Assert.Null(new NetworkAdapterDevice("Ethernet 2", "", "58:11:22:4E:39:FA", "169.254.12.7", 16, false).Subnet);
    }
}
