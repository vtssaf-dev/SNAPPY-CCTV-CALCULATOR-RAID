using AndroidPdfDocument = global::Android.Graphics.Pdf.PdfDocument;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;

namespace SNAPPY.CCTV.DiskCalculator.Android;

public partial class MainPage : ContentPage
{
    bool _dark = true;
    bool _smart = true;
    bool _calculated;
    Calculation _last = new();

    Image _logo = null!;
    Picker _encoding = null!, _resolution = null!, _fps = null!, _bitrateType = null!, _vbr = null!, _scene = null!, _retentionType = null!, _bay = null!, _raid = null!, _disk = null!;
    Entry _cameras = null!, _bitrate = null!, _retention = null!, _hours = null!, _overhead = null!, _groups = null!, _hotSpare = null!;
    Label _smartBitrate = null!, _result = null!, _vmsResult = null!, _status = null!;
    readonly List<VisualElement> _themeElements = [];
    Button _calculate = null!, _pdf = null!, _theme = null!;

    readonly string[] Encodings = ["H.265+", "H.265", "H.264+", "H.264"];
    readonly string[] Resolutions = ["1 MP - 1280×720", "2 MP - 1920×1080", "3 MP - 2048×1536", "4 MP - 2560×1440", "5 MP - 2560×1920", "6 MP - 3072×2048", "8 MP - 3840×2160", "10 MP - 3648×2736", "12 MP - 4000×3000"];
    readonly string[] Fps = Enumerable.Range(1, 30).Select(x => x.ToString()).ToArray();
    readonly string[] BitrateTypes = ["CBR", "VBR"];
    readonly string[] VbrLevels = Enumerable.Range(1, 6).Select(x => $"Level {x}").ToArray();
    readonly string[] Scenes = ["Low", "Normal", "High", "Very High"];
    readonly string[] Bays = ["4-Bay", "8-Bay", "16-Bay", "24-Bay"];
    readonly string[] Raids = ["RAID 1", "RAID 5", "RAID 6", "RAID 10"];
    readonly string[] Disks = Enumerable.Range(2, 23).Select(x => $"{x} TB").ToArray();

    public MainPage()
    {
        InitializeComponent();
        BuildUi();
        ApplyTheme();
        UpdateSmartBitrate();
    }

