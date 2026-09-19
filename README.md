# SNAPPY CCTV Disk Calculator

A native WPF .NET 10 Windows application for CCTV recording-storage planning.

## Calculation areas

### NVR mode
Calculates the basic recording storage requirement from camera count, bitrate, recording hours, retention, disk size and overhead.

### VMS mode — Storage only
The VMS section is intentionally focused on **storage**, not server CPU/RAM/GPU specifications.

After the main storage calculation, VMS calculates:

- Required usable storage in TB.
- Storage Bay Type: **4-Bay / 8-Bay / 16-Bay / 24-Bay**.
- Disk size per drive: 2–24 TB.
- RAID System: **RAID 6 / RAID 5 / RAID 10 / RAID 1**.
- Required physical disk count for the selected RAID level.
- Raw installed capacity.
- RAID usable capacity.
- Unused usable capacity after meeting the calculated storage requirement.
- Free bays.
- Whether the selected bay chassis can contain the required number of drives.
- RAID capacity formula and basic fault-tolerance information.

The RAID calculation is performed **from the calculated storage result**; it is not a separate fixed storage table.

## RAID capacity rules

- RAID 1: usable capacity = 1 × disk size (50% of raw for a 2-disk mirror).
- RAID 5: usable capacity = (disk count − 1) × disk size.
- RAID 6: usable capacity = (disk count − 2) × disk size.
- RAID 10: usable capacity = (disk count ÷ 2) × disk size; disk count is kept even.

The calculator increases the physical disk count until the RAID usable capacity meets or exceeds the calculated usable storage requirement, then checks that count against the selected bay count.

## Important

The smart bitrate model is a planning estimate. Actual camera bitrate, VMS behavior, RAID implementation, filesystem overhead and vendor-specific storage requirements should be validated against the actual deployment.

## Build

The project targets .NET 10 WPF, Windows x64, self-contained single-file publishing, with Windows 10 1809 as the minimum platform version.
