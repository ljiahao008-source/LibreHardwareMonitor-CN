// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Globalization;
using System.Management;

namespace LibreHardwareMonitor.Hardware.AcpiWmi;

/// <summary>
/// Reads the fan speed of devices that only expose it through a private ACPI WMI interface
/// (e.g. the "Ip3PowerSwitch" class on MECHREVO IMINI mini PCs). The fan is controlled by the
/// embedded controller and is not reachable through the usual SuperIO/LPC channels.
/// </summary>
internal sealed class AcpiWmiHardware : Hardware
{
    private const string WmiNamespace = @"\\.\root\WMI";
    private const string WmiClassName = "PowerSwitchInterface";
    private const string MethodGetFanControl = "GetFanControl";

    private readonly Sensor _fan;
    private readonly string _instanceName;
    private ManagementObject _wmiObject;
    private ManagementScope _scope;

    public AcpiWmiHardware(string instanceName, ISettings settings) :
        base("ACPI WMI Fan", new Identifier("acpiwmi", "ip3powerswitch"), settings)
    {
        _instanceName = instanceName;
        _fan = new Sensor("Fan #1", 0, SensorType.Fan, this, settings);
    }

    /// <inheritdoc />
    public override HardwareType HardwareType => HardwareType.EmbeddedController;

    /// <inheritdoc />
    public override void Update()
    {
        try
        {
            float rpm = ReadFanSpeed();
            _fan.Value = rpm;
            ActivateSensor(_fan);
        }
        catch
        {
            // 读取失败（权限被回收、实例消失等）时丢弃本次读数，并释放连接以便下次重连。
            _wmiObject?.Dispose();
            _wmiObject = null;
            _fan.Value = null;
            DeactivateSensor(_fan);
        }
    }

    private float ReadFanSpeed()
    {
        if (_wmiObject == null)
        {
            _scope ??= CreateScope();
            _wmiObject = new ManagementObject(_scope, new ManagementPath(GetInstancePath(_instanceName)), null);
        }

        using ManagementBaseObject inParams = _wmiObject.GetMethodParameters(MethodGetFanControl);
        inParams["FanNumber"] = (byte)1;

        using ManagementBaseObject outParams = _wmiObject.InvokeMethod(MethodGetFanControl, inParams, null);
        object raw = outParams?["FanDuty"] ?? outParams?["ReturnValue"];
        if (raw == null)
            throw new InvalidOperationException("ACPI WMI method '" + MethodGetFanControl + "' returned no value.");

        // 低 16 位为风扇转速（RPM）。
        return Convert.ToUInt32(raw, CultureInfo.InvariantCulture) & 0xFFFF;
    }

    /// <summary>
    /// 动态 ACPI WMI 类没有 <c>__PATH</c>，必须用 <c>InstanceName</c> 手工拼实例路径，
    /// 并把路径中的反斜杠转义为 <c>\\</c>。
    /// </summary>
    private static string GetInstancePath(string instanceName)
    {
        return WmiClassName + ".InstanceName=\"" + instanceName.Replace(@"\", @"\\") + "\"";
    }

    private static ManagementScope CreateScope()
    {
        ConnectionOptions options = new()
        {
            Impersonation = ImpersonationLevel.Impersonate,
            Authentication = AuthenticationLevel.PacketPrivacy,
            EnablePrivileges = true
        };

        ManagementScope scope = new(WmiNamespace, options);
        scope.Connect();
        return scope;
    }

    /// <inheritdoc />
    public override string GetReport()
    {
        return "ACPI WMI (private fan channel)" + Environment.NewLine +
               " Class: " + WmiClassName + Environment.NewLine +
               " Instance: " + _instanceName + Environment.NewLine +
               " Fan speed: " + (_fan.Value?.ToString(CultureInfo.InvariantCulture) ?? "n/a") + " RPM" + Environment.NewLine;
    }

    /// <inheritdoc />
    public override void Close()
    {
        base.Close();
        _wmiObject?.Dispose();
        _wmiObject = null;
        _scope = null;
    }
}
