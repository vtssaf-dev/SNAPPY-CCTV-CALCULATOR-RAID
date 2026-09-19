using SNAPPY.CCTV.DiskCalculator.Models;

namespace SNAPPY.CCTV.DiskCalculator.Services;

public static class CalculatorService
{
    private sealed record ResolutionProfile(string Key, int Width, int Height);

    private static readonly ResolutionProfile[] ResolutionProfiles =
    [
        new("1MP", 1280, 720),
        new("2MP", 1920, 1080),
        new("3MP", 2048, 1536),
        new("4MP", 2560, 1440),
        new("5MP", 2560, 1920),
        new("6MP", 3072, 2048),
        new("8MP", 3840, 2160),
        new("10MP", 3648, 2736),
        new("12MP", 4000, 3000)
    ];

    private static readonly Dictionary<string, double> EncodingFactor = new(StringComparer.OrdinalIgnoreCase)
    {
        ["H.265+"] = 1.00,
        ["H.265"] = 1.30,
        ["H.264+"] = 1.50,
        ["H.264"] = 1.85
    };

    private static readonly double[] VbrFactor = [0.50, 0.62, 0.74, 0.84, 0.92, 0.97];

    private static readonly Dictionary<string, double> SceneFactor = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Low"] = 0.80,
        ["Normal"] = 1.00,
        ["High"] = 1.10,
        ["Very High"] = 1.20
    };

    public static CalculatorResult Calculate(
        int cameraCount,
        string resolution,
        int frameRate,
        string encoding,
        string bitrateType,
        int vbrLevel,
        string sceneComplexity,
        double manualBitrateKbps,
        double recordingHours,
        double retentionValue,
        string retentionUnit,
        double diskSizeTb,
        double overheadPercent,
        string calculationMode)
    {
        cameraCount = Math.Clamp(cameraCount, 1, 10000);
        frameRate = Math.Clamp(frameRate, 1, 30);
        vbrLevel = Math.Clamp(vbrLevel, 1, 6);
        recordingHours = Math.Clamp(recordingHours, 1, 24);
        retentionValue = Math.Max(0.1, retentionValue);
        diskSizeTb = Math.Max(0.1, diskSizeTb);
        overheadPercent = Math.Max(0, overheadPercent);

        SmartBitrate smart = GetSmartBitrate(resolution, frameRate, encoding, bitrateType, vbrLevel, sceneComplexity);
        double average = calculationMode.Equals("Manual Bitrate", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(64, manualBitrateKbps)
            : smart.EstimatedAverageKbps;

        double retentionDays = retentionUnit.Equals("Months", StringComparison.OrdinalIgnoreCase)
            ? retentionValue * 30
            : retentionValue;

        double perCameraDayGb = average * 1000 * 86400 / 8 / 1_000_000_000 * (recordingHours / 24);
        double allCamerasDayGb = perCameraDayGb * cameraCount;
        double raw = allCamerasDayGb * retentionDays;
        double required = raw * (1 + overheadPercent / 100);
        double tb = required / 1000;
        int disks = Math.Max(1, (int)Math.Ceiling(required / (diskSizeTb * 1000)));

        string note = calculationMode.Equals("Manual Bitrate", StringComparison.OrdinalIgnoreCase)
            ? "Manual mode: the entered bitrate is used directly for storage sizing."
            : "Smart AI engine: resolution pixels, FPS, encoding/compression, CBR/VBR, VBR level and scene complexity are evaluated together. Actual camera bitrate remains authoritative.";

        return new(
            smart.RecommendedKbps,
            average,
            average,
            perCameraDayGb,
            allCamerasDayGb,
            raw,
            required,
            tb,
            disks,
            retentionDays,
            calculationMode,
            encoding,
            resolution,
            frameRate,
            bitrateType,
            vbrLevel,
            sceneComplexity,
            note);
    }

    public static VmsStorageResult CalculateVmsStorage(
        double requiredUsableTb,
        double diskSizeTb,
        string bayType,
        string raidType,
        int raidGroupCount = 1,
        int hotSpareCount = 0)
    {
        requiredUsableTb = Math.Max(0.01, requiredUsableTb);
        diskSizeTb = Math.Max(0.1, diskSizeTb);
        int bayCount = ParseBayCount(bayType);
        raidType = NormalizeRaidType(raidType);
        raidGroupCount = Math.Clamp(raidGroupCount, 1, Math.Max(1, bayCount));
        hotSpareCount = Math.Clamp(hotSpareCount, 0, Math.Max(0, bayCount - 1));

        int minimumDisksPerGroup = raidType switch
        {
            "RAID 1" => 2,
            "RAID 5" => 3,
            "RAID 6" => 4,
            "RAID 10" => 4,
            _ => 4
        };

        // The required usable capacity is distributed across the manually selected RAID groups.
        // Hot-spare disks are additional physical disks and do not contribute usable capacity.
        double requiredPerGroup = requiredUsableTb / raidGroupCount;
        int disksPerGroup = minimumDisksPerGroup;
        while (disksPerGroup <= bayCount && GetUsableCapacityTb(disksPerGroup, diskSizeTb, raidType) + 1e-9 < requiredPerGroup)
        {
            disksPerGroup++;
            if (raidType == "RAID 10" && disksPerGroup % 2 != 0)
                disksPerGroup++;
        }

        int raidDataDisks = disksPerGroup * raidGroupCount;
        int totalInstalledDisks = raidDataDisks + hotSpareCount;
        double rawCapacity = raidDataDisks * diskSizeTb;
        double usablePerGroup = GetUsableCapacityTb(disksPerGroup, diskSizeTb, raidType);
        double usableCapacity = usablePerGroup * raidGroupCount;
        bool fits = disksPerGroup <= bayCount && totalInstalledDisks <= bayCount;
        int freeBays = Math.Max(0, bayCount - totalInstalledDisks);
        double unused = Math.Max(0, usableCapacity - requiredUsableTb);

        string faultTolerance = raidType switch
        {
            "RAID 1" => "Each RAID group can tolerate 1 failed disk when the group has 2 mirrored disks.",
            "RAID 5" => "Each RAID group can tolerate 1 failed disk without losing the RAID volume.",
            "RAID 6" => "Each RAID group can tolerate 2 failed disks without losing the RAID volume.",
            "RAID 10" => "Each RAID 10 group can tolerate failures provided at least one disk in every mirror pair remains healthy.",
            _ => "—"
        };

        string formula = raidType switch
        {
            "RAID 1" => "Per group usable = 1 × disk size",
            "RAID 5" => "Per group usable = (disks per group − 1) × disk size",
            "RAID 6" => "Per group usable = (disks per group − 2) × disk size",
            "RAID 10" => "Per group usable = (disks per group ÷ 2) × disk size",
            _ => "—"
        };

        string status;
        if (disksPerGroup > bayCount)
        {
            status = $"One RAID group needs {disksPerGroup} disks, which exceeds the {bayCount}-bay enclosure.";
        }
        else if (totalInstalledDisks > bayCount)
        {
            status = $"RAID groups need {raidDataDisks} disks + {hotSpareCount} hot spare(s) = {totalInstalledDisks} installed disks, exceeding the {bayCount}-bay enclosure.";
        }
        else
        {
            status = $"Fits in {bayCount}-bay storage chassis • {raidGroupCount} RAID group(s) • {disksPerGroup} disk(s)/group • {hotSpareCount} hot spare(s) • {freeBays} bay(s) remaining.";
        }

        return new(
            $"{bayCount}-Bay",
            bayCount,
            raidType,
            requiredUsableTb,
            diskSizeTb,
            raidGroupCount,
            disksPerGroup,
            hotSpareCount,
            raidDataDisks,
            totalInstalledDisks,
            rawCapacity,
            usableCapacity,
            unused,
            fits,
            freeBays,
            faultTolerance,
            formula,
            status);
    }

    public static double GetRecommendedBitrate(string resolution, int frameRate, string encoding)
        => GetSmartBitrate(resolution, frameRate, encoding, "CBR", 6, "Normal").RecommendedKbps;

    public static SmartBitrate GetSmartBitrate(
        string resolution,
        int frameRate,
        string encoding,
        string bitrateType,
        int vbrLevel,
        string sceneComplexity)
    {
        frameRate = Math.Clamp(frameRate, 1, 30);
        vbrLevel = Math.Clamp(vbrLevel, 1, 6);

        string key = ExtractResolutionKey(resolution);
        ResolutionProfile profile = ResolutionProfiles.FirstOrDefault(x =>
            x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? ResolutionProfiles[1];

        double pixels = profile.Width * (double)profile.Height;
        double referencePixels = 1920d * 1080d;
        double resolutionFactor = Math.Sqrt(pixels / referencePixels);
        double baseKbps = 2048d * resolutionFactor;
        double fpsFactor = frameRate / 25d;
        double compressionFactor = EncodingFactor.GetValueOrDefault(encoding, 1d);
        double sceneFactor = SceneFactor.GetValueOrDefault(sceneComplexity, 1d);

        double recommended = baseKbps * fpsFactor * compressionFactor * sceneFactor;
        recommended = Math.Clamp(recommended, 128, 25_000);

        double average = recommended;
        if (bitrateType.Equals("VBR", StringComparison.OrdinalIgnoreCase))
            average *= VbrFactor[vbrLevel - 1];

        average = Math.Clamp(average, 64, recommended * 1.30);

        return new(recommended, average, baseKbps, resolutionFactor, fpsFactor, compressionFactor, sceneFactor);
    }

    private static double GetUsableCapacityTb(int diskCount, double diskSizeTb, string raidType)
    {
        return raidType switch
        {
            "RAID 1" when diskCount >= 2 => diskSizeTb,
            "RAID 5" when diskCount >= 3 => (diskCount - 1) * diskSizeTb,
            "RAID 6" when diskCount >= 4 => (diskCount - 2) * diskSizeTb,
            "RAID 10" when diskCount >= 4 && diskCount % 2 == 0 => (diskCount / 2d) * diskSizeTb,
            _ => 0
        };
    }

    private static int ParseBayCount(string bayType)
    {
        string digits = new(bayType.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int bays) && bays is 4 or 8 or 16 or 24 ? bays : 8;
    }

    private static string NormalizeRaidType(string value)
    {
        if (value.Contains("1", StringComparison.OrdinalIgnoreCase) && !value.Contains("10", StringComparison.OrdinalIgnoreCase)) return "RAID 1";
        if (value.Contains("5", StringComparison.OrdinalIgnoreCase)) return "RAID 5";
        if (value.Contains("6", StringComparison.OrdinalIgnoreCase)) return "RAID 6";
        if (value.Contains("10", StringComparison.OrdinalIgnoreCase)) return "RAID 10";
        return "RAID 6";
    }

    private static string ExtractResolutionKey(string value)
    {
        string trimmed = value.Trim();
        int index = trimmed.IndexOf("MP", StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? trimmed[..(index + 2)] : "2MP";
    }

    public sealed record SmartBitrate(
        double RecommendedKbps,
        double EstimatedAverageKbps,
        double BaseKbps,
        double ResolutionFactor,
        double FpsFactor,
        double CompressionFactor,
        double SceneFactor);
}
