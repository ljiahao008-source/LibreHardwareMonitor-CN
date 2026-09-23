// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Utilities;

namespace LibreHardwareMonitor.UI;

public sealed class TypeNode : Node, IExpandPersistNode
{
    private readonly PersistentSettings _settings;
    private readonly string _expandedIdentifier;
    private bool _expanded;

    public TypeNode(SensorType sensorType, Identifier parentId, PersistentSettings settings)
    {
        SensorType = sensorType;
        _expandedIdentifier = new Identifier(parentId, SensorType.ToString(), ".expanded").ToString();
        _settings = settings;

        switch (sensorType)
        {
            case SensorType.Voltage:
                Image = Utilities.EmbeddedResources.GetImage("voltage.png");
                Text = "电压";
                break;
            case SensorType.Current:
                Image = Utilities.EmbeddedResources.GetImage("voltage.png");
                Text = "电流";
                break;
            case SensorType.Energy:
                Image = Utilities.EmbeddedResources.GetImage("battery.png");
                Text = "容量";
                break;
            case SensorType.Clock:
                Image = Utilities.EmbeddedResources.GetImage("clock.png");
                Text = "时钟";
                break;
            case SensorType.Load:
                Image = Utilities.EmbeddedResources.GetImage("load.png");
                Text = "负载";
                break;
            case SensorType.Temperature:
                Image = Utilities.EmbeddedResources.GetImage("temperature.png");
                Text = "温度";
                break;
            case SensorType.Fan:
                Image = Utilities.EmbeddedResources.GetImage("fan.png");
                Text = "风扇";
                break;
            case SensorType.Flow:
                Image = Utilities.EmbeddedResources.GetImage("flow.png");
                Text = "流量";
                break;
            case SensorType.Control:
                Image = Utilities.EmbeddedResources.GetImage("control.png");
                Text = "控制";
                break;
            case SensorType.Level:
                Image = Utilities.EmbeddedResources.GetImage("level.png");
                Text = "水平";
                break;
            case SensorType.Power:
                Image = Utilities.EmbeddedResources.GetImage("power.png");
                Text = "功耗";
                break;
            case SensorType.Data:
                Image = Utilities.EmbeddedResources.GetImage("data.png");
                Text = "数据";
                break;
            case SensorType.SmallData:
                Image = Utilities.EmbeddedResources.GetImage("data.png");
                Text = "数据";
                break;
            case SensorType.Factor:
                Image = Utilities.EmbeddedResources.GetImage("factor.png");
                Text = "系数";
                break;
            case SensorType.Frequency:
                Image = Utilities.EmbeddedResources.GetImage("clock.png");
                Text = "频率";
                break;
            case SensorType.Throughput:
                Image = Utilities.EmbeddedResources.GetImage("throughput.png");
                Text = "吞吐量";
                break;
            case SensorType.TimeSpan:
                Image = Utilities.EmbeddedResources.GetImage("time.png");
                Text = "时间";
                break;
            case SensorType.Timing:
                Image = Utilities.EmbeddedResources.GetImage("time.png");
                Text = "时序";
                break;
            case SensorType.Noise:
                Image = Utilities.EmbeddedResources.GetImage("loudspeaker.png");
                Text = "噪声";
                break;
            case SensorType.Conductivity:
                Image = Utilities.EmbeddedResources.GetImage("voltage.png");
                Text = "电导率";
                break;
            case SensorType.Humidity:
                Image = Utilities.EmbeddedResources.GetImage("humidity.png");
                Text = "湿度";
                break;
        }

        NodeAdded += TypeNode_NodeAdded;
        NodeRemoved += TypeNode_NodeRemoved;
        _expanded = settings.GetValue(_expandedIdentifier, true);
    }

    private void TypeNode_NodeRemoved(Node node)
    {
        node.IsVisibleChanged -= Node_IsVisibleChanged;
        Node_IsVisibleChanged(null);
    }

    private void TypeNode_NodeAdded(Node node)
    {
        node.IsVisibleChanged += Node_IsVisibleChanged;
        Node_IsVisibleChanged(null);
    }

    private void Node_IsVisibleChanged(Node node)
    {
        foreach (Node n in Nodes)
        {
            if (n.IsVisible)
            {
                IsVisible = true;
                return;
            }
        }
        IsVisible = false;
    }

    public SensorType SensorType { get; }

    public bool Expanded
    {
        get => _expanded;
        set
        {
            _expanded = value;
            _settings.SetValue(_expandedIdentifier, _expanded);
        }
    }
}
