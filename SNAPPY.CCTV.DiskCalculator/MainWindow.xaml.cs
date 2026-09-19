using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SNAPPY.CCTV.DiskCalculator.Models;
using SNAPPY.CCTV.DiskCalculator.Services;

namespace SNAPPY.CCTV.DiskCalculator;

public partial class MainWindow : Window
{
    private bool _isLoaded;
    private bool _isUpdating;
    private bool _isVmsMode;
    private bool _isDark = true;
    private CalculatorResult? _lastResult;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        PopulateDynamicLists();
        ApplyDefaults();
        ApplyTheme(true);
        UpdateInputStates();
        UpdateSmartRecommendation();
        UpdateVmsPanel();
        ClearStorageResult();
    }

    private void PopulateDynamicLists()
    {
        _isUpdating = true;
        try
        {
            MpBox.Items.Clear();
            MpBox.Items.Add("1MP — 1280 × 720 (720P)");
            MpBox.Items.Add("2MP — 1920 × 1080 (1080P)");
            MpBox.Items.Add("3MP — 2048 × 1536");
            MpBox.Items.Add("4MP — 2560 × 1440");
            MpBox.Items.Add("5MP — 2560 × 1920");
            MpBox.Items.Add("6MP — 3072 × 2048");
            MpBox.Items.Add("8MP — 3840 × 2160 (4K)");
            MpBox.Items.Add("10MP — 3648 × 2736");
            MpBox.Items.Add("12MP — 4000 × 3000");

            FpsBox.Items.Clear();
            for (int i = 1; i <= 30; i++) FpsBox.Items.Add(i.ToString(CultureInfo.InvariantCulture));

            VbrBox.Items.Clear();
            for (int i = 1; i <= 6; i++) VbrBox.Items.Add(i.ToString(CultureInfo.InvariantCulture));

            SceneComplexityBox.Items.Clear();
            SceneComplexityBox.Items.Add("Low");
            SceneComplexityBox.Items.Add("Normal");
            SceneComplexityBox.Items.Add("High");
            SceneComplexityBox.Items.Add("Very High");

            DiskBox.Items.Clear();
            for (int i = 2; i <= 24; i++) DiskBox.Items.Add(i.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void ApplyDefaults()
    {
        _isUpdating = true;
        try
        {
            CameraCountBox.Text = "1";
            EncodeBox.SelectedIndex = 0;
            MpBox.SelectedIndex = 1;
            FpsBox.SelectedIndex = 24;
            RateBox.SelectedIndex = 1;
            VbrBox.SelectedIndex = 5;
            SceneComplexityBox.SelectedIndex = 1;
            BitrateBox.Text = "2048";
            HoursBox.Text = "24";
            RetentionBox.Text = "30";
            RetentionUnitBox.SelectedIndex = 0;
            DiskBox.SelectedIndex = 4;
            OverheadBox.Text = "10";
            CalculationModeBox.SelectedIndex = 0;
            BayTypeBox.SelectedIndex = 1; // 8-Bay
            RaidTypeBox.SelectedIndex = 0; // RAID 6
            RaidGroupBox.Text = "1";
            HotSpareBox.Text = "0";
            _isVmsMode = false;
            UpdateArchitectureModeText();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void SmartSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _isUpdating) return;
        UpdateInputStates();
        UpdateSmartRecommendation();
        ClearCalculatedVmsDetailsOnly();
    }

    private void CalculationModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _isUpdating) return;
        UpdateInputStates();
        UpdateSmartRecommendation();
        ClearStorageResult();
    }

    private void DiskBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _isUpdating) return;
        if (_isVmsMode && _lastResult is not null)
            RecalculateVmsFromLastStorageResult();
    }

    private void VmsStorageSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _isUpdating) return;
        RecalculateVmsFromLastStorageResult();
    }

    private void VmsManualSetting_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_isLoaded || _isUpdating) return;
        RecalculateVmsFromLastStorageResult();
    }

    private void UpdateInputStates()
    {
        bool manual = GetCalculationMode() == "Manual Bitrate";
        BitrateBox.IsEnabled = manual;
        VbrBox.IsEnabled = !manual && GetSelected(RateBox) == "VBR";
    }

    private void UpdateSmartRecommendation()
    {
        if (!_isLoaded) return;

        try
        {
            string resolution = GetMegapixelKey(GetSelected(MpBox));
            int fps = GetInt(GetSelected(FpsBox), 25);
            string encoding = GetSelected(EncodeBox);
            string rateType = GetSelected(RateBox);
            int vbr = GetInt(GetSelected(VbrBox), 6);
            string scene = GetSelected(SceneComplexityBox);
            string mode = GetCalculationMode();

            var smart = CalculatorService.GetSmartBitrate(resolution, fps, encoding, rateType, vbr, scene);
            double recommended = smart.RecommendedKbps;
            double average;

            if (mode == "Manual Bitrate")
            {
                average = Math.Max(64, GetDouble(BitrateBox.Text, 2048));
                SmartStatusText.Text = "Manual mode: entered bitrate controls storage. Smart AI recommendation remains available for reference.";
            }
            else
            {
                average = smart.EstimatedAverageKbps;
                BitrateBox.Text = Math.Round(recommended).ToString(CultureInfo.InvariantCulture);
                SmartStatusText.Text = $"Smart AI: {resolution}, {fps} FPS, {encoding}, {rateType}, VBR {vbr}, {scene} scene → recalculated now.";
            }

            RecommendedBitrateText.Text = FormatMbps(recommended);
            AverageBitrateText.Text = FormatMbps(average);
            StorageBitrateText.Text = FormatMbps(average);
        }
        catch
        {
            RecommendedBitrateText.Text = "—";
            AverageBitrateText.Text = "—";
            StorageBitrateText.Text = "—";
        }
    }

    private void CalculateButton_Click(object sender, RoutedEventArgs e) => CalculateStorage();

    private void CalculateStorage()
    {
        try
        {
            int cameras = Math.Clamp(GetInt(CameraCountBox.Text, 1), 1, 10000);
            string resolution = GetMegapixelKey(GetSelected(MpBox));
            int fps = Math.Clamp(GetInt(GetSelected(FpsBox), 25), 1, 30);
            string encoding = GetSelected(EncodeBox);
            string bitrateType = GetSelected(RateBox);
            int vbr = Math.Clamp(GetInt(GetSelected(VbrBox), 6), 1, 6);
            string scene = GetSelected(SceneComplexityBox);
            double manualBitrate = Math.Max(64, GetDouble(BitrateBox.Text, 2048));
            double hours = Math.Clamp(GetDouble(HoursBox.Text, 24), 1, 24);
            double retention = Math.Max(0.1, GetDouble(RetentionBox.Text, 30));
            string retentionUnit = GetSelected(RetentionUnitBox);
            double diskTb = Math.Max(0.1, GetDouble(GetSelected(DiskBox), 6));
            double overhead = Math.Max(0, GetDouble(OverheadBox.Text, 10));
            string mode = GetCalculationMode();

            _lastResult = CalculatorService.Calculate(
                cameras, resolution, fps, encoding, bitrateType, vbr, scene,
                manualBitrate, hours, retention, retentionUnit, diskTb, overhead, mode);

            DisplayResult(_lastResult, cameras, diskTb, overhead);

            if (_isVmsMode)
            {
                CalculateAndDisplayVmsStorage(_lastResult.RequiredStorageTb, diskTb);
                StatusText.Text = $"VMS storage + RAID calculated successfully • {DateTime.Now:HH:mm:ss}";
            }
            else
            {
                StatusText.Text = $"Storage calculated successfully • {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Please check the entered values.\n\n{ex.Message}", "SNAPPY - Calculation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DisplayResult(CalculatorResult result, int cameras, double diskTb, double overhead)
    {
        RequiredTbText.Text = $"{result.RequiredStorageTb:N2} TB";
        RequiredGbText.Text = $"{result.RequiredStorageGb:N0} GB";
        PerCameraText.Text = $"{result.PerCameraDayGb:N2} GB";
        AllCamerasText.Text = $"{result.AllCamerasDayGb:N2} GB";
        DiskCountText.Text = result.DiskCount.ToString(CultureInfo.InvariantCulture);
        RecommendedBitrateText.Text = FormatMbps(result.RecommendedBitrateKbps);
        AverageBitrateText.Text = FormatMbps(result.EstimatedAverageBitrateKbps);
        StorageBitrateText.Text = FormatMbps(result.StorageBitrateKbps);

        UpdateArchitectureModeText(cameras, result.DiskCount, diskTb);
        SummaryText.Text = $"{cameras:N0} camera(s) × {FormatMbps(result.StorageBitrateKbps)} × {result.RetentionDays:N0} days = {result.RequiredStorageTb:N2} TB required including {overhead:N1}% overhead.";
        CalculationNoteText.Text = result.CalculationNote;
    }

    private void RecalculateVmsFromLastStorageResult()
    {
        if (!_isVmsMode || _lastResult is null)
        {
            ClearCalculatedVmsDetailsOnly();
            return;
        }

        double diskTb = Math.Max(0.1, GetDouble(GetSelected(DiskBox), 6));
        CalculateAndDisplayVmsStorage(_lastResult.RequiredStorageTb, diskTb);
    }

    private void CalculateAndDisplayVmsStorage(double requiredTb, double diskTb)
    {
        string bayType = GetSelected(BayTypeBox);
        string raidType = GetSelected(RaidTypeBox);
        int raidGroups = Math.Max(1, GetInt(RaidGroupBox.Text, 1));
        int hotSpares = Math.Max(0, GetInt(HotSpareBox.Text, 0));
        VmsStorageResult vms = CalculatorService.CalculateVmsStorage(requiredTb, diskTb, bayType, raidType, raidGroups, hotSpares);
        DisplayVmsStorage(vms);
    }

    private void DisplayVmsStorage(VmsStorageResult vms)
    {
        VmsRequiredUsableText.Text = $"{vms.RequiredUsableTb:N2} TB";
        VmsBayCapacityText.Text = $"{vms.BayCount}-Bay • {vms.DiskSizeTb:N0} TB/disk";
        VmsDiskCountText.Text = vms.RequiredDiskCount.ToString(CultureInfo.InvariantCulture);
        VmsDataDisksPerGroupText.Text = vms.DisksPerRaidGroup.ToString(CultureInfo.InvariantCulture);
        VmsTotalDiskCountText.Text = vms.TotalInstalledDiskCount.ToString(CultureInfo.InvariantCulture);
        VmsFreeBaysText.Text = vms.FreeBays.ToString(CultureInfo.InvariantCulture);
        VmsRaidStatusText.Text = vms.FitsInBays ? "Fits in 1 device" : $"Requires {vms.StorageDeviceCount} devices";
        VmsStorageDeviceCountText.Text = vms.StorageDeviceCount.ToString(CultureInfo.InvariantCulture);
        VmsStorageDeviceTypeText.Text = vms.StorageDeviceSummary;
        VmsTotalBayCapacityText.Text = vms.TotalBayCapacity.ToString(CultureInfo.InvariantCulture);
        VmsFreeBaysAllText.Text = vms.FreeBaysAcrossDevices.ToString(CultureInfo.InvariantCulture);
        VmsDeviceRequirementText.Text = vms.StorageDeviceCount == 1
            ? "1 storage device is sufficient."
            : $"{vms.StorageDeviceCount} storage devices are required automatically.";
        VmsStorageDeviceNoteText.Text =
            $"Automatic sizing: {vms.TotalInstalledDiskCount} installed disks ÷ {vms.BayCount} bays/device → {vms.StorageDeviceCount} × {vms.BayCount}-Bay storage device(s).";

        ArchitectureText.Text = $"VMS Storage: {vms.StorageDeviceSummary} • {vms.RaidType} • {vms.RaidGroupCount} RAID group(s) × {vms.DisksPerRaidGroup} data disk(s) + {vms.HotSpareCount} hot spare(s) = {vms.TotalInstalledDiskCount} installed disk(s).";
        VmsRaidDetailsText.Text =
            $"Required usable: {vms.RequiredUsableTb:N2} TB\n" +
            $"RAID groups: {vms.RaidGroupCount} × {vms.DisksPerRaidGroup} data disks\n" +
            $"Hot spare disks: {vms.HotSpareCount} (manual)\n" +
            $"Raw data-disk capacity: {vms.RawCapacityTb:N2} TB\n" +
            $"RAID usable capacity: {vms.UsableCapacityTb:N2} TB\n" +
            $"Unused usable capacity: {vms.UnusedUsableTb:N2} TB\n" +
            $"Storage devices required: {vms.StorageDeviceCount} × {vms.BayCount}-Bay\n" +
            $"Total physical bays: {vms.TotalBayCapacity}\n" +
            $"Free bays across devices: {vms.FreeBaysAcrossDevices}\n" +
            $"Capacity rule: {vms.CapacityFormula}\n" +
            $"Fault tolerance: {vms.FaultTolerance}\n" +
            vms.StatusText;

        VmsRequiredUsableText.ToolTip = "This value is the calculated storage requirement before RAID capacity is applied.";
    }

    private void ClearStorageResult()
    {
        RequiredTbText.Text = "— TB";
        RequiredGbText.Text = "— GB";
        PerCameraText.Text = "—";
        AllCamerasText.Text = "—";
        DiskCountText.Text = "—";
        SummaryText.Text = "Press CALCULATE STORAGE after selecting the required camera and retention settings.";
        CalculationNoteText.Text = "Smart settings update the recommendation immediately; final storage is calculated only when the button is pressed.";
        ClearCalculatedVmsDetailsOnly();
        UpdateArchitectureModeText();
        StatusText.Text = "Ready - press CALCULATE STORAGE";
    }

    private void ClearCalculatedVmsDetailsOnly()
    {
        if (!_isLoaded) return;
        VmsRequiredUsableText.Text = "— TB";
        VmsBayCapacityText.Text = $"{GetSelected(BayTypeBox)} • {GetSelected(DiskBox)} TB/disk";
        VmsDiskCountText.Text = "—";
        VmsDataDisksPerGroupText.Text = "—";
        VmsTotalDiskCountText.Text = "—";
        VmsFreeBaysText.Text = "—";
        VmsStorageDeviceCountText.Text = "—";
        VmsStorageDeviceTypeText.Text = "—";
        VmsTotalBayCapacityText.Text = "—";
        VmsFreeBaysAllText.Text = "—";
        VmsDeviceRequirementText.Text = "—";
        VmsStorageDeviceNoteText.Text = "Automatic storage-device sizing will appear after CALCULATE STORAGE.";
        VmsRaidStatusText.Text = "Calculate storage first";
        VmsRaidDetailsText.Text = "VMS mode: RAID details will appear after CALCULATE STORAGE.";
        if (_isVmsMode)
            ArchitectureText.Text = "VMS Storage: calculate storage to see RAID and bay details.";
    }

    private void UpdateVmsPanel()
    {
        VmsStoragePanel.Visibility = _isVmsMode ? Visibility.Visible : Visibility.Collapsed;
        ModeTitleText.Text = _isVmsMode ? "VMS MODE" : "NVR MODE";
        ModeDescriptionText.Text = _isVmsMode
            ? "Storage is calculated first, then the selected bay type and RAID system are sized from that result."
            : "Storage is calculated for the selected NVR/storage disks.";
        ModeIconText.Text = _isVmsMode ? "▤" : "▣";
    }

    private void UpdateArchitectureModeText(int cameras = -1, int disks = -1, double diskTb = 0)
    {
        if (!_isLoaded) return;
        if (cameras < 0) cameras = GetInt(CameraCountBox.Text, 1);
        if (disks < 0) disks = 1;
        if (diskTb <= 0) diskTb = GetDouble(GetSelected(DiskBox), 6);

        if (!_isVmsMode)
            ArchitectureText.Text = $"NVR Storage: {cameras:N0} camera(s), {disks:N0} × {diskTb:N0} TB disk(s) by basic capacity.";
        else
            ArchitectureText.Text = "VMS Storage: calculate storage to see RAID and bay details.";
    }

    private void NvrButton_Click(object sender, RoutedEventArgs e)
    {
        _isVmsMode = false;
        NvrButton.Style = (Style)FindResource("GlowButton");
        VmsButton.Style = (Style)FindResource("ModeButton");
        UpdateVmsPanel();
        ClearStorageResult();
    }

    private void VmsButton_Click(object sender, RoutedEventArgs e)
    {
        _isVmsMode = true;
        VmsButton.Style = (Style)FindResource("GlowButton");
        NvrButton.Style = (Style)FindResource("ModeButton");
        UpdateVmsPanel();
        ClearStorageResult();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyDefaults();
        NvrButton.Style = (Style)FindResource("GlowButton");
        VmsButton.Style = (Style)FindResource("ModeButton");
        UpdateInputStates();
        UpdateSmartRecommendation();
        UpdateVmsPanel();
        ClearStorageResult();
    }

    private void DarkThemeButton_Click(object sender, RoutedEventArgs e) => ApplyTheme(true);
    private void LightThemeButton_Click(object sender, RoutedEventArgs e) => ApplyTheme(false);

    private void ApplyTheme(bool dark)
    {
        _isDark = dark;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        dictionaries.Add(new ResourceDictionary { Source = new Uri(dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative) });

        LogoImage.Source = new BitmapImage(new Uri(dark ? "Assets/SNAPPY-Logo-Dark.png" : "Assets/SNAPPY-Logo-Light.png", UriKind.Relative));
        HeaderIconImage.Source = new BitmapImage(new Uri("Assets/SNAPPY-icon.png", UriKind.Relative));

        DarkThemeButton.Style = (Style)FindResource(dark ? "GlowButton" : "ModeButton");
        LightThemeButton.Style = (Style)FindResource(dark ? "ModeButton" : "GlowButton");
        NvrButton.Style = (Style)FindResource(_isVmsMode ? "ModeButton" : "GlowButton");
        VmsButton.Style = (Style)FindResource(_isVmsMode ? "GlowButton" : "ModeButton");
    }

    private string GetCalculationMode() => GetSelected(CalculationModeBox) switch
    {
        "Manual Bitrate" => "Manual Bitrate",
        _ => "Smart / Recommended"
    };

    private static string GetSelected(ComboBox box) =>
        box.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? string.Empty : box.SelectedItem?.ToString() ?? string.Empty;

    private static string GetMegapixelKey(string value)
    {
        int index = value.IndexOf("MP", StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? value[..(index + 2)] : "2MP";
    }

    private static int GetInt(string? value, int fallback) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback;
    private static double GetDouble(string? value, double fallback) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) ? n : fallback;
    private static string FormatMbps(double kbps) => $"{kbps / 1000.0:0.00} Mbps";
}
