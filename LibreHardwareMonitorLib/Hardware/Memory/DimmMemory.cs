// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using LibreHardwareMonitor.Hardware.Memory.Sensors;
using RAMSPDToolkit.SPD;
using RAMSPDToolkit.SPD.Interfaces;
using RAMSPDToolkit.SPD.Interop.Shared;

namespace LibreHardwareMonitor.Hardware.Memory;

internal sealed class DimmMemory : Hardware
{
    private readonly SpdThermalSensor _thermalSensor;

    public DimmMemory(SPDAccessor accessor, string name, Identifier identifier, ISettings settings)
        : base(name, identifier, settings)
    {
        //Only add thermal sensor if present
        if (accessor is IThermalSensor ts && ts.HasThermalSensor)
        {
            //Check which kind of RAM we have
            switch (accessor.MemoryType())
            {
                case SPDMemoryType.SPD_DDR4_SDRAM:
                case SPDMemoryType.SPD_DDR4E_SDRAM:
                case SPDMemoryType.SPD_LPDDR4_SDRAM:
                case SPDMemoryType.SPD_LPDDR4X_SDRAM:
                case SPDMemoryType.SPD_DDR5_SDRAM:
                case SPDMemoryType.SPD_LPDDR5_SDRAM:
                    _thermalSensor = new SpdThermalSensor($"内存条 #{accessor.Index}",
                                                          0,
                                                          SensorType.Temperature,
                                                          this,
                                                          settings,
                                                          accessor as IThermalSensor);
                    break;
            }
        }

        bool hasThermalSensor = _thermalSensor != null;

        //Add thermal sensor
        if (hasThermalSensor)
            ActivateSensor(_thermalSensor);

        //Add other sensors
        CreateSensors(accessor, hasThermalSensor);
    }

    public override HardwareType HardwareType => HardwareType.Memory;

    public override void Update()
    {
        _thermalSensor?.UpdateSensor();
    }

    private void CreateSensors(SPDAccessor accessor, bool hasThermalSensor)
    {
        if (accessor is DDR4Accessor ddr4)
        {
            CreateSensorsDDR4(ddr4, hasThermalSensor);
        }
        else if (accessor is DDR5Accessor ddr5)
        {
            CreateSensorsDDR5(ddr5, hasThermalSensor);
        }
    }

    private void CreateSensorsDDR4(DDR4Accessor accessor, bool hasThermalSensor)
    {
        if (hasThermalSensor)
        {
            //Temperature Resolution (fixed value)
            AddSensor("温度传感器分辨率", 1, false, SensorType.Temperature, accessor.TemperatureResolution);
            AddSensor("温度传感器下限", 2, false, SensorType.Temperature, accessor.ThermalSensorLowLimit);
            AddSensor("温度传感器上限", 3, false, SensorType.Temperature, accessor.ThermalSensorHighLimit);
            AddSensor("温度传感器临界值", 4, false, SensorType.Temperature, accessor.ThermalSensorCriticalLimit);
        }

        //Timings
        AddSensor("tCKAVGmin (最小时钟周期)", 20, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumCycleTime);
        AddSensor("tCKAVGmax (最大时钟周期)", 21, false, SensorType.Timing, (float)accessor.SDRAMTimings.MaximumCycleTime);
        AddSensor("tAA (CAS 延迟时间)", 22, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumCASLatencyTime);
        AddSensor("tRCD (RAS 到 CAS 延迟时间)", 23, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumRASToCASDelayTime);
        AddSensor("tRP (行预充电延迟时间)", 24, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumRowPrechargeDelayTime);
        AddSensor("tRAS (激活到预充电延迟时间)", 25, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumActiveToPrechargeDelayTime);
        AddSensor("tRC (激活到激活/刷新延迟时间)", 26, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumActiveToActiveRefreshDelayTime);
        AddSensor("tRFC1 (刷新恢复延迟时间)", 27, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumRefreshRecoveryDelayTime1);
        AddSensor("tRFC2 (刷新恢复延迟时间)", 28, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumRefreshRecoveryDelayTime2);
        AddSensor("tRFC4 (刷新恢复延迟时间)", 29, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumRefreshRecoveryDelayTime4);
        AddSensor("tFAW (四激活窗口时间)", 30, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumFourActivateWindowTime);
        AddSensor("tRRD_S (激活到激活延迟时间)", 31, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumActivateToActivateDelay_DiffGroup);
        AddSensor("tRRD_L (激活到激活延迟时间)", 32, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumActivateToActivateDelay_SameGroup);
        AddSensor("tCCD_L (CAS 到 CAS 延迟时间)", 33, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumCASToCASDelay_SameGroup);
        AddSensor("tWR (写恢复时间)", 34, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumWriteRecoveryTime);
        AddSensor("tWTR_S (写到读时间)", 35, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumWriteToReadTime_DiffGroup);
        AddSensor("tWTR_L (写到读时间)", 36, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumWriteToReadTime_SameGroup);

        //Data
        AddSensor("容量", 50, false, SensorType.Data, accessor.GetCapacity());
    }

