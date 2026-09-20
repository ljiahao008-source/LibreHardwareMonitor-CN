// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Management;
using System.Text;

namespace LibreHardwareMonitor.Hardware.AcpiWmi;

/// <summary>
/// 发现私有 ACPI WMI 风扇接口（"PowerSwitchInterface" / "Ip3PowerSwitch"，root\WMI 命名空间），
/// 并为找到的实例创建 <see cref="AcpiWmiHardware" />。读取实例需要管理员权限，
/// 无权限或本机没有该类时静默跳过，不产生任何硬件。
/// </summary>
internal sealed class AcpiWmiGroup : IGroup
{
    private const string WmiNamespace = @"\\.\root\WMI";
    private const string WmiClassName = "PowerSwitchInterface";
    private const string WmiInstanceName = @"ACPI\PNP0C14\IP3POWERSWITCH_0";

    private readonly List<AcpiWmiHardware> _hardware = [];

    public AcpiWmiGroup(ISettings settings)
    {
        // 该通道是 Windows 专有的 WMI 接口，其他平台直接跳过。
        if (Software.OperatingSystem.IsUnix)
            return;

        try
        {
            ConnectionOptions options = new()
            {
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.PacketPrivacy,
                EnablePrivileges = true
            };

            ManagementScope scope = new(WmiNamespace, options);
            scope.Connect();

            // 1) 先做类存在性检查（GetObject on class，不枚举实例）。
            ManagementClass wmiClass = new(scope, new ManagementPath(WmiClassName), null);
            wmiClass.Get();

            // 2) 取实例名：优先枚举；System.Management 对动态 ACPI 类可能枚举不到实例，
            //    此时回退到该类已知的固定实例名。
            string instanceName = EnumerateInstanceName(scope);
            if (string.IsNullOrEmpty(instanceName))
                instanceName = WmiInstanceName;

            _hardware.Add(new AcpiWmiHardware(instanceName, settings));
        }
        catch
        {
            // 未以管理员运行（WBEM_E_ACCESS_DENIED 0x80041003）或本机不存在该类：静默跳过。
        }
    }

    private static string EnumerateInstanceName(ManagementScope scope)
    {
        try
        {
            using ManagementObjectSearcher searcher = new(scope, new ObjectQuery("SELECT * FROM " + WmiClassName));
            foreach (ManagementObject instance in searcher.Get())
            {
                string instanceName = instance["InstanceName"]?.ToString();
                if (!string.IsNullOrEmpty(instanceName))
                    return instanceName;
            }
        }
        catch
        {
            // 枚举失败不影响后续用已知实例名直接调用方法。
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<IHardware> Hardware => _hardware;

    /// <inheritdoc />
    public string GetReport()
    {
        StringBuilder reportBuilder = new();
        uint count = 1;

        foreach (AcpiWmiHardware hardware in _hardware)
        {
            reportBuilder.Append("ACPI WMI #").Append(count).AppendLine(":")
                         .Append(" Name: ").AppendLine(hardware.Name);
            count++;
        }

        return reportBuilder.ToString();
    }

    /// <inheritdoc />
    public void Close()
    {
        foreach (AcpiWmiHardware hardware in _hardware)
            hardware.Close();
    }
}
