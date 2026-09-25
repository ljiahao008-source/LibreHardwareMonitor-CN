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
    private string _lastError;

    public AcpiWmiHardware(string instanceName, ISettings settings) :
        base("ACPI WMI 风扇", new Identifier("acpiwmi", "ip3powerswitch"), settings)
    {
        _instanceName = instanceName;
        _fan = new Sensor("风扇 #1", 0, SensorType.Fan, this, settings);

        // 必须在构造函数中激活：MainForm 设置「传感器数值时间窗口」时只遍历已激活的传感器，
        // 若此处不激活，风扇将拿不到时间窗口，导致数值正常但曲线无历史数据。
        ActivateSensor(_fan);
    }

    /// <inheritdoc />
    public override HardwareType HardwareType => HardwareType.EmbeddedController;

    /// <inheritdoc />
    public override void Update()
    {
        try
        {
            _fan.Value = ReadFanSpeed();
            _lastError = null;
        }
        catch (Exception e)
        {
            // 读取失败（权限被回收、实例消失等）：丢弃本次读数并释放连接，下次自动重连。
            // 这里不调用 DeactivateSensor —— 否则节点会在树上反复消失/出现。
            _wmiObject?.Dispose();
            _wmiObject = null;
            _fan.Value = null;
            _lastError = e.Message;
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
        // 该编号没有风扇（或控制器忙/无响应）时返回 0x7FFFFFFF，其低 16 位为 0xFFFF；
        // 必须过滤掉，否则会显示出 65535 RPM 这种不可能的读数。
        uint raw32 = Convert.ToUInt32(raw, CultureInfo.InvariantCulture);
        uint rpm = raw32 & 0xFFFF;
        if (rpm == 0xFFFF)
            throw new InvalidOperationException("ACPI WMI 返回无效转速（0x" + raw32.ToString("X8", CultureInfo.InvariantCulture) + "）。");

        return rpm;
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
        return "ACPI WMI 私有风扇通道" + Environment.NewLine +
               " 类: " + WmiClassName + Environment.NewLine +
               " 实例: " + _instanceName + Environment.NewLine +
               " 转速: " + (_fan.Value?.ToString(CultureInfo.InvariantCulture) ?? "无数据") + " RPM" + Environment.NewLine +
               " 最近错误: " + (_lastError ?? "无") + Environment.NewLine;
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
