using System;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace HardwareInfoApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DisplayHardwareInfo();
        }

        private void DisplayHardwareInfo()
        {
            AddSection("CPU Information", GetCPUInfo());
            AddSection("GPU Information", GetGPUInfo());
            AddSection("RAM Information", GetRAMInfo());
            AddSection("Disk Information", GetDiskInfo());
            AddSection("Motherboard Information", GetMotherboardInfo());
        }

        private void AddSection(string title, string content)
        {
            InfoPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.DeepSkyBlue,
                Margin = new Thickness(0, 10, 0, 5)
            });

            InfoPanel.Children.Add(new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 15)
            });
        }

        private string GetCPUInfo()
        {
            var sb = new StringBuilder();
            using (var searcher = new ManagementObjectSearcher("select * from Win32_Processor"))
            {
                foreach (var obj in searcher.Get())
                {
                    sb.AppendLine($"Name: {obj["Name"]}");
                    sb.AppendLine($"Cores: {obj["NumberOfCores"]}");
                    sb.AppendLine($"Logical Processors: {obj["NumberOfLogicalProcessors"]}");
                    sb.AppendLine($"Max Clock Speed: {obj["MaxClockSpeed"]} MHz");
                }
            }
            return sb.ToString();
        }

        private string GetGPUInfo()
        {
            var sb = new StringBuilder();
            using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
            {
                foreach (var obj in searcher.Get())
                {
                    double ramGB = Math.Round(Convert.ToDouble(obj["AdapterRAM"]) / (1024 * 1024 * 1024), 2);
                    sb.AppendLine($"Name: {obj["Name"]}");
                    sb.AppendLine($"Driver Version: {obj["DriverVersion"]}");
                    sb.AppendLine($"Video Processor: {obj["VideoProcessor"]}");
                    sb.AppendLine($"VRAM: {ramGB} GB");
                }
            }
            return sb.ToString();
        }

        private string GetRAMInfo()
        {
            using (var searcher = new ManagementObjectSearcher("select * from Win32_ComputerSystem"))
            {
                foreach (var obj in searcher.Get())
                {
                    double totalMemory = Convert.ToDouble(obj["TotalPhysicalMemory"]) / (1024 * 1024 * 1024);
                    return $"Installed RAM: {Math.Round(totalMemory, 2)} GB";
                }
            }
            return "Unknown";
        }

        private string GetDiskInfo()
        {
            var sb = new StringBuilder();
            using (var searcher = new ManagementObjectSearcher("select * from Win32_DiskDrive"))
            {
                foreach (var obj in searcher.Get())
                {
                    double sizeGB = Math.Round(Convert.ToDouble(obj["Size"]) / (1024 * 1024 * 1024), 2);
                    sb.AppendLine($"Model: {obj["Model"]}");
                    sb.AppendLine($"Interface: {obj["InterfaceType"]}");
                    sb.AppendLine($"Size: {sizeGB} GB");
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        private string GetMotherboardInfo()
        {
            var sb = new StringBuilder();
            using (var searcher = new ManagementObjectSearcher("select * from Win32_BaseBoard"))
            {
                foreach (var obj in searcher.Get())
                {
                    sb.AppendLine($"Manufacturer: {obj["Manufacturer"]}");
                    sb.AppendLine($"Product: {obj["Product"]}");
                    sb.AppendLine($"Serial Number: {obj["SerialNumber"]}");
                }
            }
            return sb.ToString();
        }
    }
}
