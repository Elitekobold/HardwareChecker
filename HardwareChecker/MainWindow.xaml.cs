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
         
        // Hardware-Klassenvariablen
         
        private string CPUName;
        private int CPUCores;
        private string CPUManufacturer;     // NEU
        private string CPUVersion;

        private string GPUName;
        private string GPUManufacturer;     // NEU
        private string GPUVersion;

        private double RAMSize;
        private string RAMSpeed;            // NEU

        private string MotherboardModel;
        private string MotherboardManufacturer;

        private string DiskModel;           // wird als "Memory" zusammengefasst genutzt
        private string MemorySummary;       // NEU (statt nur Textausgabe)

         
        // Computer / DB-Klassenvariablen
         
        private string ComputerName;        // NEU
        private string Ort;                 // NEU (Standort)
        private string DbPath;              // NEU
        private string ConnectionString;    // NEU

        // USB Label (Stick)
        private const string UsbLabel = "Luesvbi";

         
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

        public MainWindow()
        {
            InitializeComponent();

            // Computer Name initial setzen
            ComputerName = Environment.MachineName;

            // Ort optional: hier leer (kannst du später aus UI befüllen)
            Ort = "";

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
                    CPUName = obj["Name"]?.ToString();
                    CPUManufacturer = obj["Manufacturer"]?.ToString(); // NEU
                    CPUCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                    CPUVersion = obj["ProcessorId"]?.ToString();       // optional (ID als "Version")

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
                    GPUManufacturer = obj["AdapterCompatibility"]?.ToString(); // NEU

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
            // Gesamt RAM
            using (var searcher = new ManagementObjectSearcher("select * from Win32_ComputerSystem"))
            {
                foreach (var obj in searcher.Get())
                {
                    double totalMemory = Convert.ToDouble(obj["TotalPhysicalMemory"] ?? 0) / (1024 * 1024 * 1024);
                    RAMSize = Math.Round(totalMemory, 2);
                    break;
                }
            }

            // RAM Speed (max. aus allen Riegeln)
            var speeds = new List<int>();
            using (var searcher = new ManagementObjectSearcher("select Speed from Win32_PhysicalMemory"))
            {
                foreach (var obj in searcher.Get())
                {
                    if (obj["Speed"] != null && int.TryParse(obj["Speed"].ToString(), out var s))
                        speeds.Add(s);
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
                        sizeGB = Math.Round(Convert.ToDouble(obj["Size"]) / (1024 * 1024 * 1024), 2);

                    var model = obj["Model"]?.ToString();
                    var iface = obj["InterfaceType"]?.ToString();

                    sb.AppendLine($"Model: {model}");
                    sb.AppendLine($"Interface: {iface}");
                    sb.AppendLine($"Size: {sizeGB} GB");
                    sb.AppendLine();

                    // Für DB als "Memory" zusammenfassen
                    if (!string.IsNullOrWhiteSpace(model))
                        memoryParts.Add($"{model} ({sizeGB} GB)");
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
            // 1) Versuche Stick zu finden (Drive Label = LMSDB)
            // 2) Fallback: lokale Datei in AppData
            var removable = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Removable)
                .FirstOrDefault(d => string.Equals(d.VolumeLabel, UsbLabel, StringComparison.OrdinalIgnoreCase));

            if (removable != null)
            {
                var root = removable.RootDirectory.FullName; // z.B. "E:\"
                DbPath = Path.Combine(root, "db", "app.db");
            }
            else
            {
                var localDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HardwareInfoApp",
                    "db"
                );
                Directory.CreateDirectory(localDir);
                DbPath = Path.Combine(localDir, "app.db");
            }

            // Ordner sicherstellen
            var dir = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
        }

        /*private void EnsureSchema(SqliteConnection con, SqliteTransaction tx)
        {
            using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = SqlSchema;
            cmd.ExecuteNonQuery();
        }*/

        private void SaveToDatabase()
        {
            EnsureDbPathAndConnection();

            using (var con = new SqliteConnection(ConnectionString))
            {
                con.Open();

                using (var tx = con.BeginTransaction())
                {
                    // FK aktivieren
                    using (var fk = con.CreateCommand())
                    {
                        fk.Transaction = tx;
                        fk.CommandText = SqlEnableFk;
                        fk.ExecuteNonQuery();
                    }

                    // Schema sicherstellen
                    using (var cmdSchema = con.CreateCommand())
                    {
                        cmdSchema.Transaction = tx;
                        cmdSchema.CommandText = SqlSchema;
                        cmdSchema.ExecuteNonQuery();
                    }

                    // CPU upsert
                    if (!string.IsNullOrWhiteSpace(CPUName))
                    {
                        using (var cmdCpu = con.CreateCommand())
                        {
                            cmdCpu.Transaction = tx;
                            cmdCpu.CommandText = SqlUpsertCpu;
                            cmdCpu.Parameters.AddWithValue("$name", CPUName);
                            cmdCpu.Parameters.AddWithValue("$kerne", CPUCores > 0 ? CPUCores : 1);
                            cmdCpu.Parameters.AddWithValue("$hersteller", string.IsNullOrWhiteSpace(CPUManufacturer) ? "Unknown" : CPUManufacturer);
                            cmdCpu.ExecuteNonQuery();
                        }
                    }

                    // GPU upsert
                    if (!string.IsNullOrWhiteSpace(GPUName))
                    {
                        using (var cmdGpu = con.CreateCommand())
                        {
                            cmdGpu.Transaction = tx;
                            cmdGpu.CommandText = SqlUpsertGpu;
                            cmdGpu.Parameters.AddWithValue("$name", GPUName);
                            cmdGpu.Parameters.AddWithValue("$hersteller", string.IsNullOrWhiteSpace(GPUManufacturer) ? "Unknown" : GPUManufacturer);
                            cmdGpu.ExecuteNonQuery();
                        }
                    }

                    // Motherboard upsert
                    if (!string.IsNullOrWhiteSpace(MotherboardModel))
                    {
                        using (var cmdMobo = con.CreateCommand())
                        {
                            cmdMobo.Transaction = tx;
                            cmdMobo.CommandText = SqlUpsertMotherboard;
                            cmdMobo.Parameters.AddWithValue("$name", MotherboardModel);
                            cmdMobo.Parameters.AddWithValue("$hersteller", string.IsNullOrWhiteSpace(MotherboardManufacturer) ? "Unknown" : MotherboardManufacturer);
                            cmdMobo.ExecuteNonQuery();
                        }
                    }

                    // Computer insert
                    using (var cmdPc = con.CreateCommand())
                    {
                        cmdPc.Transaction = tx;
                        cmdPc.CommandText = SqlInsertComputer;

                        cmdPc.Parameters.AddWithValue("$name", string.IsNullOrWhiteSpace(ComputerName) ? "Unknown" : ComputerName);
                        cmdPc.Parameters.AddWithValue("$ort", string.IsNullOrWhiteSpace(Ort) ? (object)DBNull.Value : Ort);

                        cmdPc.Parameters.AddWithValue("$cpu_name", string.IsNullOrWhiteSpace(CPUName) ? (object)DBNull.Value : CPUName);
                        cmdPc.Parameters.AddWithValue("$gpu_name", string.IsNullOrWhiteSpace(GPUName) ? (object)DBNull.Value : GPUName);
                        cmdPc.Parameters.AddWithValue("$motherboard_name", string.IsNullOrWhiteSpace(MotherboardModel) ? (object)DBNull.Value : MotherboardModel);

                        cmdPc.Parameters.AddWithValue("$ram", RAMSize > 0 ? (RAMSize.ToString("0.##") + " GB") : (object)DBNull.Value);
                        cmdPc.Parameters.AddWithValue("$ram_speed", string.IsNullOrWhiteSpace(RAMSpeed) ? (object)DBNull.Value : RAMSpeed);
                        cmdPc.Parameters.AddWithValue("$memory", string.IsNullOrWhiteSpace(MemorySummary) ? (object)DBNull.Value : MemorySummary);

                        cmdPc.ExecuteScalar(); // last_insert_rowid() ignorieren oder speichern
                    }

                    tx.Commit();
                }
            }
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
