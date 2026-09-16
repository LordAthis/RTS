// Verzio: v2.0.0 - 2026-09-16
// ROUND17 ATALAKITAS: uj sema (schema_version = 2), amit a
// Scripts\Hw\*.ps1 resz-scriptek allitanak elo, es a
// Get-HardwareReport.ps1 orchestrator fuz ossze EGY kozos JSON-ba
// (<InstallRoot>\data\hardware-info.json).
//
// MI VALTOZOTT a v0.6.0-hoz kepest es MIERT:
//  - A korabbi modell EGY-EGY kulso eszkoz (CPU-Z/GPU-Z/HDS/DXDIAG) NYERS
//    SZOVEGET tarolta, mert a lekerdezes is azokra epult. A round17-ben a
//    lekerdezes alapja a Windows sajat WMI/CIM adatbazisa lett (csendes,
//    azonnali, letoltes nelkuli), ezert mostantol STRUKTURALT mezoket is
//    tarolunk (magok szama, VRAM, SMART-ertekek, kotetek), nem csak
//    szoveget. A nyers szoveg (raw_text) megmarad a reszletes nezethez.
//  - A "machine" szekcio neve "system" lett, es sokkal tobbet tud
//    (alaplap, BIOS, memoria-modulok, uzemido).
//  - Minden szekcio sajat "errors" tombot kap - egy hibas ag SOSEM
//    akasztja meg a tobbit, a hiba pedig lathato marad.
//
// VISSZAFELE KOMPATIBILITAS: a regi (v1) hardware-info.json beolvasasakor
// az uj mezok egyszeruen uresen maradnak, es a kovetkezo lekerdezes
// felulirja oket - NINCS szukseg migraciora, es nem all le semmi.
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RTS.Models
{
    // Minden szekcio kozos alapja.
    public class HwSectionBase
    {
        [JsonPropertyName("section")]
        public string Section { get; set; } = "";

        [JsonPropertyName("queried_at_utc")]
        public DateTime? QueriedAtUtc { get; set; }

        [JsonPropertyName("available")]
        public bool Available { get; set; }

        // Rovid, egysoros osszefoglalo (B2 doboz, A2 fejlec).
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        // A reszletes nezet nyers szovege.
        [JsonPropertyName("raw_text")]
        public string RawText { get; set; } = "";

        // Honnan szarmazik a nyers szoveg: "WMI" vagy pl. "GPU-Z (C:\...)".
        // Ezt kiirjuk a feluleten is, hogy egyertelmu legyen, kell-e egyaltalan
        // a kulso eszkoz a pontosabb adathoz.
        [JsonPropertyName("raw_source")]
        public string RawSource { get; set; } = "";

        [JsonPropertyName("errors")]
        public List<string> Errors { get; set; } = new();
    }

    public class MemoryModuleInfo
    {
        [JsonPropertyName("slot")]
        public string Slot { get; set; } = "";

        [JsonPropertyName("size_gb")]
        public double SizeGb { get; set; }

        [JsonPropertyName("speed")]
        public string Speed { get; set; } = "";

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = "";

        [JsonPropertyName("part_number")]
        public string PartNumber { get; set; } = "";
    }

    public class SystemSection : HwSectionBase
    {
        [JsonPropertyName("machine_name")]
        public string MachineName { get; set; } = "";

        [JsonPropertyName("os_caption")]
        public string OsCaption { get; set; } = "";

        [JsonPropertyName("os_version")]
        public string OsVersion { get; set; } = "";

        [JsonPropertyName("os_build")]
        public string OsBuild { get; set; } = "";

        [JsonPropertyName("os_arch")]
        public string OsArch { get; set; } = "";

        [JsonPropertyName("os_install_date")]
        public string OsInstallDate { get; set; } = "";

        [JsonPropertyName("last_boot")]
        public string LastBoot { get; set; } = "";

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = "";

        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("baseboard")]
        public string Baseboard { get; set; } = "";

        [JsonPropertyName("bios")]
        public string Bios { get; set; } = "";

        [JsonPropertyName("bios_date")]
        public string BiosDate { get; set; } = "";

        [JsonPropertyName("ram_total_gb")]
        public double RamTotalGb { get; set; }

        [JsonPropertyName("ram_modules")]
        public List<MemoryModuleInfo> RamModules { get; set; } = new();

        [JsonPropertyName("local_user_accounts")]
        public List<string> LocalUserAccounts { get; set; } = new();
    }

    public class CpuSection : HwSectionBase
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = "";

        [JsonPropertyName("cores")]
        public int Cores { get; set; }

        [JsonPropertyName("threads")]
        public int Threads { get; set; }

        [JsonPropertyName("max_clock_mhz")]
        public int MaxClockMhz { get; set; }

        [JsonPropertyName("socket")]
        public string Socket { get; set; } = "";

        [JsonPropertyName("l2_cache_kb")]
        public int L2CacheKb { get; set; }

        [JsonPropertyName("l3_cache_kb")]
        public int L3CacheKb { get; set; }
    }

    public class GpuAdapterInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("vram_gb")]
        public double VramGb { get; set; }

        [JsonPropertyName("driver_version")]
        public string DriverVersion { get; set; } = "";

        [JsonPropertyName("driver_date")]
        public string DriverDate { get; set; } = "";

        [JsonPropertyName("video_processor")]
        public string VideoProcessor { get; set; } = "";

        [JsonPropertyName("resolution")]
        public string Resolution { get; set; } = "";

        [JsonPropertyName("refresh_hz")]
        public string RefreshHz { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
    }

    public class GpuSection : HwSectionBase
    {
        [JsonPropertyName("adapters")]
        public List<GpuAdapterInfo> Adapters { get; set; } = new();
    }

    public class DriveInfo
    {
        [JsonPropertyName("index")]
        public string Index { get; set; } = "";

        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("serial")]
        public string Serial { get; set; } = "";

        [JsonPropertyName("interface")]
        public string Interface { get; set; } = "";

        [JsonPropertyName("size_gb")]
        public double SizeGb { get; set; }

        [JsonPropertyName("media_type")]
        public string MediaType { get; set; } = "";

        [JsonPropertyName("health_status")]
        public string HealthStatus { get; set; } = "";

        [JsonPropertyName("smart_predict_failure")]
        public bool? SmartPredictFailure { get; set; }

        [JsonPropertyName("temperature_c")]
        public int? TemperatureC { get; set; }

        [JsonPropertyName("power_on_hours")]
        public int? PowerOnHours { get; set; }

        [JsonPropertyName("wear_percent")]
        public int? WearPercent { get; set; }

        [JsonPropertyName("read_errors")]
        public int? ReadErrors { get; set; }
    }

    public class VolumeInfo
    {
        [JsonPropertyName("drive")]
        public string Drive { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("filesystem")]
        public string Filesystem { get; set; } = "";

        [JsonPropertyName("total_gb")]
        public double TotalGb { get; set; }

        [JsonPropertyName("free_gb")]
        public double FreeGb { get; set; }

        [JsonPropertyName("used_percent")]
        public int UsedPercent { get; set; }
    }

    public class DiskSection : HwSectionBase
    {
        // A H.D. Sentinel sajat ertekelese, ha elerheto - kulonben egy
        // OVATOS becsles a SMART-allapotbol (ezt a raw_text egyertelmuen
        // jelzi is a felhasznalonak).
        [JsonPropertyName("health_percent")]
        public int? HealthPercent { get; set; }

        [JsonPropertyName("drives")]
        public List<DriveInfo> Drives { get; set; } = new();

        [JsonPropertyName("volumes")]
        public List<VolumeInfo> Volumes { get; set; } = new();
    }

    public class SensorReading
    {
        // "Temperature" | "Load" | "Fan" | "Clock" | "Power"
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = "";

        [JsonPropertyName("parent")]
        public string Parent { get; set; } = "";

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("min")]
        public double? Min { get; set; }

        [JsonPropertyName("max")]
        public double? Max { get; set; }
    }

    // UJ, round17: homerseklet / ventilator / terheles adatok. Fo forras a
    // LibreHardwareMonitor sajat WMI-nevtere; tartalek a Windows ACPI
    // termikus zonaja (lasd Scripts\Hw\Get-SensorInfo.ps1).
    public class SensorSection : HwSectionBase
    {
        [JsonPropertyName("sensors")]
        public List<SensorReading> Sensors { get; set; } = new();
    }

    public class DxDiagSection : HwSectionBase
    {
    }

    // A teljes <InstallRoot>\data\hardware-info.json gyoker-objektuma.
    public class HardwareInfo
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = 2;

        // A legutobbi lekerdezes ideje.
        [JsonPropertyName("queried_at_utc")]
        public DateTime? QueriedAtUtc { get; set; }

        // Mit frissitett a legutobbi futas: "all" vagy egy szekcio neve
        // (a "Uj gyorsjelentes" gombokhoz).
        [JsonPropertyName("last_refresh_scope")]
        public string LastRefreshScope { get; set; } = "";

        // Egysoros, osszefuzott osszefoglalo (B2 doboz).
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("system")]
        public SystemSection? System { get; set; }

        [JsonPropertyName("cpu")]
        public CpuSection? Cpu { get; set; }

        [JsonPropertyName("gpu")]
        public GpuSection? Gpu { get; set; }

        [JsonPropertyName("disk")]
        public DiskSection? Disk { get; set; }

        [JsonPropertyName("sensors")]
        public SensorSection? Sensors { get; set; }

        [JsonPropertyName("dxdiag")]
        public DxDiagSection? DxDiag { get; set; }

        [JsonPropertyName("errors")]
        public List<string> Errors { get; set; } = new();
    }
}
