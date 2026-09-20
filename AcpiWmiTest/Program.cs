// 验证程序：仅开启 IsAcpiWmiEnabled，读取 ACPI WMI 私有通道的风扇转速。
// 必须以管理员身份运行（WMI 动态类枚举需要管理员权限）。

using System;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace AcpiWmiTest;

internal sealed class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }

    public void VisitParameter(IParameter parameter) { }
}

internal static class Program
{
    private static void Main()
    {
        Computer computer = new();
        computer.IsAcpiWmiEnabled = true;
        computer.Open();

        Console.WriteLine("Detected hardware:");
        foreach (IHardware hw in computer.Hardware)
            Console.WriteLine($"  {hw.Name} [{hw.HardwareType}] {hw.Identifier}");

        UpdateVisitor visitor = new();
        for (int i = 0; i < 10; i++)
        {
            computer.Accept(visitor);

            Console.WriteLine($"--- sample {i} ---");
            foreach (IHardware hw in computer.Hardware)
            {
                foreach (ISensor sensor in hw.Sensors)
                    Console.WriteLine($"  {hw.Name} / {sensor.Name}: {sensor.Value} ({sensor.SensorType})");
            }

            Thread.Sleep(1000);
        }

        Console.WriteLine("Report:");
        Console.WriteLine(computer.GetReport());

        computer.Close();
    }
}