    void BuildUi()
    {
        Root.Clear();
        _logo = new Image { Source = "snappy_logo.png", HeightRequest = 34, HorizontalOptions = LayoutOptions.Start };
        _theme = Button("☀ / ☾  Theme", ThemeChanged, 12);
        _theme.HorizontalOptions = LayoutOptions.End;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }, Margin = new Thickness(0, 0, 0, 4) };
        header.Add(_logo); header.Add(_theme, 1);
        Root.Add(header);
        Root.Add(Label("SNAPPY CCTV DISK CALCULATOR", 22, true));
        Root.Add(Label("Camera bitrate, retention, VMS storage, RAID groups and hot-spare planning", 12, false));

        var mode = new HorizontalStackLayout { Spacing = 8 };
        mode.Add(Button("SMART", (_, _) => { _smart = true; UpdateSmartBitrate(); }, 12));
        mode.Add(Button("MANUAL", (_, _) => { _smart = false; UpdateSmartBitrate(); }, 12));
        Root.Add(mode);

        Root.Add(Card("CAMERA CONFIGURATION", CameraSection()));
        Root.Add(Card("STORAGE CONFIGURATION", StorageSection()));
        Root.Add(Card("VMS STORAGE BAY & RAID", VmsSection()));

        _calculate = Button("CALCULATE STORAGE", CalculateClicked, 15, true);
        Root.Add(_calculate);
        _pdf = Button("EXPORT TOTAL RESULT AS PDF", ExportPdfClicked, 14, true);
        _pdf.IsEnabled = false;
        Root.Add(_pdf);
        Root.Add(Card("TOTAL RESULT", ResultSection()));
        Root.Add(Card("VMS RESULT", VmsResultSection()));
        _status = Label("Ready", 12, false);
        Root.Add(_status);
    }

    View CameraSection()
    {
        _cameras = Entry("16", Keyboard.Numeric);
        _encoding = Picker(Encodings, 0);
        _resolution = Picker(Resolutions, 1);
        _fps = Picker(Fps, 24);
        _bitrateType = Picker(BitrateTypes, 1);
        _vbr = Picker(VbrLevels, 5);
        _scene = Picker(Scenes, 1);
        _bitrate = Entry("2048", Keyboard.Numeric);
        _smartBitrate = Label("Recommended: 2048 Kbps | Estimated average: 1894 Kbps", 12, false);
        _resolution.SelectedIndexChanged += (_, _) => UpdateSmartBitrate();
        _fps.SelectedIndexChanged += (_, _) => UpdateSmartBitrate();
        _encoding.SelectedIndexChanged += (_, _) => UpdateSmartBitrate();
        _bitrateType.SelectedIndexChanged += (_, _) => UpdateSmartBitrate();
        _vbr.SelectedIndexChanged += (_, _) => UpdateSmartBitrate();
        _scene.SelectedIndexChanged += (_, _) => UpdateSmartBitrate();
        return FormGrid(("Camera Count", _cameras), ("Encoding", _encoding), ("Resolution", _resolution), ("Frame Rate", _fps), ("Bitrate Type", _bitrateType), ("VBR Level", _vbr), ("Scene Complexity", _scene), ("Manual Bitrate (Kbps)", _bitrate), ("Smart Bitrate", _smartBitrate));
    }

    View StorageSection()
    {
        _retentionType = Picker(["Days", "Months"], 0);
        _retention = Entry("30", Keyboard.Numeric);
        _hours = Entry("24", Keyboard.Numeric);
        _overhead = Entry("10", Keyboard.Numeric);
        _disk = Picker(Disks, 4); // 6 TB
        return FormGrid(("Retention Unit", _retentionType), ("Retention", _retention), ("Recording Hours / Day", _hours), ("Storage Overhead %", _overhead), ("Disk Size", _disk));
    }

    View VmsSection()
    {
        _bay = Picker(Bays, 3);
        _raid = Picker(Raids, 2);
        _groups = Entry("3", Keyboard.Numeric);
        _hotSpare = Entry("2", Keyboard.Numeric);
        return FormGrid(("Storage Bay Type", _bay), ("RAID System", _raid), ("RAID Groups", _groups), ("Hot Spare Disks", _hotSpare));
    }

    View ResultSection()
    {
        _result = Label("Press CALCULATE STORAGE", 15, true);
        return new VerticalStackLayout
        {
            Spacing = 5,
            Children = { _result, Label("The calculation uses the selected camera bitrate and retention.", 11, false) }
        };
    }

    View VmsResultSection()
    {
        _vmsResult = Label("VMS result will appear here.", 15, true);
        return new VerticalStackLayout { Spacing = 5, Children = { _vmsResult } };
    }

    Grid FormGrid(params (string, View)[] fields)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }, RowSpacing = 9, ColumnSpacing = 10 };
        for (int i = 0; i < fields.Length; i++)
        {
            int r = i / 2;
            if (g.RowDefinitions.Count <= r) g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var box = new VerticalStackLayout { Spacing = 4 };
            box.Add(Label(fields[i].Item1, 11, true)); box.Add(fields[i].Item2);
            g.Add(box, i % 2, r);
        }
        return g;
    }

    Border Card(string title, View content)
    {
        var b = new Border { StrokeShape = new RoundRectangle { CornerRadius = 14 }, StrokeThickness = 1, Padding = 14, Content = new VerticalStackLayout { Spacing = 8, Children = { Label(title, 15, true), content } } };
        _themeElements.Add(b);
        return b;
    }

    Label Label(string text, double size, bool bold) { var v = new Label { Text = text, FontSize = size, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, LineBreakMode = LineBreakMode.WordWrap }; _themeElements.Add(v); return v; }
    Entry Entry(string text, Keyboard keyboard) { var v = new Entry { Text = text, Keyboard = keyboard, HeightRequest = 44, FontSize = 14 }; _themeElements.Add(v); return v; }
    Picker Picker(IEnumerable<string> items, int index) { var v = new Picker { ItemsSource = items.ToList(), SelectedIndex = index, HeightRequest = 44, FontSize = 14 }; _themeElements.Add(v); return v; }
    Button Button(string text, EventHandler click, double size, bool primary = false) { var v = new Button { Text = text, FontSize = size, FontAttributes = FontAttributes.Bold, HeightRequest = 48, CornerRadius = 10, Padding = new Thickness(12, 6) }; v.Clicked += click; _themeElements.Add(v); return v; }

    void ThemeChanged(object? s, EventArgs e) { _dark = !_dark; ApplyTheme(); }
    void ApplyTheme()
    {
        var bg = _dark ? Color.FromArgb("#06101F") : Color.FromArgb("#F3F7FB");
        var card = _dark ? Color.FromArgb("#0B1930") : Colors.White;
        var input = _dark ? Color.FromArgb("#0A2342") : Colors.White;
        var fg = _dark ? Colors.White : Colors.Black;
        var muted = _dark ? Color.FromArgb("#C5D6E8") : Color.FromArgb("#44515E");
        var border = _dark ? Color.FromArgb("#168FE8") : Color.FromArgb("#A8B8C8");
        BackgroundColor = bg;
        Root.BackgroundColor = bg;
        foreach (var v in _themeElements)
        {
            if (v is Label l) l.TextColor = l.FontAttributes == FontAttributes.Bold ? fg : muted;
            if (v is Entry e) { e.TextColor = fg; e.PlaceholderColor = muted; e.BackgroundColor = input; e.Unfocused += (_, _) => { }; }
            if (v is Picker p) { p.TextColor = fg; p.BackgroundColor = input; }
            if (v is Border borderView) { borderView.BackgroundColor = card; borderView.Stroke = border; }
            if (v is Button buttonView) { buttonView.TextColor = Colors.White; buttonView.BackgroundColor = Color.FromArgb(_dark ? "#087FDB" : "#1268A8"); }
        }
        _status.TextColor = muted;
    }

    void UpdateSmartBitrate()
    {
        if (_resolution == null) return;
        double[] baseRates = [1024, 2048, 2048, 3072, 3072, 4096, 4096, 5120, 6144];
        double[] enc = [1.0, 1.3, 1.5, 1.85];
        double[] vbr = [0.55, 0.65, 0.75, 0.85, 0.93, 1.0];
        double[] scene = [0.8, 1.0, 1.1, 1.2];
        int r = Math.Clamp(_resolution.SelectedIndex, 0, 8), f = Math.Clamp(_fps.SelectedIndex + 1, 1, 30), e = Math.Clamp(_encoding.SelectedIndex, 0, 3), v = Math.Clamp(_vbr.SelectedIndex, 0, 5), s = Math.Clamp(_scene.SelectedIndex, 0, 3);
        double recommended = baseRates[r] * (f / 25.0) * enc[e];
        double average = recommended * scene[s] * (_bitrateType.SelectedIndex == 1 ? vbr[v] : 1.0);
        average = Math.Clamp(average, 64, recommended * 1.30);
        _smartBitrate.Text = $"Recommended: {recommended:N0} Kbps | Estimated average: {average:N0} Kbps";
        if (_smart) _bitrate.Text = Math.Round(average).ToString(CultureInfo.InvariantCulture);
    }

    void CalculateClicked(object? sender, EventArgs e)
    {
        try
        {
            UpdateSmartBitrate();
            int cameras = I(_cameras.Text, 1), fps = _fps.SelectedIndex + 1;
            double bitrate = _smart ? D(_smartBitrate.Text.Split('|')[1].Replace("Estimated average:", "").Replace("Kbps", "")) : D(_bitrate.Text);
            double retention = D(_retention.Text);
            double days = _retentionType.SelectedIndex == 1 ? retention * 30 : retention;
            double hours = Math.Clamp(D(_hours.Text), 0, 24), overhead = Math.Max(0, D(_overhead.Text));
            double perCamDayGb = bitrate * 1000 * 86400 / 8 / 1_000_000_000 * (hours / 24);
            double requiredGb = perCamDayGb * cameras * days * (1 + overhead / 100);
            double requiredTb = requiredGb / 1000;
            int diskTb = _disk.SelectedIndex + 2, groups = Math.Max(1, I(_groups.Text, 1)), hot = Math.Max(0, I(_hotSpare.Text, 0));
            string raid = _raid.SelectedItem?.ToString() ?? "RAID 6";
            int min = raid switch { "RAID 1" => 2, "RAID 5" => 3, "RAID 6" => 4, _ => 4 };
            double usableFactor = raid switch { "RAID 1" => 0.5, "RAID 5" => (1.0 - 1.0 / 3.0), "RAID 6" => (1.0 - 2.0 / 4.0), _ => 0.5 };
            int dataPerGroup = Math.Max(min, (int)Math.Ceiling((requiredTb / groups) / (diskTb * usableFactor)));
            if (raid == "RAID 10" && dataPerGroup % 2 != 0) dataPerGroup++;
            if (raid == "RAID 6" && dataPerGroup < 4) dataPerGroup = 4;
            if (raid == "RAID 5" && dataPerGroup < 3) dataPerGroup = 3;
            if (raid == "RAID 10" && dataPerGroup < 4) dataPerGroup = 4;
            int dataDisks = dataPerGroup * groups, installed = dataDisks + hot, bay = (new[] { 4, 8, 16, 24 })[_bay.SelectedIndex];
            int devices = Math.Max(1, (int)Math.Ceiling(installed / (double)bay));
            double usable = raid switch { "RAID 1" => dataDisks / 2.0 * diskTb, "RAID 5" => groups * (dataPerGroup - 1) * diskTb, "RAID 6" => groups * (dataPerGroup - 2) * diskTb, _ => dataDisks / 2.0 * diskTb };
            int totalBays = devices * bay;
            _last = new Calculation(cameras, bitrate, days, requiredTb, diskTb, bay, raid, groups, dataPerGroup, dataDisks, hot, installed, devices, totalBays, usable);
            _result.Text = $"Required usable storage: {requiredTb:N2} TB\nDaily storage: {perCamDayGb * cameras:N2} GB/day\nBitrate used: {bitrate:N0} Kbps\nCameras: {cameras} | FPS: {fps}";
            _vmsResult.Text = $"RAID: {raid} | Groups: {groups}\nData disks/group: {dataPerGroup} | Data disks: {dataDisks}\nHot spare: {hot} | Total installed: {installed}\nStorage devices required: {devices} × {bay}-Bay\nTotal bays: {totalBays} | Free bays: {totalBays - installed}\nRAID usable capacity: {usable:N2} TB\nStatus: {(usable >= requiredTb ? "CAPACITY OK" : "NOT ENOUGH CAPACITY")}";
            _status.Text = "Calculation completed";
            _calculated = true;
            _pdf.IsEnabled = true;
            ApplyTheme();
        }
        catch (Exception ex) { _ = DisplayAlertAsync("SNAPPY", ex.Message, "OK"); }
    }

    async void ExportPdfClicked(object? sender, EventArgs e)
    {
        if (!_calculated && _last.RequiredTb <= 0) { await DisplayAlertAsync("SNAPPY", "Please calculate storage first.", "OK"); return; }
        try
        {
            string fileName = $"SNAPPY-CCTV-Storage-Result-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
            string path = global::System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);

            // Build and completely close the PDF before handing it to Android.
            // This prevents other apps in the Android Share Sheet from seeing a
            // still-open/incomplete file.
            using (var doc = new AndroidPdfDocument())
            {
                var page = doc.StartPage(new AndroidPdfDocument.PageInfo.Builder(595, 842, 1).Create())
                    ?? throw new InvalidOperationException("Unable to create the PDF page.");
                var canvas = page.Canvas
                    ?? throw new InvalidOperationException("Unable to access the PDF canvas.");
                var paint = new global::Android.Graphics.Paint { Color = global::Android.Graphics.Color.Black, TextSize = 14 };
                var title = new global::Android.Graphics.Paint { Color = global::Android.Graphics.Color.Rgb(0, 105, 180), TextSize = 22 };
                int y = 45;
                canvas.DrawText("SNAPPY CCTV DISK CALCULATOR", 35, y, title); y += 30;
                canvas.DrawText($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", 35, y, paint); y += 30;

                foreach (var line in ReportLines())
                {
                    if (y > 805)
                    {
                        doc.FinishPage(page);
                        page = doc.StartPage(new AndroidPdfDocument.PageInfo.Builder(595, 842, 1).Create())
                            ?? throw new InvalidOperationException("Unable to create the next PDF page.");
                        canvas = page.Canvas
                            ?? throw new InvalidOperationException("Unable to access the next PDF canvas.");
                        y = 45;
                    }

                    canvas.DrawText(line, 35, y, paint);
                    y += 21;
                }

                doc.FinishPage(page);
                using var stream = global::System.IO.File.Create(path);
                doc.WriteTo(stream);
            }

            // The PDF is now fully closed. MAUI opens the native Android
            // Sharesheet so the user can Save, WhatsApp, Gmail, Drive, etc.
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share SNAPPY CCTV Storage Result PDF",
                File = new ShareFile(path)
            });

            _status.Text = "PDF ready — Share/Save completed";
        }
        catch (Exception ex) { await DisplayAlertAsync("PDF Export", ex.Message, "OK"); }
    }

    IEnumerable<string> ReportLines() => new[]
    {
        "TOTAL STORAGE RESULT", "", $"Cameras: {_last.Cameras}", $"Required usable storage: {_last.RequiredTb:N2} TB", $"Disk size: {_last.DiskTb} TB", $"Storage bay: {_last.Bay}-Bay", $"RAID: {_last.Raid}", $"RAID groups: {_last.Groups}", $"Data disks/group: {_last.DataPerGroup}", $"RAID data disks: {_last.DataDisks}", $"Hot spare disks: {_last.HotSpare}", $"Total installed disks: {_last.Installed}", $"Storage devices required: {_last.Devices} × {_last.Bay}-Bay", $"Total available bays: {_last.TotalBays}", $"Free bays: {_last.TotalBays - _last.Installed}", $"RAID usable capacity: {_last.UsableTb:N2} TB", $"Capacity status: {(_last.UsableTb >= _last.RequiredTb ? "OK" : "NOT ENOUGH CAPACITY")}", "", "SNAPPY VMS storage planning report"
    };

    static int I(string? s, int d) => int.TryParse(s, out var v) ? v : d;
    static double D(string? s) => double.TryParse(s?.Trim().Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    record Calculation(int Cameras = 0, double Bitrate = 0, double Days = 0, double RequiredTb = 0, int DiskTb = 0, int Bay = 0, string Raid = "", int Groups = 0, int DataPerGroup = 0, int DataDisks = 0, int HotSpare = 0, int Installed = 0, int Devices = 0, int TotalBays = 0, double UsableTb = 0);
}
