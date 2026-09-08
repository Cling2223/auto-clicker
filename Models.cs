using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace LightweightAutoClicker
{
    public enum ClickButton
    {
        Left,
        Right,
        Middle
    }

    public sealed class ClickPointConfig
    {
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public ClickButton Button { get; set; }
        public int IntervalMs { get; set; }

        public ClickPointConfig()
        {
            Enabled = true;
            Name = "点击点";
            Button = ClickButton.Left;
            IntervalMs = 1000;
        }

        public ClickPointConfig Copy()
        {
            return new ClickPointConfig
            {
                Enabled = Enabled,
                Name = Name,
                X = X,
                Y = Y,
                Button = Button,
                IntervalMs = IntervalMs
            };
        }
    }

    public sealed class ClickGroupConfig
    {
        public bool Enabled { get; set; }
        public string Name { get; set; }

        [XmlArrayItem("Point")]
        public List<ClickPointConfig> Points { get; set; }

        public ClickGroupConfig()
        {
            Enabled = true;
            Name = "分组";
            Points = new List<ClickPointConfig>();
        }

        public ClickGroupConfig Copy()
        {
            var copy = new ClickGroupConfig { Enabled = Enabled, Name = Name };
            foreach (ClickPointConfig point in Points)
                copy.Points.Add(point.Copy());
            return copy;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    [Serializable]
    public sealed class AppSettings
    {
        public string TargetProcessName { get; set; }
        public string TargetWindowTitle { get; set; }
        public bool AlwaysOnTop { get; set; }

        [XmlArrayItem("Point")]
        public List<ClickPointConfig> Points { get; set; }

        [XmlArrayItem("Group")]
        public List<ClickGroupConfig> Groups { get; set; }

        public AppSettings()
        {
            TargetProcessName = string.Empty;
            TargetWindowTitle = string.Empty;
            Points = new List<ClickPointConfig>();
            Groups = new List<ClickGroupConfig>();
        }
    }

    internal sealed class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string Title { get; set; }

        public override string ToString()
        {
            return string.Format("{0} — {1} (PID {2})", Title, ProcessName, ProcessId);
        }
    }
}