    private void CreateSensorsDDR5(DDR5Accessor accessor, bool hasThermalSensor)
    {
        if (hasThermalSensor)
        {
            //Temperature Resolution (fixed value)
            AddSensor("温度传感器分辨率", 1, false, SensorType.Temperature, accessor.TemperatureResolution);
            AddSensor("温度传感器下限", 2, false, SensorType.Temperature, accessor.ThermalSensorLowLimit);
            AddSensor("温度传感器上限", 3, false, SensorType.Temperature, accessor.ThermalSensorHighLimit);
            AddSensor("温度传感器临界下限", 4, false, SensorType.Temperature, accessor.ThermalSensorCriticalLowLimit);
            AddSensor("温度传感器临界上限", 5, false, SensorType.Temperature, accessor.ThermalSensorCriticalHighLimit);
        }

        //Timings
        AddSensor("tCKAVGmin (最小时钟周期)", 20, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumCycleTime);
        AddSensor("tCKAVGmax (最大时钟周期)", 21, false, SensorType.Timing, (float)accessor.SDRAMTimings.MaximumCycleTime);
        AddSensor("tAA (CAS 延迟时间)", 22, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumCASLatencyTime);
        AddSensor("tRCD (RAS 到 CAS 延迟时间)", 23, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumRASToCASDelayTime);
        AddSensor("tRP (行预充电延迟时间)", 24, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumRowPrechargeDelayTime);
        AddSensor("tRAS (激活到预充电延迟时间)", 25, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumActiveToPrechargeDelayTime);
        AddSensor("tRC (激活到激活/刷新延迟时间)", 26, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumActiveToActiveRefreshDelayTime);
        AddSensor("tWR (写恢复时间)", 27, false, SensorType.Timing, (float)accessor.SDRAMTimings.MinimumWriteRecoveryTime);
        AddSensor("tRFC1 (普通刷新恢复时间)", 28, false, SensorType.Timing, (float)accessor.SDRAMTimings.NormalRefreshRecoveryTime);
        AddSensor("tRFC2 (精细粒度刷新恢复时间)", 29, false, SensorType.Timing, (float)accessor.SDRAMTimings.FineGranularityRefreshRecoveryTime);
        AddSensor("tRFCsb (同 Bank 刷新恢复时间)", 30, false, SensorType.Timing, (float)accessor.SDRAMTimings.SameBankRefreshRecoveryTime);
        AddSensor("tRFC1_dlr (普通刷新恢复时间 3DS)", 31, false, SensorType.Timing, (float)accessor.SDRAMTimings.NormalRefreshRecoveryTime_3DSDifferentLogicalRank);
        AddSensor("tRFC2_dlr (精细粒度刷新恢复时间 3DS)", 32, false, SensorType.Timing, (float)accessor.SDRAMTimings.FineGranularityRefreshRecoveryTime_3DSDifferentLogicalRank);
        AddSensor("tRFCsb_dlr (同 Bank 刷新恢复时间 3DS)", 33, false, SensorType.Timing, (float)accessor.SDRAMTimings.SameBankRefreshRecoveryTime_3DSDifferentLogicalRank);

        //Data
        AddSensor("容量", 50, false, SensorType.Data, accessor.GetCapacity());
    }

    private void AddSensor(string name, int index, bool defaultHidden, SensorType sensorType, float value)
    {
        var sensor = new Sensor(name, index, defaultHidden, sensorType, this, null, _settings)
        {
            Value = value,
        };

        ActivateSensor(sensor);
    }
}
