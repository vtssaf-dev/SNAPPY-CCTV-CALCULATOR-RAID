namespace SNAPPY.CCTV.DiskCalculator.Models;

public sealed record CalculatorResult(
    double RecommendedBitrateKbps,
    double EstimatedAverageBitrateKbps,
    double StorageBitrateKbps,
    double PerCameraDayGb,
    double AllCamerasDayGb,
    double RawStorageGb,
    double RequiredStorageGb,
    double RequiredStorageTb,
    int DiskCount,
    double RetentionDays,
    string CalculationMode,
    string Encoding,
    string Resolution,
    int FrameRate,
    string BitrateType,
    int VbrLevel,
    string SceneComplexity,
    string CalculationNote);

public sealed record VmsStorageResult(
    string BayType,
    int BayCount,
    string RaidType,
    double RequiredUsableTb,
    double DiskSizeTb,
    int RequiredDiskCount,
    double RawCapacityTb,
    double UsableCapacityTb,
    double UnusedUsableTb,
    bool FitsInBays,
    int FreeBays,
    string FaultTolerance,
    string CapacityFormula,
    string StatusText);
