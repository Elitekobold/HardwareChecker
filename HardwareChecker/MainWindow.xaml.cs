using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace HardwareInfoApp
{
    public partial class MainWindow : Window
    {
        private sealed class SavedComputer
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public string Ort { get; set; }
            public string CpuName { get; set; }
            public int? CpuCores { get; set; }
            public string CpuManufacturer { get; set; }
            public string GpuName { get; set; }
            public string GpuManufacturer { get; set; }
            public string MotherboardName { get; set; }
            public string MotherboardManufacturer { get; set; }
            public string Ram { get; set; }
            public string RamSpeed { get; set; }
            public string Memory { get; set; }
            public string DisplayName => $"#{Id} - {Name}";
        }

        // Hardware-Klassenvariablen
        private string CPUName;
        private int CPUCores;
        private string CPUManufacturer;
        private string CPUVersion;

        private string GPUName;
        private string GPUManufacturer;
        private string GPUVersion;

        private double RAMSize;
        private string RAMSpeed;

        private string MotherboardModel;
        private string MotherboardManufacturer;

        private string DiskModel;
        private string MemorySummary;

        // Computer / DB-Klassenvariablen
        private string ComputerName;
        private string Ort;
        private string DbPath;
        private string ConnectionString;

        // SQL (Schema + Upserts)
        private const string SqlEnableFk = "PRAGMA foreign_keys = ON;";

        private const string SqlSchema = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS cpu (
  name        TEXT PRIMARY KEY,
  kerne       INTEGER NOT NULL CHECK (kerne > 0),
  hersteller  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS gpu (
  name        TEXT PRIMARY KEY,
  hersteller  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS motherboard (
  name        TEXT PRIMARY KEY,
  hersteller  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS computer (
  id               INTEGER PRIMARY KEY,
  name             TEXT NOT NULL,
  ort              TEXT,

  cpu_name         TEXT,
  gpu_name         TEXT,
  motherboard_name TEXT,

  ram              TEXT,
  ram_speed        TEXT,
  memory           TEXT,

  FOREIGN KEY (cpu_name) REFERENCES cpu(name)
    ON UPDATE CASCADE ON DELETE SET NULL,
  FOREIGN KEY (gpu_name) REFERENCES gpu(name)
    ON UPDATE CASCADE ON DELETE SET NULL,
  FOREIGN KEY (motherboard_name) REFERENCES motherboard(name)
    ON UPDATE CASCADE ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_computer_cpu  ON computer(cpu_name);
CREATE INDEX IF NOT EXISTS idx_computer_gpu  ON computer(gpu_name);
CREATE INDEX IF NOT EXISTS idx_computer_mobo ON computer(motherboard_name);
";

        private const string SqlUpsertCpu = @"
INSERT INTO cpu (name, kerne, hersteller)
VALUES ($name, $kerne, $hersteller)
ON CONFLICT(name) DO UPDATE SET
  kerne = excluded.kerne,
  hersteller = excluded.hersteller;";

        private const string SqlUpsertGpu = @"
INSERT INTO gpu (name, hersteller)
VALUES ($name, $hersteller)
ON CONFLICT(name) DO UPDATE SET
  hersteller = excluded.hersteller;";

        private const string SqlUpsertMotherboard = @"
INSERT INTO motherboard (name, hersteller)
VALUES ($name, $hersteller)
ON CONFLICT(name) DO UPDATE SET
  hersteller = excluded.hersteller;";

        private const string SqlInsertComputer = @"
INSERT INTO computer
  (name, ort, cpu_name, gpu_name, motherboard_name, ram, ram_speed, memory)
VALUES
  ($name, $ort, $cpu_name, $gpu_name, $motherboard_name, $ram, $ram_speed, $memory);
SELECT last_insert_rowid();";

        private const string SqlSelectAllComputers = @"
SELECT
    c.id,
    c.name,
    c.ort,
    c.ram,
    c.ram_speed,
    c.memory,
    c.cpu_name,
    cpu.kerne,
    cpu.hersteller AS cpu_hersteller,
    c.gpu_name,
    gpu.hersteller AS gpu_hersteller,
    c.motherboard_name,
    motherboard.hersteller AS motherboard_hersteller
FROM computer c
LEFT JOIN cpu ON cpu.name = c.cpu_name
LEFT JOIN gpu ON gpu.name = c.gpu_name
LEFT JOIN motherboard ON motherboard.name = c.motherboard_name
ORDER BY c.id DESC;";

        public MainWindow()
        {
            InitializeComponent();

            ComputerName = Environment.MachineName;
            Ort = "";

            ShowCurrentHardwareDetails();
        }

        private void ShowCurrentHardwareDetails()
        {
            InfoPanel.Children.Clear();
            DetailTitleText.Text = "System Hardware Information";
            BackToListButton.Visibility = Visibility.Collapsed;
            AddSection("CPU Information", GetCPUInfo());
            AddSection("GPU Information", GetGPUInfo());
            AddSection("RAM Information", GetRAMInfo());
            AddSection("Disk Information", GetDiskInfo());
            AddSection("Motherboard Information", GetMotherboardInfo());

            DetailViewGrid.Visibility = Visibility.Visible;
            ListViewGrid.Visibility = Visibility.Collapsed;
        }

        private void ShowSavedComputerDetails(SavedComputer computer)
        {
            InfoPanel.Children.Clear();
            DetailTitleText.Text = $"Gespeicherter Rechner: {ValueOrUnknown(computer.Name)}";
            BackToListButton.Visibility = Visibility.Visible;

            var cpuText = new StringBuilder();
            cpuText.AppendLine($"Name: {ValueOrUnknown(computer.CpuName)}");
            cpuText.AppendLine($"Manufacturer: {ValueOrUnknown(computer.CpuManufacturer)}");
            cpuText.AppendLine($"Cores: {(computer.CpuCores.HasValue ? computer.CpuCores.Value.ToString() : "Unknown")}");

            var gpuText = new StringBuilder();
            gpuText.AppendLine($"Name: {ValueOrUnknown(computer.GpuName)}");
            gpuText.AppendLine($"Manufacturer: {ValueOrUnknown(computer.GpuManufacturer)}");

            var ramText = $"Installed RAM: {ValueOrUnknown(computer.Ram)}\nRAM Speed: {ValueOrUnknown(computer.RamSpeed)}";

            var diskText = $"Memory: {ValueOrUnknown(computer.Memory)}";

            var moboText = new StringBuilder();
            moboText.AppendLine($"Manufacturer: {ValueOrUnknown(computer.MotherboardManufacturer)}");
            moboText.AppendLine($"Product: {ValueOrUnknown(computer.MotherboardName)}");

            var metaText = new StringBuilder();
            metaText.AppendLine($"Computer Name: {ValueOrUnknown(computer.Name)}");
            metaText.AppendLine($"Ort: {ValueOrUnknown(computer.Ort)}");
            metaText.AppendLine($"Datenbank-ID: {computer.Id}");

            AddSection("Computer", metaText.ToString());
            AddSection("CPU Information", cpuText.ToString());
            AddSection("GPU Information", gpuText.ToString());
            AddSection("RAM Information", ramText);
            AddSection("Disk Information", diskText);
            AddSection("Motherboard Information", moboText.ToString());

            DetailViewGrid.Visibility = Visibility.Visible;
            ListViewGrid.Visibility = Visibility.Collapsed;
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
                    CPUName = obj["Name"]?.ToString();
                    CPUManufacturer = obj["Manufacturer"]?.ToString();
                    CPUCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                    CPUVersion = obj["ProcessorId"]?.ToString();

                    sb.AppendLine($"Name: {CPUName}");
                    sb.AppendLine($"Manufacturer: {CPUManufacturer}");
                    sb.AppendLine($"Cores: {CPUCores}");
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
                    GPUName = obj["Name"]?.ToString();
                    GPUVersion = obj["DriverVersion"]?.ToString();
                    GPUManufacturer = obj["AdapterCompatibility"]?.ToString();

                    double ramGB = 0;
                    if (obj["AdapterRAM"] != null)
                    {
                        ramGB = Math.Round(Convert.ToDouble(obj["AdapterRAM"]) / (1024 * 1024 * 1024), 2);
                    }

                    sb.AppendLine($"Name: {GPUName}");
                    sb.AppendLine($"Manufacturer: {GPUManufacturer}");
                    sb.AppendLine($"Driver Version: {GPUVersion}");
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
                    double totalMemory = Convert.ToDouble(obj["TotalPhysicalMemory"] ?? 0) / (1024 * 1024 * 1024);
                    RAMSize = Math.Round(totalMemory, 2);
                    break;
                }
            }

            var speeds = new List<int>();
            using (var searcher = new ManagementObjectSearcher("select Speed from Win32_PhysicalMemory"))
            {
                foreach (var obj in searcher.Get())
                {
                    if (obj["Speed"] != null && int.TryParse(obj["Speed"].ToString(), out var s))
                    {
                        speeds.Add(s);
                    }
                }
            }

            RAMSpeed = speeds.Count > 0 ? $"{speeds.Max()} MHz" : "Unknown";

            return $"Installed RAM: {RAMSize} GB\nRAM Speed: {RAMSpeed}";
        }

        private string GetDiskInfo()
        {
            var sb = new StringBuilder();
            var memoryParts = new List<string>();

            using (var searcher = new ManagementObjectSearcher("select * from Win32_DiskDrive"))
            {
                foreach (var obj in searcher.Get())
                {
                    double sizeGB = 0;
                    if (obj["Size"] != null)
                    {
                        sizeGB = Math.Round(Convert.ToDouble(obj["Size"]) / (1024 * 1024 * 1024), 2);
                    }

                    var model = obj["Model"]?.ToString();
                    var iface = obj["InterfaceType"]?.ToString();

                    sb.AppendLine($"Model: {model}");
                    sb.AppendLine($"Interface: {iface}");
                    sb.AppendLine($"Size: {sizeGB} GB");
                    sb.AppendLine();

                    if (!string.IsNullOrWhiteSpace(model))
                    {
                        memoryParts.Add($"{model} ({sizeGB} GB)");
                    }
                }
            }

            DiskModel = memoryParts.FirstOrDefault() ?? "Unknown";
            MemorySummary = memoryParts.Count > 0 ? string.Join(" + ", memoryParts) : "Unknown";

            return sb.ToString();
        }

        private string GetMotherboardInfo()
        {
            var sb = new StringBuilder();
            using (var searcher = new ManagementObjectSearcher("select * from Win32_BaseBoard"))
            {
                foreach (var obj in searcher.Get())
                {
                    MotherboardManufacturer = obj["Manufacturer"]?.ToString();
                    MotherboardModel = obj["Product"]?.ToString();

                    sb.AppendLine($"Manufacturer: {MotherboardManufacturer}");
                    sb.AppendLine($"Product: {MotherboardModel}");
                    sb.AppendLine($"Serial Number: {obj["SerialNumber"]}");
                }
            }
            return sb.ToString();
        }

        // DB Helpers
        private void EnsureDbPathAndConnection()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            DbPath = Path.Combine(appDir, "app.db");

            var dir = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
        }

        private void EnsureSchema(SqliteConnection con, SqliteTransaction tx = null)
        {
            using (var fk = con.CreateCommand())
            {
                fk.Transaction = tx;
                fk.CommandText = SqlEnableFk;
                fk.ExecuteNonQuery();
            }

            using (var cmdSchema = con.CreateCommand())
            {
                cmdSchema.Transaction = tx;
                cmdSchema.CommandText = SqlSchema;
                cmdSchema.ExecuteNonQuery();
            }
        }

        private static object DbOrNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;
        }

        private static string ValueOrUnknown(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
        }

        private List<SavedComputer> LoadAllComputers()
        {
            EnsureDbPathAndConnection();
            var list = new List<SavedComputer>();

            if (!File.Exists(DbPath))
            {
                return list;
            }

            using (var con = new SqliteConnection(ConnectionString))
            {
                con.Open();
                EnsureSchema(con);

                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = SqlSelectAllComputers;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var cpuCores = reader.IsDBNull(reader.GetOrdinal("kerne"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("kerne"));

                            list.Add(new SavedComputer
                            {
                                Id = reader.GetInt64(reader.GetOrdinal("id")),
                                Name = reader["name"] as string,
                                Ort = reader["ort"] as string,
                                CpuName = reader["cpu_name"] as string,
                                CpuCores = cpuCores,
                                CpuManufacturer = reader["cpu_hersteller"] as string,
                                GpuName = reader["gpu_name"] as string,
                                GpuManufacturer = reader["gpu_hersteller"] as string,
                                MotherboardName = reader["motherboard_name"] as string,
                                MotherboardManufacturer = reader["motherboard_hersteller"] as string,
                                Ram = reader["ram"] as string,
                                RamSpeed = reader["ram_speed"] as string,
                                Memory = reader["memory"] as string
                            });
                        }
                    }
                }
            }

            return list;
        }

        private void SaveToDatabase()
        {
            EnsureDbPathAndConnection();

            using (var con = new SqliteConnection(ConnectionString))
            {
                con.Open();

                using (var tx = con.BeginTransaction())
                {
                    EnsureSchema(con, tx);

                    if (!string.IsNullOrWhiteSpace(CPUName))
                    {
                        using (var cmdCpu = con.CreateCommand())
                        {
                            cmdCpu.Transaction = tx;
                            cmdCpu.CommandText = SqlUpsertCpu;
                            cmdCpu.Parameters.AddWithValue("$name", CPUName);
                            cmdCpu.Parameters.AddWithValue("$kerne", CPUCores > 0 ? CPUCores : 1);
                            cmdCpu.Parameters.AddWithValue("$hersteller", ValueOrUnknown(CPUManufacturer));
                            cmdCpu.ExecuteNonQuery();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(GPUName))
                    {
                        using (var cmdGpu = con.CreateCommand())
                        {
                            cmdGpu.Transaction = tx;
                            cmdGpu.CommandText = SqlUpsertGpu;
                            cmdGpu.Parameters.AddWithValue("$name", GPUName);
                            cmdGpu.Parameters.AddWithValue("$hersteller", ValueOrUnknown(GPUManufacturer));
                            cmdGpu.ExecuteNonQuery();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(MotherboardModel))
                    {
                        using (var cmdMobo = con.CreateCommand())
                        {
                            cmdMobo.Transaction = tx;
                            cmdMobo.CommandText = SqlUpsertMotherboard;
                            cmdMobo.Parameters.AddWithValue("$name", MotherboardModel);
                            cmdMobo.Parameters.AddWithValue("$hersteller", ValueOrUnknown(MotherboardManufacturer));
                            cmdMobo.ExecuteNonQuery();
                        }
                    }

                    using (var cmdPc = con.CreateCommand())
                    {
                        cmdPc.Transaction = tx;
                        cmdPc.CommandText = SqlInsertComputer;

                        cmdPc.Parameters.AddWithValue("$name", ValueOrUnknown(ComputerName));
                        cmdPc.Parameters.AddWithValue("$ort", DbOrNull(Ort));

                        cmdPc.Parameters.AddWithValue("$cpu_name", DbOrNull(CPUName));
                        cmdPc.Parameters.AddWithValue("$gpu_name", DbOrNull(GPUName));
                        cmdPc.Parameters.AddWithValue("$motherboard_name", DbOrNull(MotherboardModel));

                        cmdPc.Parameters.AddWithValue("$ram", RAMSize > 0 ? (object)(RAMSize.ToString("0.##") + " GB") : DBNull.Value);
                        cmdPc.Parameters.AddWithValue("$ram_speed", DbOrNull(RAMSpeed));
                        cmdPc.Parameters.AddWithValue("$memory", DbOrNull(MemorySummary));

                        cmdPc.ExecuteScalar();
                    }

                    tx.Commit();
                }
            }
        }

        private string BuildPreview(SavedComputer computer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Name: {ValueOrUnknown(computer.Name)}");
            sb.AppendLine($"Ort: {ValueOrUnknown(computer.Ort)}");
            sb.AppendLine();
            sb.AppendLine($"CPU: {ValueOrUnknown(computer.CpuName)}");
            sb.AppendLine($"CPU Kerne: {(computer.CpuCores.HasValue ? computer.CpuCores.Value.ToString() : "Unknown")}");
            sb.AppendLine($"GPU: {ValueOrUnknown(computer.GpuName)}");
            sb.AppendLine($"RAM: {ValueOrUnknown(computer.Ram)}");
            sb.AppendLine($"RAM Speed: {ValueOrUnknown(computer.RamSpeed)}");
            sb.AppendLine($"Memory: {ValueOrUnknown(computer.Memory)}");
            sb.AppendLine($"Motherboard: {ValueOrUnknown(computer.MotherboardName)}");
            return sb.ToString();
        }

        private void OpenSavedListButton_Click(object sender, RoutedEventArgs e)
        {
            var computers = LoadAllComputers();
            SavedComputersListBox.ItemsSource = computers;

            if (computers.Count == 0)
            {
                SavedComputerPreviewText.Text = "Keine gespeicherten Rechner in der Datenbank gefunden.";
            }
            else
            {
                SavedComputersListBox.SelectedIndex = 0;
            }

            DetailViewGrid.Visibility = Visibility.Collapsed;
            ListViewGrid.Visibility = Visibility.Visible;
        }

        private void CloseSavedListButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCurrentHardwareDetails();
        }

        private void SavedComputersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = SavedComputersListBox.SelectedItem as SavedComputer;
            SavedComputerPreviewText.Text = selected == null ? "Bitte einen Rechner auswählen." : BuildPreview(selected);
        }

        private void OpenSelectedComputerButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SavedComputersListBox.SelectedItem as SavedComputer;
            if (selected == null)
            {
                MessageBox.Show("Bitte zuerst einen gespeicherten Rechner auswählen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowSavedComputerDetails(selected);
        }

        private void BackToListButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSavedListButton_Click(sender, e);
        }

        // Button Handler
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveToDatabase();

                MessageBox.Show(
                    $"Gespeichert in:\n{DbPath}",
                    "OK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Fehler beim Speichern:\n" + ex.Message,
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}
