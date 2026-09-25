// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Utilities;

namespace LibreHardwareMonitor.UI;

public class SensorNotifyIcon : IDisposable
{
    private readonly UnitManager _unitManager;
    private readonly NotifyIconAdv _notifyIcon;
    private readonly Bitmap _bitmap;
    private readonly Graphics _graphics;
    private Color _color;
    private Color _darkColor;
    private Brush _brush;
    private Brush _darkBrush;
    private readonly Pen _pen;
    private readonly Font _font;
    private readonly Font _smallFont;

    public SensorNotifyIcon(SystemTray sensorSystemTray, ISensor sensor, PersistentSettings settings, UnitManager unitManager)
    {
        _unitManager = unitManager;
        Sensor = sensor;
        _notifyIcon = new NotifyIconAdv();

        Color defaultColor = Color.White;
        if (sensor.SensorType == SensorType.Load || sensor.SensorType == SensorType.Control || sensor.SensorType == SensorType.Level)
            defaultColor = Color.FromArgb(0xff, 0x70, 0x8c, 0xf1);

        Color = settings.GetValue(new Identifier(sensor.Identifier, "traycolor").ToString(), defaultColor);

        _pen = new Pen(Color.FromArgb(96, Color.Black));
        ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
        ToolStripItem hideShowItem = new ToolStripMenuItem("隐藏/显示");
        hideShowItem.Click += delegate
        {
            sensorSystemTray.SendHideShowCommand();
        };
        contextMenuStrip.Items.Add(hideShowItem);
        contextMenuStrip.Items.Add(new ToolStripSeparator());
        ToolStripItem removeItem = new ToolStripMenuItem("移除传感器");
        removeItem.Click += delegate
        {
            sensorSystemTray.Remove(Sensor);
        };
        contextMenuStrip.Items.Add(removeItem);
        ToolStripItem colorItem = new ToolStripMenuItem("更改颜色...");
        colorItem.Click += delegate
        {
            ColorDialog dialog = new ColorDialog { Color = Color };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                Color = dialog.Color;
                settings.SetValue(new Identifier(sensor.Identifier,
                                                 "traycolor").ToString(), Color);
            }
        };
        contextMenuStrip.Items.Add(colorItem);
        contextMenuStrip.Items.Add(new ToolStripSeparator());
        ToolStripItem exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += delegate
        {
            sensorSystemTray.SendExitCommand();
        };
        contextMenuStrip.Items.Add(exitItem);
        _notifyIcon.ContextMenuStrip = contextMenuStrip;
        _notifyIcon.DoubleClick += delegate
        {
            sensorSystemTray.SendHideShowCommand();
        };

        // get the default dpi to create an icon with the correct size
        float dpiX, dpiY;
        using (Bitmap b = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
        {
            dpiX = b.HorizontalResolution;
            dpiY = b.VerticalResolution;
        }

        // adjust the size of the icon to current dpi (default is 16x16 at 96 dpi)
        int width = (int)Math.Round(16 * dpiX / 96);
        int height = (int)Math.Round(16 * dpiY / 96);

        // make sure it does never get smaller than 16x16
        width = width < 16 ? 16 : width;
        height = height < 16 ? 16 : height;

        // 调整字号到图标尺寸。统一取 11px：托盘槽位由系统固定为 16x16（SM_CXSMICON），
        // 该字号下 "2.0"（风扇）与 "15"（功耗）都能完整显示，各图标字号一致、观感协调。
        FontFamily family = SystemFonts.MessageBoxFont.FontFamily;
        float baseSize;
        switch (family.Name)
        {
            case "Segoe UI": baseSize = 11; break;
            case "Tahoma": baseSize = 11; break;
            default: baseSize = 11; break;
        }

        _font = new Font(family, baseSize * width / 16.0f, GraphicsUnit.Pixel);
        _smallFont = new Font(family, 0.75f * baseSize * width / 16.0f, GraphicsUnit.Pixel);

        _bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        _graphics = Graphics.FromImage(_bitmap);
        if (Environment.OSVersion.Version.Major > 5)
        {
            _graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            _graphics.SmoothingMode = SmoothingMode.HighQuality;
        }
    }

