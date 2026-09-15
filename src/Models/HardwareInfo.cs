// Verzio: v0.6.0 - 2026-09-14
// UJ MODELL a "2.1 Eszkozok (hardver-lekerdezes)" kor elokeszitesehez
// (lasd feladatok.md 2.1 pontja - mar korabban egyeztetett reszletek
// alapjan). Ez a modell irja le a <InstallRoot>\data\hardware-info.json
// tartalmat - EGY kozos fajl, ahogy a specifikacio kerte, nem kulon
// fajl eszkozonkent.
//
// FONTOS: ez a fajl DRAFT/ELOKESZULET - meg nincs push-olva, a
// felhasznaloi review varhato ra (lasd a kisero jegyzet-dokumentumot).
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RTS.Models
{
    // Egy-egy lekerdezett eszkoz (CPU-Z / GPU-Z / H.D. Sentinel / DXDIAG)
    // eredmenye - nyers szoveg + rovid, UI-baratabb osszefoglalo + sajat
    // idobelyeg (a "globalis ES eszkozonkenti idobelyeg is kell" elvaras
    // szerint).
    public class HardwareToolResult
    {
        [JsonPropertyName("available")]
        public bool Available { get; set; }

        // Rovid, egy-ket soros osszefoglalo a gyors nezethez (B2 doboz,
        // A2 fejlec) - pl. "Intel Core i5-8250U (4 mag/8 szal)".
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        // A teljes, nyers szoveges riport (CPU-Z -txt kimenete, GPU-Z log
        // utolso sora, HDS /REPORT szovege, DXDIAG dump) - a reszletes
        // nezetben (A2, kulon gombbal) jelenik meg.
        [JsonPropertyName("raw_text")]
        public string RawText { get; set; } = "";

        [JsonPropertyName("queried_at_utc")]
        public DateTime? QueriedAtUtc { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    // A lemez-egeszseg (H.D. Sentinel) kulon szazalekos ertekkel is bir a
    // gyors osszefoglalohoz - ezert nem a sima HardwareToolResult-ot
    // hasznalja, hanem ezt a kiegeszitett valtozatot.
    public class DiskHealthResult : HardwareToolResult
    {
        // Null, ha nem sikerult kiolvasni a HDS riportbol.
        [JsonPropertyName("health_percent")]
        public int? HealthPercent { get; set; }
    }

    public class LocalMachineInfo
    {
        [JsonPropertyName("machine_name")]
        public string MachineName { get; set; } = "";

        [JsonPropertyName("os_caption")]
        public string OsCaption { get; set; } = "";

        [JsonPropertyName("os_version")]
        public string OsVersion { get; set; } = "";

        // Csak a felhasznalonevek listaja (nem jelszo/SID reszletek) -
        // WMI/CIM-bol (Win32_UserAccount, LocalAccount=True), natcv
        // eszkoz nelkul.
        [JsonPropertyName("local_user_accounts")]
        public List<string> LocalUserAccounts { get; set; } = new();
    }

    // A teljes <InstallRoot>\data\hardware-info.json gyoker-objektuma.
    public class HardwareInfo
    {
        // Globalis idobelyeg - a legutobbi TELJES lekerdezes ideje
        // ("Informaciok frissitese" gombra kattintaskor frissul).
        [JsonPropertyName("queried_at_utc")]
        public DateTime? QueriedAtUtc { get; set; }

        [JsonPropertyName("machine")]
        public LocalMachineInfo Machine { get; set; } = new();

        [JsonPropertyName("cpu")]
        public HardwareToolResult Cpu { get; set; } = new();

        [JsonPropertyName("gpu")]
        public HardwareToolResult Gpu { get; set; } = new();

        [JsonPropertyName("disk")]
        public DiskHealthResult Disk { get; set; } = new();

        [JsonPropertyName("dxdiag")]
        public HardwareToolResult DxDiag { get; set; } = new();
    }
}
