using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace EvonyCDB
{
    public partial class Form1 : Form
    {
        // ---------- Settings ----------
        private readonly string ImagesDir = "Images";
        private const double StrictThresh = 0.82;   // crisp UI (Go/Confirm/Alliance, etc.)
        private const double RelaxedThresh = 0.72;  // tabs/backgrounds/alt styles
        private const int ClickJitter = 3;          // px
        private const int WaitAfterGoMs = 1400;     // 1000–2000 per your spec
        private const int ShortDelayMs = 250;
        private readonly Random _rnd = new Random();

        public Form1()
        {
            InitializeComponent();

            if (comboBox1.Items.Count == 0)
                comboBox1.Items.AddRange(new object[] { "Evony.exe", "chrome.exe", "msedge.exe" });
            comboBox1.Text = "Evony.exe";

            this.Load += Form1_Load;
        }

        // =================== CONNECT (btnConnect) ===================
        private async void button1_Click(object sender, EventArgs e)
        {
            try
            {
                var name = comboBox1.Text?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Please enter a process name (e.g., Evony.exe).");
                    return;
                }

                using var finder = new ProcessFinder();
                if (finder.TryConnect(name, out string err))
                {
                    finder.FocusWindow();
                    finder.UpdateWindowRect();

                    lblStatus.Text = $"Connected to Evony (PID: {finder.TargetProcess.Id})";
                    lblStatus.ForeColor = Color.SeaGreen;
                    Log($"Connected (PID: {finder.TargetProcess.Id})");
                }
                else
                {
                    lblStatus.Text = $"Failed: {err}";
                    lblStatus.ForeColor = Color.Red;
                    LogError(err);
                }
            }
            catch (Exception ex)
            {
                LogError(ex.ToString());
            }

            await Task.CompletedTask;
        }

        // =================== ACTIVATE (btnActivate) ===================
        private async void btnActivate_Click(object sender, EventArgs e)
        {
            await ActivatePipelineAsync();
        }

        private async Task ActivatePipelineAsync()
        {
            try
            {
                ClearLog();
                Log("== Activate clicked, starting pipeline ==");

                // Ensure images folder + a key template present
                var imagesPath = System.IO.Path.GetFullPath(ImagesDir);
                if (!System.IO.Directory.Exists(imagesPath))
                {
                    LogError($"Images folder not found: {imagesPath}");
                    return;
                }
                var mustHave = System.IO.Path.Combine(imagesPath, "CoordTab.png");
                if (!System.IO.File.Exists(mustHave))
                {
                    LogError($"Missing template: {mustHave} (set 'Copy to Output Directory' = Copy always)");
                    return;
                }

                // Connect + focus
                var name = comboBox1.Text?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Please enter a process name (e.g., Evony.exe or chrome.exe).");
                    return;
                }

                using var procFinder = new ProcessFinder();
                if (!procFinder.TryConnect(name, out string err))
                {
                    lblStatus.Text = $"Failed: {err}";
                    lblStatus.ForeColor = Color.Red;
                    LogError(err);
                    return;
                }
                procFinder.FocusWindow();
                procFinder.UpdateWindowRect();

                lblStatus.Text = $"Connected to Evony (PID: {procFinder.TargetProcess.Id})";
                lblStatus.ForeColor = Color.SeaGreen;
                Log($"Connected (PID: {procFinder.TargetProcess.Id})");

                // Parse targets (tolerant regex)
                var targets = ParseTargets(CoordsRichTextBox.Text);
                Log($"Parsed {targets.Count} targets.");
                if (targets.Count == 0)
                {
                    LogWarn("No valid coordinates parsed. Expected: Lv21 Azazel (xy: 460 589) 144km");
                    return;
                }

                progressBar1.Minimum = 0;
                progressBar1.Maximum = targets.Count;
                progressBar1.Value = 0;

                var matcher = new TemplateFinder(ImagesDir);

                // Use for-loop for reliable last-item execution + index logging
                for (int idx = 0; idx < targets.Count; idx++)
                {
                    var t = targets[idx];
                    string caption = $"Target #{idx + 1}";
                    Log($"-- [{idx + 1}/{targets.Count}] Processing {caption} @ ({t.X},{t.Y}) --");

                    // STEP 1: CoordTab or CoordTab2
                    Log("Step 1: Find CoordTab/CoordTab2…");
                    if (!await matcher.FindAndClickAsync(procFinder.MainWindowHandle, new[] { "CoordTab.png", "CoordTab2.png" }, RelaxedThresh, ClickJitter))
                    {
                        LogWarn("Coord tab not found. Skipping target.");
                        FinishCurrentTarget();
                        continue;
                    }
                    await Task.Delay(ShortDelayMs);

                    // STEP 2: Fill X and Y using a single template coordsXY.png (no double typing)
                    Log("Step 2: Locate coordsXY and fill X/Y …");
                    var findXY = matcher.FindTemplateRectWindowCoords(
                        procFinder.MainWindowHandle,
                        "coordsXY.png",
                        threshold: 0.82,
                        minScale: 0.90, maxScale: 1.10, step: 0.05);

                    if (!findXY.ok)
                    {
                        LogWarn($"coordsXY.png not found (best score={findXY.score:F3}). Skipping target.");
                        FinishCurrentTarget();
                        continue;
                    }

                    const int margin = 10; // pixels from edges for safe clicks
                    int xyLeft = findXY.winRect.X;
                    int xyRight = findXY.winRect.X + findXY.winRect.Width;
                    int xyMidY = findXY.winRect.Y + findXY.winRect.Height / 2;

                    // X (left)
                    var xWinPoint = new System.Drawing.Point(xyLeft + margin, xyMidY);
                    var xScreen = WindowCoordToScreen(procFinder.MainWindowHandle, xWinPoint.X, xWinPoint.Y);
                    xScreen.X += _rnd.Next(-ClickJitter, ClickJitter + 1);
                    xScreen.Y += _rnd.Next(-ClickJitter, ClickJitter + 1);
                    ClickScreen(xScreen);
                    await Task.Delay(120);
                    SendKeysToWindow("^{a}");
                    SendKeysToWindow("{DEL}");
                    SendKeysToWindow(t.X.ToString());
                    await Task.Delay(120);

                    // Y (right)
                    var yWinPoint = new System.Drawing.Point(xyRight - margin, xyMidY);
                    var yScreen = WindowCoordToScreen(procFinder.MainWindowHandle, yWinPoint.X, yWinPoint.Y);
                    yScreen.X += _rnd.Next(-ClickJitter, ClickJitter + 1);
                    yScreen.Y += _rnd.Next(-ClickJitter, ClickJitter + 1);
                    ClickScreen(yScreen);
                    await Task.Delay(120);
                    SendKeysToWindow("^{a}");
                    SendKeysToWindow("{DEL}");
                    SendKeysToWindow(t.Y.ToString());
                    await Task.Delay(120);

                    // Go
                    if (!await matcher.FindAndClickAsync(procFinder.MainWindowHandle, new[] { "GoButton.png" }, StrictThresh, ClickJitter))
                    { LogWarn("Go button not found. Skipping."); FinishCurrentTarget(); continue; }

                    await Task.Delay(WaitAfterGoMs);

                    // STEP 3: click center to open the boss panel
                    Log("Step 3: Click center…");
                    ClickWindowCenter(procFinder.MainWindowHandle, ClickJitter);
                    await Task.Delay(250); // allow panel to animate in

                    // STEP 4: Find Attack (multi-scale + edges, ROI) — ONLY Attack_alt.png
                    Log("Step 4: Find Attack (multi-scale + edges, ROI)…");

                    GetWindowRect(procFinder.MainWindowHandle, out RECT wr);
                    int winW = wr.Right - wr.Left, winH = wr.Bottom - wr.Top;
                    int centerX = winW / 2, centerY = winH / 2;

                    int roiW = Math.Max(200, (int)(winW * 0.45));
                    int roiH = Math.Max(200, (int)(winH * 0.45));
                    var searchRectWin = new System.Drawing.Rectangle(
                        Math.Max(0, centerX - roiW / 2),
                        Math.Max(0, centerY - roiH / 2),
                        Math.Min(roiW, winW),
                        Math.Min(roiH, winH)
                    );

                    bool attackShown = false;
                    double lastScore = 0;

                    for (int i = 0; i < 3 && !attackShown; i++)
                    {
                        (attackShown, lastScore) = await matcher.FindAndClickMultiScaleAsync(
                            procFinder.MainWindowHandle,
                            new[] { "Attack_alt.png" }, // ✅ only alt
                            threshold: 0.80,
                            jitter: ClickJitter,
                            searchRectWin: searchRectWin,
                            minScale: 0.85, maxScale: 1.20, step: 0.05,
                            useEdges: true);

                        Log($"Attack search try {i + 1}: best score={lastScore:F3}");
                        if (!attackShown)
                        {
                            await Task.Delay(350);
                            ClickWindowCenter(procFinder.MainWindowHandle, ClickJitter);
                            await Task.Delay(350);
                        }
                    }
                    /*
                    if (!attackShown)
                    {
                        using (var dbg = CaptureWindowMat(procFinder.MainWindowHandle))
                        {
                            var dbgPath = System.IO.Path.Combine(imagesPath, $"_debug_attack_fail_{DateTime.Now:HHmmss}.png");
                            if (!dbg.Empty()) Cv2.ImWrite(dbgPath, dbg);
                            LogWarn($"Attack not found. Saved debug shot: {dbgPath}");
                        }
                        FinishCurrentTarget();
                        continue;
                    }
                    await Task.Delay(ShortDelayMs);
                    */
                    // =================== FAST STEP 5 (single pass + cache + ROI) ===================
                    Log("Step 5: Verify boss/level overlay (fast single-pass)...");
                    var verifyPool = VerifyImagePool(); // keep this tight if you can (filtered by selection)
                    var fastRoi = matcher.MonsterPanelRoi(procFinder.MainWindowHandle);

                    // Try tight scale first, optionally a couple of fallbacks if needed
                    var foundRect = matcher.FindAnyTemplateRectWindowCoordsFast(
                        procFinder.MainWindowHandle,
                        verifyPool,
                        threshold: 0.80,
                        searchRectWin: fastRoi,
                        scale: 1.0);

                    if (!foundRect.ok)
                    {
                        // slight fallbacks if UI occasionally scales
                        foundRect = matcher.FindAnyTemplateRectWindowCoordsFast(
                            procFinder.MainWindowHandle, verifyPool, 0.80, fastRoi, 0.95);
                        if (!foundRect.ok)
                            foundRect = matcher.FindAnyTemplateRectWindowCoordsFast(
                                procFinder.MainWindowHandle, verifyPool, 0.80, fastRoi, 1.05);
                    }

                    if (!foundRect.ok)
                    {
                        Log("Not a desired boss. Skipping…");
                        /* await matcher.FindAndClickAsync(procFinder.MainWindowHandle, new[] { "WorldMapGrass.png" }, RelaxedThresh, ClickJitter); */
                        FinishCurrentTarget();
                        continue;
                    }

                    // Click just outside left of the verified overlay immediately (short delay)
                    {
                        int clickX = Math.Max(5, foundRect.winRect.X - 60);
                        int clickY = foundRect.winRect.Y + foundRect.winRect.Height / 2;

                        var screenPt = WindowCoordToScreen(procFinder.MainWindowHandle, clickX, clickY);
                        screenPt.X += _rnd.Next(-ClickJitter, ClickJitter + 1);
                        screenPt.Y += _rnd.Next(-ClickJitter, ClickJitter + 1);

                        Log($"Verified via {foundRect.matchedFile} (score={foundRect.score:F3}). Clicking left outside panel…");
                        ClickScreen(screenPt);
                        await Task.Delay(120); // shorter settle
                    }

                    // STEP 6: Click center to re-open the monster popup
                    Log("Step 6: Click center to open panel again…");
                    ClickWindowCenter(procFinder.MainWindowHandle, ClickJitter);
                    await Task.Delay(300);

                    // STEP 7: Find Share button (primary or alt)
                    Log("Step 7: Find Share button…");
                    if (!await matcher.FindAndClickAsync(procFinder.MainWindowHandle, new[] { "ShareButton.png", "ShareButton_alt.png" }, RelaxedThresh, ClickJitter))
                    {
                        LogWarn("Share button not found. Skipping.");
                        FinishCurrentTarget();
                        continue;
                    }
                    await Task.Delay(250);

                    // STEP 8: AllianceChat
                    Log("Step 8: Click Alliance chat…");
                    if (!await matcher.FindAndClickAsync(procFinder.MainWindowHandle, new[] { "AllianceChat.png" }, RelaxedThresh, ClickJitter))
                    {
                        LogWarn("Alliance chat button not found. Skipping.");
                        FinishCurrentTarget();
                        continue;
                    }
                    await Task.Delay(250);

                    // STEP 9: ConfirmShare
                    Log("Step 9: Confirm share…");
                    if (!await matcher.FindAndClickAsync(procFinder.MainWindowHandle, new[] { "ConfirmShare.png" }, StrictThresh, ClickJitter))
                    {
                        LogWarn("Confirm share not found. Skipping.");
                        FinishCurrentTarget();
                        continue;
                    }

                    Log($"OK: {caption} shared successfully.");
                    FinishCurrentTarget();
                    await Task.Delay(200);
                }

                // ensure bar full
                progressBar1.Value = progressBar1.Maximum;

                Log("== Activation finished ==");
                lblStatus.Text = "Status: Done";
            }
            catch (Exception ex)
            {
                LogError(ex.ToString());
            }
        }

        // =================== Coords parsing ===================
        private static readonly Regex TargetRegexAll = new Regex(
            @"(?ix)                                   # ignore case + allow comments
              \bLv \s* (?<level>\d+) \s+              # Lv + level
              (?<name> [^\(\r\n]+? )                  # name up to '(' or EOL (minimal)
              \s* (?: \([^\)]*\) )? \s*               # optional parenthetical like (Cavalry Troop)
              \( \s* x \s* y \s* : \s*                # (xy:
              (?<x> -?\d+ ) \s+ (?<y> -?\d+ ) \s*     # X Y
              \)                                      # )
            ",
            RegexOptions.Compiled
        );

        private static readonly Regex XYCoordsRegex = new Regex(
            @"(?ix)            # ignore case + allow comments
              \(\s* x \s* y    # '(' then 'xy' with optional spaces
              \s* : \s*        # colon
              (?<x> -?\d+ )    # X (allow negative just in case)
              \s+              # space
              (?<y> -?\d+ )    # Y
              \s* \)           # closing ')'
            ",
            RegexOptions.Compiled);

        private List<Target> ParseTargets(string input)
        {
            var list = new List<Target>();
            if (string.IsNullOrWhiteSpace(input)) return list;

            string normalized = input
                .Replace("\u00A0", " ")
                .Replace("\u2007", " ")
                .Replace("\u202F", " ");

            var matches = XYCoordsRegex.Matches(normalized);
            if (matches.Count == 0)
            {
                LogWarn("No (xy: X Y) pairs found. Example: (xy: 760 746)");
                return list;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in matches)
            {
                if (!m.Success) continue;
                if (!int.TryParse(m.Groups["x"].Value, out int x)) continue;
                if (!int.TryParse(m.Groups["y"].Value, out int y)) continue;

                string key = $"{x}|{y}";
                if (seen.Contains(key)) continue;
                seen.Add(key);

                list.Add(new Target { Level = 0, Name = "Coords", X = x, Y = y });
            }

            Log($"Parsed {list.Count} coordinate pairs from text.");
            return list;
        }

        private sealed class Target
        {
            public int Level;
            public string Name;
            public int X;
            public int Y;
        }

        // =================== Template Finder (OpenCV) ===================
        private sealed class TemplateFinder
        {
            private readonly string _baseDir;
            private readonly Random _rnd = new Random();

            // cache to avoid disk I/O / decode every call
            private readonly Dictionary<string, Mat> _templateCache = new(StringComparer.OrdinalIgnoreCase);

            public TemplateFinder(string baseDir) => _baseDir = baseDir;

            // ROI where monster panel appears (center area)
            public System.Drawing.Rectangle MonsterPanelRoi(IntPtr hwnd)
            {
                GetWindowRect(hwnd, out var wr);
                int winW = wr.Right - wr.Left, winH = wr.Bottom - wr.Top;
                int centerX = winW / 2, centerY = winH / 2;
                int roiW = Math.Max(220, (int)(winW * 0.42));
                int roiH = Math.Max(220, (int)(winH * 0.42));
                return new System.Drawing.Rectangle(
                    Math.Max(0, centerX - roiW / 2),
                    Math.Max(0, centerY - roiH / 2),
                    Math.Min(roiW, winW),
                    Math.Min(roiH, winH)
                );
            }

            // Fast single-scale, single-pass finder that ALSO returns rect (with early exit)
            public (bool ok, OpenCvSharp.Rect winRect, string matchedFile, double score)
                FindAnyTemplateRectWindowCoordsFast(
                    IntPtr hwnd,
                    IEnumerable<string> images,
                    double threshold,
                    System.Drawing.Rectangle? searchRectWin = null,
                    double scale = 1.0)
            {
                using var hayFull = CaptureWindowMat(hwnd);
                if (hayFull.Empty()) return (false, default, null, 0);

                // Optional ROI
                Mat hay = hayFull;
                int offX = 0, offY = 0;
                if (searchRectWin.HasValue)
                {
                    var r = searchRectWin.Value;
                    var safe = new OpenCvSharp.Rect(
                        Math.Max(0, r.X), Math.Max(0, r.Y),
                        Math.Min(hayFull.Width - Math.Max(0, r.X), r.Width),
                        Math.Min(hayFull.Height - Math.Max(0, r.Y), r.Height));
                    hay = new Mat(hayFull, safe);
                    offX = safe.X; offY = safe.Y;
                }

                using var hayGray = hay.CvtColor(ColorConversionCodes.BGR2GRAY);

                double best = 0.0;
                OpenCvSharp.Rect bestRect = default;
                string bestFile = null;

                foreach (var file in images)
                {
                    var nGrayOrig = GetTemplate(file);
                    if (nGrayOrig is null || nGrayOrig.Empty()) continue;

                    Mat nGray = nGrayOrig;
                    bool disposeN = false;
                    if (Math.Abs(scale - 1.0) > 1e-9)
                    {
                        nGray = nGrayOrig.Resize(new OpenCvSharp.Size(), scale, scale, InterpolationFlags.Linear);
                        disposeN = true;
                    }

                    using (var res = hayGray.MatchTemplate(nGray, TemplateMatchModes.CCoeffNormed))
                    {
                        res.MinMaxLoc(out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                        if (maxVal > best)
                        {
                            best = maxVal;
                            bestRect = new OpenCvSharp.Rect(offX + maxLoc.X, offY + maxLoc.Y, nGray.Width, nGray.Height);
                            bestFile = file;

                            if (best >= threshold)
                            {
                                if (disposeN) nGray.Dispose();
                                return (true, bestRect, bestFile, best);
                            }
                        }
                    }

                    if (disposeN) nGray.Dispose();
                }

                return (best >= threshold, bestRect, bestFile, best);
            }

            // -------- Helpers (kept from your original) --------

            // Multiscale + optional edges + optional ROI; returns if clicked and best score
            public async Task<(bool ok, double score)> FindAndClickMultiScaleAsync(
                IntPtr hwnd,
                IEnumerable<string> images,
                double threshold,
                int jitter = 0,
                System.Drawing.Rectangle? searchRectWin = null,
                double minScale = 0.80,
                double maxScale = 1.25,
                double step = 0.05,
                bool useEdges = true)
            {
                using var hayFull = CaptureWindowMat(hwnd);
                if (hayFull.Empty()) return (false, 0);

                // ROI cropping (in window coords)
                Mat hay = hayFull;
                int roiOffsetX = 0, roiOffsetY = 0;
                if (searchRectWin.HasValue)
                {
                    var r = searchRectWin.Value;
                    if (r.Width > 10 && r.Height > 10)
                    {
                        var safe = new OpenCvSharp.Rect(
                            Math.Max(0, r.X),
                            Math.Max(0, r.Y),
                            Math.Min(hayFull.Width - Math.Max(0, r.X), r.Width),
                            Math.Min(hayFull.Height - Math.Max(0, r.Y), r.Height));
                        hay = new Mat(hayFull, safe); // submat (keeps hayFull alive)
                        roiOffsetX = safe.X;
                        roiOffsetY = safe.Y;
                    }
                }

                using var hayGray = hay.CvtColor(ColorConversionCodes.BGR2GRAY);
                Mat hayEdge = null;
                try
                {
                    if (useEdges)
                        hayEdge = CannyOf(hayGray);

                    double bestScore = 0;
                    System.Drawing.Point bestScreen = default;

                    foreach (var file in images)
                    {
                        using var needleOrig = LoadTemplate(file);
                        if (needleOrig is null || needleOrig.Empty()) continue;

                        using var nGrayOrig = needleOrig.CvtColor(ColorConversionCodes.BGR2GRAY);
                        Mat nEdgeOrig = null;
                        try
                        {
                            if (useEdges)
                                nEdgeOrig = CannyOf(nGrayOrig);

                            for (double s = minScale; s <= maxScale + 1e-9; s += step)
                            {
                                // ---- grayscale path
                                Mat nGray = nGrayOrig;
                                bool disposeNGray = false;
                                if (Math.Abs(s - 1.0) > 1e-9)
                                {
                                    nGray = nGrayOrig.Resize(new OpenCvSharp.Size(), s, s, InterpolationFlags.Linear);
                                    disposeNGray = true;
                                }

                                using (var res = hayGray.MatchTemplate(nGray, TemplateMatchModes.CCoeffNormed))
                                {
                                    res.MinMaxLoc(out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                                    if (maxVal > bestScore)
                                    {
                                        bestScore = maxVal;
                                        int cx = roiOffsetX + maxLoc.X + nGray.Width / 2;
                                        int cy = roiOffsetY + maxLoc.Y + nGray.Height / 2;
                                        bestScreen = WindowCoordToScreen(hwnd, cx, cy);
                                    }
                                }
                                if (disposeNGray) nGray.Dispose();

                                // ---- edge path
                                if (useEdges && hayEdge is not null && nEdgeOrig is not null)
                                {
                                    Mat nEdge = nEdgeOrig;
                                    bool disposeNEdge = false;
                                    if (Math.Abs(s - 1.0) > 1e-9)
                                    {
                                        nEdge = nEdgeOrig.Resize(new OpenCvSharp.Size(), s, s, InterpolationFlags.Linear);
                                        disposeNEdge = true;
                                    }

                                    using (var resE = hayEdge.MatchTemplate(nEdge, TemplateMatchModes.CCoeffNormed))
                                    {
                                        resE.MinMaxLoc(out _, out double maxValE, out _, out OpenCvSharp.Point maxLocE);
                                        if (maxValE > bestScore)
                                        {
                                            bestScore = maxValE;
                                            int cx = roiOffsetX + maxLocE.X + nEdge.Width / 2;
                                            int cy = roiOffsetY + maxLocE.Y + nEdge.Height / 2;
                                            bestScreen = WindowCoordToScreen(hwnd, cx, cy);
                                        }
                                    }
                                    if (disposeNEdge) nEdge.Dispose();
                                }
                            }
                        }
                        finally
                        {
                            nEdgeOrig?.Dispose();
                        }
                    }

                    if (bestScore >= threshold)
                    {
                        if (jitter != 0)
                        {
                            bestScreen.X += _rnd.Next(-jitter, jitter + 1);
                            bestScreen.Y += _rnd.Next(-jitter, jitter + 1);
                        }
                        ClickScreen(bestScreen);
                        await Task.Delay(80);
                    }

                    return (bestScore >= threshold, bestScore);
                }
                finally
                {
                    hayEdge?.Dispose();
                    if (!ReferenceEquals(hay, hayFull))
                        hay.Dispose(); // dispose ROI submat
                }
            }

            // Near-1.0 scale single-pass "click if found"
            public async Task<bool> FindAndClickAsync(
                IntPtr hwnd, IEnumerable<string> images, double threshold, int jitter = 0,
                System.Drawing.Rectangle? searchRectWin = null)
            {
                var (ok, _) = await FindAndClickMultiScaleAsync(
                    hwnd, images, threshold, jitter, searchRectWin,
                    0.95, 1.05, 0.05, useEdges: false); // tight scale
                return ok;
            }

            // Return rect of a single template in WINDOW coords (no click)
            public (bool ok, OpenCvSharp.Rect winRect, double score) FindTemplateRectWindowCoords(
                IntPtr hwnd, string image, double threshold,
                double minScale = 0.85, double maxScale = 1.15, double step = 0.05)
            {
                using var hay = CaptureWindowMat(hwnd);
                if (hay.Empty()) return (false, default, 0);

                using var hayGray = hay.CvtColor(ColorConversionCodes.BGR2GRAY);

                using var needleOrig = LoadTemplate(image);
                if (needleOrig is null || needleOrig.Empty()) return (false, default, 0);
                using var nGrayOrig = needleOrig.CvtColor(ColorConversionCodes.BGR2GRAY);

                double bestScore = 0;
                OpenCvSharp.Rect bestRect = default;

                for (double s = minScale; s <= maxScale + 1e-9; s += step)
                {
                    Mat nGray = nGrayOrig;
                    bool disposeNGray = false;
                    if (Math.Abs(s - 1.0) > 1e-9)
                    {
                        nGray = nGrayOrig.Resize(new OpenCvSharp.Size(), s, s, InterpolationFlags.Linear);
                        disposeNGray = true;
                    }

                    using (var res = hayGray.MatchTemplate(nGray, TemplateMatchModes.CCoeffNormed))
                    {
                        res.MinMaxLoc(out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                        if (maxVal > bestScore)
                        {
                            bestScore = maxVal;
                            bestRect = new OpenCvSharp.Rect(maxLoc.X, maxLoc.Y, nGray.Width, nGray.Height);
                        }
                    }

                    if (disposeNGray) nGray.Dispose();
                }

                return (bestScore >= threshold, bestRect, bestScore);
            }

            // Return best rect among multiple templates in WINDOW coords (no click)
            public (bool ok, OpenCvSharp.Rect winRect, string matchedFile, double score)
                FindAnyTemplateRectWindowCoords(
                    IntPtr hwnd,
                    IEnumerable<string> images,
                    double threshold,
                    double minScale = 0.85,
                    double maxScale = 1.15,
                    double step = 0.05)
            {
                using var hay = CaptureWindowMat(hwnd);
                if (hay.Empty()) return (false, default, null, 0);

                using var hayGray = hay.CvtColor(ColorConversionCodes.BGR2GRAY);

                double bestScore = 0;
                OpenCvSharp.Rect bestRect = default;
                string bestFile = null;

                foreach (var file in images)
                {
                    using var needleOrig = LoadTemplate(file);
                    if (needleOrig is null || needleOrig.Empty()) continue;
                    using var nGrayOrig = needleOrig.CvtColor(ColorConversionCodes.BGR2GRAY);

                    for (double s = minScale; s <= maxScale + 1e-9; s += step)
                    {
                        Mat nGray = nGrayOrig;
                        bool disposeNGray = false;
                        if (Math.Abs(s - 1.0) > 1e-9)
                        {
                            nGray = nGrayOrig.Resize(new OpenCvSharp.Size(), s, s, InterpolationFlags.Linear);
                            disposeNGray = true;
                        }

                        using (var res = hayGray.MatchTemplate(nGray, TemplateMatchModes.CCoeffNormed))
                        {
                            res.MinMaxLoc(out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                            if (maxVal > bestScore)
                            {
                                bestScore = maxVal;
                                bestRect = new OpenCvSharp.Rect(maxLoc.X, maxLoc.Y, nGray.Width, nGray.Height);
                                bestFile = file;
                            }
                        }

                        if (disposeNGray) nGray.Dispose();
                    }
                }

                return (bestScore >= threshold, bestRect, bestFile, bestScore);
            }

            private static Mat CannyOf(Mat gray) => gray.Canny(50, 150);

            private Mat LoadTemplate(string file)
            {
                string path = System.IO.Path.Combine(_baseDir, file);
                if (!System.IO.File.Exists(path)) return null;
                return Cv2.ImRead(path, ImreadModes.Unchanged);
            }

            private Mat GetTemplate(string file)
            {
                if (_templateCache.TryGetValue(file, out var m) && m is not null && !m.Empty())
                    return m;
                string path = System.IO.Path.Combine(_baseDir, file);
                if (!System.IO.File.Exists(path)) return null;
                var mat = Cv2.ImRead(path, ImreadModes.Grayscale); // read as gray directly
                _templateCache[file] = mat;
                return mat;
            }
        }

        // =================== Window capture + input ===================
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
                                                           IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        private const int SRCCOPY = 0x00CC0020;
        private const uint PW_RENDERFULLCONTENT = 0x00000002;

        private static Mat CaptureWindowMat(IntPtr hwnd)
        {
            GetWindowRect(hwnd, out RECT r);
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            if (w <= 0 || h <= 0) return new Mat();

            using var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                var hdcDest = g.GetHdc();
                IntPtr hdcSrc = GetWindowDC(hwnd);
                BitBlt(hdcDest, 0, 0, w, h, hdcSrc, 0, 0, SRCCOPY);
                ReleaseDC(hwnd, hdcSrc);
                g.ReleaseHdc(hdcDest);
            }

            // If capture is totally black, try PrintWindow
            if (IsBitmapEmpty(bmp))
            {
                using var g2 = Graphics.FromImage(bmp);
                var hdcDest2 = g2.GetHdc();
                PrintWindow(hwnd, hdcDest2, PW_RENDERFULLCONTENT);
                g2.ReleaseHdc(hdcDest2);
            }

            return BitmapConverter.ToMat(bmp);
        }

        // Safe (no unsafe code). Slow but only used on 1 bitmap per capture.
        private static bool IsBitmapEmpty(Bitmap bmp)
        {
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.R != 0 || c.G != 0 || c.B != 0)
                        return false;
                }
            }
            return true;
        }

        private static System.Drawing.Point WindowCoordToScreen(IntPtr hwnd, int x, int y)
        {
            GetWindowRect(hwnd, out RECT r);
            return new System.Drawing.Point(r.Left + x, r.Top + y);
        }

        private static void ClickWindowCenter(IntPtr hwnd, int jitter = 0)
        {
            GetWindowRect(hwnd, out RECT r);
            int cx = (r.Left + r.Right) / 2;
            int cy = (r.Top + r.Bottom) / 2;
            var p = new System.Drawing.Point(cx, cy);
            if (jitter != 0)
            {
                var rnd = new Random();
                p.X += rnd.Next(-jitter, jitter + 1);
                p.Y += rnd.Next(-jitter, jitter + 1);
            }
            ClickScreen(p);
        }

        private static System.Drawing.Point ClientToScreen(IntPtr hwnd, System.Drawing.Point client)
        {
            POINT c = new POINT { X = client.X, Y = client.Y };
            ClientToScreen(hwnd, ref c);
            return new System.Drawing.Point(c.X, c.Y);
        }

        private static void ClickScreen(System.Drawing.Point p)
        {
            SetCursorPos(p.X, p.Y);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); // LEFTDOWN
            Thread.Sleep(30);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); // LEFTUP
        }

        private static void SendKeysToWindow(string keys)
        {
            SendKeys.SendWait(keys); // requires target foreground (we set focus earlier)
            Thread.Sleep(40);
        }

        // =================== Logging (colors) ===================
        private void ClearLog()
        {
            if (ActivityLogRichTextBox.InvokeRequired)
            {
                ActivityLogRichTextBox.Invoke(new Action(ClearLog));
                return;
            }
            ActivityLogRichTextBox.Clear();
            ActivityLogRichTextBox.ForeColor = Color.Black; // default text color
        }

        private void Log(string s) => AppendLog(s, Color.Black);                      // normal = black
        private void LogWarn(string s) => AppendLog("WARN: " + s, Color.Orange);      // warning = orange
        private void LogError(string s) => AppendLog("ERROR: " + s, Color.Red);       // error = red

        private void AppendLog(string msg, Color c)
        {
            if (ActivityLogRichTextBox.InvokeRequired)
            {
                ActivityLogRichTextBox.Invoke(new Action(() => AppendLog(msg, c)));
                return;
            }
            ActivityLogRichTextBox.SelectionStart = ActivityLogRichTextBox.TextLength;
            ActivityLogRichTextBox.SelectionColor = c;
            ActivityLogRichTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
            ActivityLogRichTextBox.SelectionColor = ActivityLogRichTextBox.ForeColor;
        }

        private void FinishCurrentTarget()
        {
            if (progressBar1.Value < progressBar1.Maximum)
                progressBar1.Value++;
            Application.DoEvents();
        }

        // =================== Monsters list on Load ===================
        private void Form1_Load(object sender, EventArgs e)
        {
            string[] monsters =
            {
                "AMMIT",
                "AZAZEL",
                "CERBERUS LEGENDARY",
                "CERBERUS SENIOR",
                "KRAKEN",
                "PAN (CAVALRY) Level 4",
                "PAN (CAVALRY) Level 5",
                "PAN (CAVALRY) Level 6",
                "PAN (RANGE) Level 6",
                "SPHINX Level 5",
                "SPHINX Level 6",
                "SPHINX Level 7",
                "STYMPHALIAN BIRD",
                "TURTLE Level 5",
                "TURTLE Level 6",
                "YMIR Level 3",
                "YMIR Level 4",
                "YMIR Level 5",
                "YMIR Level 6"
            };
            if (checkedListBox1.Items.Count == 0)
                checkedListBox1.Items.AddRange(monsters);

            comboBox2.Items.Clear();
            comboBox2.Items.AddRange(new object[]
            {
                "Warlord", "Pan", "Kraken", "Azazel", "Sphinx",
                "Cerberus", "Stymphalian Bird", "Ymir", "Ammit"
            });
            comboBox2.SelectedIndex = 0;
        }

        // =================== ProcessFinder helper ===================
        internal sealed class ProcessFinder : IDisposable
        {
            public Process TargetProcess { get; private set; }
            public IntPtr MainWindowHandle { get; private set; } = IntPtr.Zero;
            public Rectangle WindowRect { get; private set; } = Rectangle.Empty;
            public bool IsConnected => TargetProcess != null && !TargetProcess.HasExited && MainWindowHandle != IntPtr.Zero;

            public void Dispose() => Detach();

            public void Detach()
            {
                if (TargetProcess != null)
                {
                    try { TargetProcess.EnableRaisingEvents = false; } catch { }
                    try { TargetProcess.Dispose(); } catch { }
                }
                TargetProcess = null;
                MainWindowHandle = IntPtr.Zero;
                WindowRect = Rectangle.Empty;
            }

            public bool TryConnect(string exeOrProcessName, out string errorMessage, int waitForWindowMs = 2000)
            {
                errorMessage = null;
                Detach();

                if (string.IsNullOrWhiteSpace(exeOrProcessName))
                {
                    errorMessage = "No process name provided.";
                    return false;
                }

                var name = exeOrProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? exeOrProcessName[..^4]
                    : exeOrProcessName;

                var candidates = Process.GetProcessesByName(name);
                if (candidates == null || candidates.Length == 0)
                {
                    errorMessage = $"Process \"{name}\" not found.";
                    return false;
                }

                TargetProcess = candidates.FirstOrDefault(p => GetSafeMainWindowHandle(p) != IntPtr.Zero) ?? candidates.First();

                try { TargetProcess.EnableRaisingEvents = true; } catch { }

                int deadline = Environment.TickCount + waitForWindowMs;
                while (Environment.TickCount < deadline)
                {
                    MainWindowHandle = GetTopLevelWindowForProcess(TargetProcess);
                    if (MainWindowHandle != IntPtr.Zero && IsWindow(MainWindowHandle))
                        break;
                    System.Threading.Thread.Sleep(50);
                }

                if (MainWindowHandle == IntPtr.Zero)
                {
                    errorMessage = "Found the process, but no top-level window was detected.";
                    Detach();
                    return false;
                }

                UpdateWindowRect();
                return true;
            }

            public bool FocusWindow()
            {
                if (!IsConnected) return false;

                if (IsIconic(MainWindowHandle))
                    ShowWindowAsync(MainWindowHandle, SW_RESTORE);
                else
                    ShowWindowAsync(MainWindowHandle, SW_SHOW);

                return SetForegroundWindow(MainWindowHandle);
            }

            public bool UpdateWindowRect()
            {
                if (!IsConnected) return false;

                if (DwmGetWindowAttribute(MainWindowHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT dwmRect, Marshal.SizeOf<RECT>()) == 0)
                {
                    WindowRect = Rectangle.FromLTRB(dwmRect.Left, dwmRect.Top, dwmRect.Right, dwmRect.Bottom);
                    return true;
                }

                if (GetWindowRect(MainWindowHandle, out RECT r))
                {
                    WindowRect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                    return true;
                }
                return false;
            }

            public System.Drawing.Point ClientToScreen(System.Drawing.Point clientPoint)
            {
                if (!IsConnected) return System.Drawing.Point.Empty;
                POINT p = new POINT { X = clientPoint.X, Y = clientPoint.Y };
                if (ClientToScreen(MainWindowHandle, ref p))
                    return new System.Drawing.Point(p.X, p.Y);
                return System.Drawing.Point.Empty;
            }

            public bool ScreenPointInWindow(System.Drawing.Point screenPoint) =>
                IsConnected && WindowRect.Contains(screenPoint);

            private static IntPtr GetSafeMainWindowHandle(Process p)
            {
                try { return p.MainWindowHandle; } catch { return IntPtr.Zero; }
            }

            private static IntPtr GetTopLevelWindowForProcess(Process p)
            {
                var h = GetSafeMainWindowHandle(p);
                if (h != IntPtr.Zero && IsWindow(h)) return h;

                IntPtr result = IntPtr.Zero;
                uint targetPid = (uint)p.Id;

                bool Callback(IntPtr hwnd, IntPtr lParam)
                {
                    if (!IsWindowVisible(hwnd)) return true;
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid == targetPid && GetWindow(hwnd, 4) == IntPtr.Zero)
                    {
                        result = hwnd;
                        return false;
                    }
                    return true;
                }

                EnumWindows(Callback, IntPtr.Zero);
                return result;
            }

            // Win32 interop for ProcessFinder
            private const int SW_RESTORE = 9;
            private const int SW_SHOW = 5;
            private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

            [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
            [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
            [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
            [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
            [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
            [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
            [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
            [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
            private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
            [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);
            [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("dwmapi.dll")]
            private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

            [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
            [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        }

        // Shared structs for outer class P/Invokes
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

        private static readonly Regex MonsterLineRegex =
        new Regex(@"^\s*Lv\s*(\d+)\s+([^(]+?)\s*(?:\(|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private void Sortbtn_Click(object sender, EventArgs e)
        {
            var cmp = StringComparison.OrdinalIgnoreCase;
            string priority = comboBox2.SelectedItem?.ToString()?.Trim();

            // Parse all lines
            var lines = SortMonstersrichtextbox.Lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var parsed = new List<MonsterEntry>(lines.Count);
            var leftovers = new List<string>();

            foreach (var line in lines)
            {
                var entry = TryParseMonster(line);
                if (entry != null) parsed.Add(entry);
                else leftovers.Add(line); // keep lines we couldn't parse
            }

            // Split by priority
            var priorityGroup = parsed
                .Where(e => IsPriority(e.Name, priority, cmp))
                .OrderByDescending(e => e.Level)
                .ToList();

            // Others grouped by name (A→Z), each group by level desc
            var otherOrdered = parsed
                .Where(e => !IsPriority(e.Name, priority, cmp))
                .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .SelectMany(g => g.OrderByDescending(e => e.Level))
                .ToList();

            // Combine: priority first, then the rest
            var finalList = new List<string>(parsed.Count + leftovers.Count);
            finalList.AddRange(priorityGroup.Select(e => e.OriginalLine));
            finalList.AddRange(otherOrdered.Select(e => e.OriginalLine));

            // If you want to keep unparsed lines, you can append them at the end (optional)
            // finalList.AddRange(leftovers);

            CoordsRichTextBox.Text = string.Join(Environment.NewLine, finalList);
        }

        private void clearbtn_Click(object sender, EventArgs e)
        {
            SortMonstersrichtextbox.Clear();
        }

        private sealed class MonsterEntry
        {
            public int Level { get; set; }
            public string Name { get; set; } = "";
            public string OriginalLine { get; set; } = "";
        }

        private MonsterEntry TryParseMonster(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            var m = MonsterLineRegex.Match(line);
            if (!m.Success) return null;

            if (!int.TryParse(m.Groups[1].Value, out int level)) return null;

            // Normalize the name a bit: trim & collapse internal spaces
            string rawName = m.Groups[2].Value.Trim();
            string name = Regex.Replace(rawName, @"\s+", " ");

            return new MonsterEntry
            {
                Level = level,
                Name = name,
                OriginalLine = line
            };
        }

        private bool IsPriority(string monsterName, string priority, StringComparison cmp)
        {
            if (string.IsNullOrWhiteSpace(priority)) return false;

            // Match if equal or starts with (so "Pan" also catches "Pan Cavalry", etc.)
            return monsterName.Equals(priority, cmp) || monsterName.StartsWith(priority + " ", cmp);
        }

        // ====== Verification template pool (keep tight for speed) ======
        private IEnumerable<string> VerifyImagePool()
        {
            // TIP: you can filter this by checkedListBox1 selections
            return new[]{
                "Azazel.png","StymphalianBirdPower.png","KrakenPower.png","AmmitPower.png",
                "Turtle6.png","Turtle5.png","Cerb4.png","Cerb3.png",
                "Sphinx7.png","Sphinx6.png","Sphinx5.png",
                "ymir3.png","ymir4.png","ymir5.png","ymir6.png",
                "PanRange6.png","PanCav4.png","PanCav5.png","PanCav6.png",
                "Warlord3.png","Warlord4.png","Warlord5.png","Warlord6.png"
            };
        }
    }
}