    public ISensor Sensor { get; }

    public Color Color
    {
        get { return _color; }
        set
        {
            _color = value;
            _darkColor = Color.FromArgb(255, _color.R / 3, _color.G / 3, _color.B / 3);
            Brush brush = _brush;
            _brush = new SolidBrush(_color);
            brush?.Dispose();
            Brush darkBrush = _darkBrush;
            _darkBrush = new SolidBrush(_darkColor);
            darkBrush?.Dispose();
        }
    }

    public void Dispose()
    {
        Icon icon = _notifyIcon.Icon;
        _notifyIcon.Icon = null;
        icon?.Destroy();
        _notifyIcon.Dispose();

        _brush?.Dispose();
        _darkBrush?.Dispose();
        _pen.Dispose();
        _graphics.Dispose();
        _bitmap.Dispose();
        _font.Dispose();
        _smallFont.Dispose();
    }

    private string GetString()
    {
        if (!Sensor.Value.HasValue)
            return "-";

        switch (Sensor.SensorType)
        {
            case SensorType.Temperature:
                return _unitManager.TemperatureUnit == TemperatureUnit.Fahrenheit ? $"{UnitManager.CelsiusToFahrenheit(Sensor.Value):F0}" : $"{Sensor.Value:F0}";
            case SensorType.TimeSpan:
                return $"{TimeSpan.FromSeconds(Sensor.Value.Value):g}";
            case SensorType.Timing:
                return $"{Sensor.Value.Value:F3}";
            case SensorType.Clock:
            case SensorType.Fan:
            case SensorType.Flow:
                return $"{1e-3f * Sensor.Value:F1}";
            case SensorType.Voltage:
            case SensorType.Current:
            case SensorType.SmallData:
            case SensorType.Factor:
            case SensorType.Throughput:
            case SensorType.Conductivity:
                return $"{Sensor.Value:F1}";
            case SensorType.Control:
            case SensorType.Frequency:
            case SensorType.Level:
            case SensorType.Power:
            case SensorType.Data:
            case SensorType.Load:
            case SensorType.Energy:
            case SensorType.Noise:
            case SensorType.Humidity:
                return $"{Sensor.Value:F0}";
            default:
                return "-";
        }
    }

