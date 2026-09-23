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
/// 并为找到的实例创建 <see cref="AcpiWmiHardware" />。读取实例需要管理员权限。
/// 本机不存在该类或无权限时不产生任何硬件，失败原因记录在 <see cref="GetReport" /> 中。
/// </summary>
internal sealed class AcpiWmiGroup : IGroup
{
    private const string WmiNamespace = @"\\.\root\WMI";
    private const string WmiClassName = "PowerSwitchInterface";

    private readonly List<AcpiWmiHardware> _hardware = [];
    private readonly string _diagnostic = "未初始化";

    public AcpiWmiGroup(ISettings settings)
    {
        // 该通道是 Windows 专有的 WMI 接口，其他平台直接跳过。
        if (Software.OperatingSystem.IsUnix)
        {
            _diagnostic = "非 Windows 平台，已跳过。";
            return;
        }

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

            // 先做类存在性检查（取类对象，不枚举实例）。
            ManagementClass wmiClass = new(scope, new ManagementPath(WmiClassName), null);
            wmiClass.Get();

            // 枚举实例名。System.Management 对动态 ACPI 类有时枚举不到实例；
            // 此时不做任何猜测 —— 早期版本用固定实例名兜底，但那只对特定机型成立。
            string instanceName = EnumerateInstanceName(scope);
            if (string.IsNullOrEmpty(instanceName))
            {
                _diagnostic = "WMI 类存在，但未枚举到实例（InstanceName 为空），未创建硬件。";
                return;
            }

            _hardware.Add(new AcpiWmiHardware(instanceName, settings));
            _diagnostic = "正常，实例: " + instanceName;
        }
        catch (Exception e)
        {
            // 未以管理员运行（WBEM_E_ACCESS_DENIED 0x80041003）或本机不存在该类。
            // 不静默丢弃：原因记入诊断信息，可在「文件 → 保存报告」中查看。
            _diagnostic = "初始化失败: " + e.Message;
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
        reportBuilder.AppendLine("ACPI WMI 私有风扇通道:");
        reportBuilder.Append(" 状态: ").AppendLine(_diagnostic);

        uint count = 1;
        foreach (AcpiWmiHardware hardware in _hardware)
        {
            reportBuilder.Append(" 硬件 #").Append(count).AppendLine(":")
                         .Append("  名称: ").AppendLine(hardware.Name);
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
