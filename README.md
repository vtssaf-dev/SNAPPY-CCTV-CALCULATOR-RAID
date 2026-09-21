# SNAPPY CCTV Disk Calculator - Android

Android version of the SNAPPY CCTV Disk Calculator.

## Included
- Camera count, resolution, FPS, encoding and bitrate calculation
- Smart/manual bitrate mode
- Retention days/months
- Recording hours and overhead
- VMS storage bay selection: 4/8/16/24 bay
- RAID 1/5/6/10
- Manual RAID groups
- Manual hot-spare disks
- Automatic data disks/group
- Automatic total installed disks
- Automatic number of storage devices required
- Automatic free-bay calculation
- Dark/light theme
- Total Result PDF export using the native Android Share Sheet
- PDF is fully closed before sharing so Save/Share apps can access it correctly
- Proper Android launcher icon, splash screen and startup entry

## PDF Export behavior
Tap **EXPORT TOTAL RESULT AS PDF** after calculating storage.
The app creates the PDF and opens the Android system Share Sheet. From there the user can save the PDF or share it to any installed compatible application such as Files, Google Drive, Gmail, WhatsApp, Quick Share, etc.

## Build in GitHub Actions
The included `.github/workflows/build-android.yml` installs the .NET MAUI Android workload and produces an APK artifact.

## Local build
```bash
dotnet workload install maui-android
dotnet restore SNAPPY.CCTV.DiskCalculator.Android/SNAPPY.CCTV.DiskCalculator.Android.csproj
dotnet build SNAPPY.CCTV.DiskCalculator.Android/SNAPPY.CCTV.DiskCalculator.Android.csproj -c Release -f net10.0-android
```