    private Icon CreateTransparentIcon()
    {
        string text = GetString();

        // 依据实际字宽选择字号，避免数字超出图标边界被裁掉。
        // 不能只数数字个数：像 "2.1" 这类值数字虽少，但多一个小数点就更宽。
        Font font = _font;
        if (MeasureTextWidth(text, font) > _bitmap.Width)
        {
            font = _smallFont;

            float smallWidth = MeasureTextWidth(text, font);
            if (smallWidth > _bitmap.Width)
            {
                // 留 1px 余量，避免缩放后因字体度量非线性仍差一点点而贴边。
                float size = Math.Max(1f, _smallFont.Size * (_bitmap.Width - 1) / smallWidth);
                font = new Font(_smallFont.FontFamily, size, GraphicsUnit.Pixel);
            }
        }

        try
        {
            _graphics.Clear(Color.Black);
            Rectangle bounds = new Rectangle(Point.Empty, _bitmap.Size);
            // NoPadding 让文字使用完整的 16px 宽度，避免 GDI 预留内边距把数字挤到边界外。
            TextRenderer.DrawText(_graphics, text, font,
                bounds, Color.White, Color.Black,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            BitmapData data = _bitmap.LockBits(
                new Rectangle(0, 0, _bitmap.Width, _bitmap.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            IntPtr Scan0 = data.Scan0;

            int numBytes = _bitmap.Width * _bitmap.Height * 4;
            byte[] bytes = new byte[numBytes];
            Marshal.Copy(Scan0, bytes, 0, numBytes);
            _bitmap.UnlockBits(data);

            byte red, green, blue;
            for (int i = 0; i < bytes.Length; i += 4)
            {
                blue = bytes[i];
                green = bytes[i + 1];
                red = bytes[i + 2];

                bytes[i] = _color.B;
                bytes[i + 1] = _color.G;
                bytes[i + 2] = _color.R;
                bytes[i + 3] = (byte)(0.3 * red + 0.59 * green + 0.11 * blue);
            }

            return IconFactory.Create(bytes, _bitmap.Width, _bitmap.Height,
                PixelFormat.Format32bppArgb);
        }
        finally
        {
            // 仅释放本次临时创建的字号，缓存的两个字号由 Dispose 统一释放。
            if (font != _font && font != _smallFont)
                font.Dispose();
        }
    }

    /// <summary>
    /// 测量文字的实际渲染宽度（不含 TextRenderer 的内边距），用于判断是否放得进图标。
    /// </summary>
    private float MeasureTextWidth(string text, Font font)
    {
        return _graphics.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic).Width;
    }

    private Icon CreatePercentageIcon()
    {
        try
        {
            _graphics.Clear(Color.Transparent);
        }
        catch (ArgumentException)
        {
            _graphics.Clear(Color.Black);
        }
        _graphics.FillRectangle(_darkBrush, 0.5f, -0.5f, _bitmap.Width - 2, _bitmap.Height);
        float value = Sensor.Value.GetValueOrDefault();
        float y = (float)(_bitmap.Height * 0.01f) * (100 - value);
        _graphics.FillRectangle(_brush, 0.5f, -0.5f + y, _bitmap.Width - 2, _bitmap.Height - y);
        _graphics.DrawRectangle(_pen, 1, 0, _bitmap.Width - 3, _bitmap.Height - 1);

        return IconFactory.Create(_bitmap);
    }

    public void Update()
    {
        Icon icon = _notifyIcon.Icon;

        switch (Sensor.SensorType)
        {
            case SensorType.Load:
            case SensorType.Control:
            case SensorType.Level:
                _notifyIcon.Icon = CreatePercentageIcon();
                break;
            default:
                _notifyIcon.Icon = CreateTransparentIcon();
                break;
        }

        icon?.Destroy();

        string format = "";
        switch (Sensor.SensorType)
        {
            case SensorType.Voltage: format = "\n{0}: {1:F2} V"; break;
            case SensorType.Current: format = "\n{0}: {1:F2} A"; break;
            case SensorType.Clock: format = "\n{0}: {1:F0} MHz"; break;
            case SensorType.Load: format = "\n{0}: {1:F1} %"; break;
            case SensorType.Temperature: format = "\n{0}: {1:F1} °C"; break;
            case SensorType.Fan: format = "\n{0}: {1:F0} RPM"; break;
            case SensorType.Flow: format = "\n{0}: {1:F0} L/h"; break;
            case SensorType.Control: format = "\n{0}: {1:F1} %"; break;
            case SensorType.Level: format = "\n{0}: {1:F1} %"; break;
            case SensorType.Power: format = "\n{0}: {1:F0} W"; break;
            case SensorType.Data: format = "\n{0}: {1:F0} GB"; break;
            case SensorType.Factor: format = "\n{0}: {1:F3} GB"; break;
            case SensorType.Energy: format = "\n{0}: {1:F0} mWh"; break;
            case SensorType.Noise: format = "\n{0}: {1:F0} dBA"; break;
            case SensorType.Conductivity: format = "\n{0}: {1:F1} µS/cm"; break;
            case SensorType.Humidity: format = "\n{0}: {1:F0} %"; break;
            case SensorType.Timing: format = "\n{0}: {1:F3} ns"; break;
        }

        string formattedValue = string.Format(format, Sensor.Name, Sensor.Value);

        if (Sensor.SensorType == SensorType.Temperature && _unitManager.TemperatureUnit == TemperatureUnit.Fahrenheit)
        {
            format = "\n{0}: {1:F1} °F";
            formattedValue = string.Format(format, Sensor.Name, UnitManager.CelsiusToFahrenheit(Sensor.Value));
        }

        string hardwareName = Sensor.Hardware.Name;
        hardwareName = hardwareName.Substring(0, Math.Min(63 - formattedValue.Length, hardwareName.Length));
        string text = hardwareName + formattedValue;
        if (text.Length > 63)
            text = null;

        _notifyIcon.Text = text;
        _notifyIcon.Visible = true;
    }
}
