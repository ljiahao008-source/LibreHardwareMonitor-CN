// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Utilities;

public class PersistentSettings : ISettings
{
    private readonly IDictionary<string, string> _settings = new Dictionary<string, string>();

    // 配置字典可能被非 UI 线程访问（例如系统从睡眠恢复时，SystemEvents 线程会触发
    // Computer.Reset() → 硬件 Close() → 传感器把历史写回配置），而 UI 线程会定时保存
    // 并遍历该字典。所有访问都必须加锁，否则会抛 InvalidOperationException（集合已修改）。
    private readonly object _settingsLock = new object();

    private volatile bool _dirty;

    // 每次修改递增，用于判定 Save 期间是否又有新改动，避免把「新改动」误标为已保存。
    private long _version;

    /// <summary>
    /// Indicates whether settings were changed since the last <see cref="Save"/> call.
    /// </summary>
    public bool IsDirty
    {
        get { return _dirty; }
    }

    public void Load(string fileName)
    {
        XmlDocument doc = new XmlDocument();
        try
        {
            doc.Load(fileName);
        }
        catch
        {
            try
            {
                File.Delete(fileName);
            }
            catch { }

            string backupFileName = fileName + ".backup";
            try
            {
                doc.Load(backupFileName);
            }
            catch
            {
                try
                {
                    File.Delete(backupFileName);
                }
                catch { }

                return;
            }
        }

        XmlNodeList list = doc.GetElementsByTagName("appSettings");
        lock (_settingsLock)
        {
            foreach (XmlNode node in list)
            {
                XmlNode parent = node.ParentNode;
                if (parent != null && parent.Name == "configuration" && parent.ParentNode is XmlDocument)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name == "add")
                        {
                            XmlAttributeCollection attributes = child.Attributes;
                            XmlAttribute keyAttribute = attributes["key"];
                            XmlAttribute valueAttribute = attributes["value"];
                            if (keyAttribute != null && valueAttribute != null && keyAttribute.Value != null)
                            {
                                _settings.Add(keyAttribute.Value, valueAttribute.Value);
                            }
                        }
                    }
                }
            }
        }
    }

    public void Save(string fileName)
    {
        // 在锁内取快照，随后在锁外做耗时的 XML 构建与文件写入，
        // 避免长时间持锁阻塞传感器线程写配置。
        KeyValuePair<string, string>[] snapshot;
        long snapshotVersion;

        lock (_settingsLock)
        {
            snapshot = _settings.ToArray();
            snapshotVersion = _version;
        }

        XmlDocument doc = new XmlDocument();
        doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
        XmlElement configuration = doc.CreateElement("configuration");
        doc.AppendChild(configuration);
        XmlElement appSettings = doc.CreateElement("appSettings");
        configuration.AppendChild(appSettings);
        foreach (KeyValuePair<string, string> keyValuePair in snapshot)
        {
            XmlElement add = doc.CreateElement("add");
            add.SetAttribute("key", keyValuePair.Key);
            add.SetAttribute("value", keyValuePair.Value);
            appSettings.AppendChild(add);
        }

        byte[] file;
        using (var memory = new MemoryStream())
        {
            using (var writer = new StreamWriter(memory, Encoding.UTF8))
            {
                doc.Save(writer);
            }
            file = memory.ToArray();
        }

        string backupFileName = fileName + ".backup";
        if (File.Exists(fileName))
        {
            try
            {
                File.Delete(backupFileName);
            }
            catch { }
            try
            {
                File.Move(fileName, backupFileName);
            }
            catch { }
        }

        using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
        {
            stream.Write(file, 0, file.Length);
        }

        try
        {
            File.Delete(backupFileName);
        }
        catch { }

        lock (_settingsLock)
        {
            // 仅当快照之后没有新的改动时才清除脏标记；否则保留，供下次继续保存。
            if (_version == snapshotVersion)
                _dirty = false;
        }
    }

    public bool Contains(string name)
    {
        lock (_settingsLock)
        {
            return _settings.ContainsKey(name);
        }
    }

    public void SetValue(string name, string value)
    {
        lock (_settingsLock)
        {
            _settings[name] = value;
            _version++;
            _dirty = true;
        }
    }

    public string GetValue(string name, string value)
    {
        lock (_settingsLock)
        {
            if (_settings.TryGetValue(name, out string result))
                return result;
        }


        return value;
    }

    public void Remove(string name)
    {
        lock (_settingsLock)
        {
            if (_settings.Remove(name))
            {
                _version++;
                _dirty = true;
            }
        }
    }

    public void SetValue(string name, int value)
    {
        lock (_settingsLock)
        {
            _settings[name] = value.ToString();
            _version++;
            _dirty = true;
        }
    }

    public int GetValue(string name, int value)
    {
        lock (_settingsLock)
        {
            if (_settings.TryGetValue(name, out string str))
            {
                if (int.TryParse(str, out int parsedValue))
                    return parsedValue;
            }
        }

        return value;
    }

    public void SetValue(string name, float value)
    {
        lock (_settingsLock)
        {
            _settings[name] = value.ToString(CultureInfo.InvariantCulture);
            _version++;
            _dirty = true;
        }
    }

    public float GetValue(string name, float value)
    {
        lock (_settingsLock)
        {
            if (_settings.TryGetValue(name, out string str))
            {
                if (float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedValue))
                    return parsedValue;
            }
        }

        return value;

    }

    public double GetValue(string name, double value)
    {
        lock (_settingsLock)
        {
            if (_settings.TryGetValue(name, out string str))
            {
                if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedValue))
                    return parsedValue;
            }
        }

        return value;
    }
        
    public void SetValue(string name, bool value)
    {
        lock (_settingsLock)
        {
            _settings[name] = value ? "true" : "false";
            _version++;
            _dirty = true;
        }
    }

    public bool GetValue(string name, bool value)
    {
        lock (_settingsLock)
        {
            if (_settings.TryGetValue(name, out string str))
            {
                return str == "true";
            }
        }

        return value;
    }

    public void SetValue(string name, Color color)
    {
        lock (_settingsLock)
        {
            _settings[name] = color.ToArgb().ToString("X8");
            _version++;
            _dirty = true;
        }
    }

    public Color GetValue(string name, Color value)
    {
        lock (_settingsLock)
        {
            if (_settings.TryGetValue(name, out string str))
            {
                if (int.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsedValue))
                    return Color.FromArgb(parsedValue);
            }
        }

        return value;
    }
}