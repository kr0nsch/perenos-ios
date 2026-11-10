<#
.SYNOPSIS
  Records a running Perenos demo window directly to lossless, cursor-free PNGs.
.DESCRIPTION
  Start an isolated portable demo yourself, then pass its exact PID. This script
  does not start, stop, activate, resize or operate the application. Create a file
  named "stop" in OutDir to finish. MaxSeconds provides a second stopping limit.

  OutDir must be a new or empty directory strictly inside this repository's
  artifacts directory. Existing files, reparse points and non-demo processes are
  rejected. No settings, account data or command lines are saved.
.EXAMPLE
  ./build/Record-DemoWindow.ps1 -ProcessId 1234 -OutDir artifacts/recordings-1.2.0/ru-light/demo
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateRange(1, 2147483647)] [int] $ProcessId,
    [Parameter(Mandatory)] [string] $OutDir,
    [ValidateRange(40, 1000)] [int] $IntervalMs = 90,
    [ValidateRange(1, 3600)] [int] $MaxSeconds = 600
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [IO.Path]::Combine($repoRoot, 'artifacts')
$outputPath = if ([IO.Path]::IsPathRooted($OutDir)) {
    [IO.Path]::GetFullPath($OutDir)
} else {
    [IO.Path]::GetFullPath([IO.Path]::Combine($repoRoot, $OutDir))
}
$artifactsPrefix = $artifactsRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (-not $outputPath.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutDir must be strictly inside this repository''s artifacts directory.'
}

function Assert-NoReparsePoint([string] $Path) {
    # Inspect all existing ancestors, including artifacts itself, before writing.
    $candidate = $Path
    while ($candidate) {
        if (Test-Path -LiteralPath $candidate) {
            $item = Get-Item -LiteralPath $candidate -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse points are not allowed in the capture path: $candidate"
            }
        }
        $parent = [IO.Path]::GetDirectoryName($candidate)
        if ($parent -eq $candidate) { break }
        $candidate = $parent
    }
}

Assert-NoReparsePoint $outputPath
if ([IO.File]::Exists($outputPath)) { throw 'OutDir is an existing file.' }
if ([IO.Directory]::Exists($outputPath) -and [IO.Directory]::GetFileSystemEntries($outputPath).Length -gt 0) {
    throw 'OutDir must be empty. Existing recordings are never overwritten.'
}

$processInfo = Get-CimInstance Win32_Process -Filter "ProcessId=$ProcessId"
if (-not $processInfo -or -not $processInfo.ExecutablePath) { throw 'The target process is unavailable.' }
$exePath = [string] $processInfo.ExecutablePath
if ([IO.Path]::GetFileName($exePath) -notin @('Perenos.exe')) {
    throw 'Only a running Perenos demonstration process may be recorded.'
}
if ([string] $processInfo.CommandLine -notmatch '(?:^|\s|\")--demo(?:-saved)?(?:\s|\"|$)') {
    throw 'The target must have been launched with --demo or --demo-saved.'
}
if (-not [IO.File]::Exists([IO.Path]::Combine([IO.Path]::GetDirectoryName($exePath), 'portable.txt'))) {
    throw 'Use an isolated portable demo copy with portable.txt next to its executable.'
}
$targetProcess = Get-Process -Id $ProcessId
$expectedStartTicks = $targetProcess.StartTime.ToUniversalTime().Ticks
$exeHash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash.ToLowerInvariant()

$recorderSource = @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Perenos.Media {
    public sealed class CaptureResult {
        public int Frames;
        public int Width;
        public int Height;
        public long DurationMs;
        public string StopReason;
    }

    public static class DemoWindowRecorder {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

        private static Rectangle Bounds(IntPtr hwnd, out RECT window, out RECT frame) {
            if (!GetWindowRect(hwnd, out window)) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read target window bounds.");
            }
            int hr = DwmGetWindowAttribute(hwnd, 9, out frame, Marshal.SizeOf(typeof(RECT)));
            if (hr != 0) { Marshal.ThrowExceptionForHR(hr); }
            int width = window.Right - window.Left;
            int height = window.Bottom - window.Top;
            var crop = new Rectangle(frame.Left - window.Left, frame.Top - window.Top,
                frame.Right - frame.Left, frame.Bottom - frame.Top);
            if (width <= 0 || height <= 0 || width > 16384 || height > 16384 ||
                crop.Width < 400 || crop.Height < 300 || crop.X < 0 || crop.Y < 0 ||
                crop.Right > width || crop.Bottom > height) {
                throw new InvalidOperationException("Invalid target window bounds or DPI scaling.");
            }
            return crop;
        }

        public static CaptureResult Run(int pid, long expectedStartTicks, string dir, int intervalMs, int maxSeconds) {
            // Physical-pixel coordinates keep GetWindowRect and DWM frame bounds aligned.
            IntPtr previousDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
            if (previousDpi == IntPtr.Zero) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot set capture thread DPI awareness.");
            }
            try {
                using (var process = Process.GetProcessById(pid)) {
                    if (process.StartTime.ToUniversalTime().Ticks != expectedStartTicks) {
                        throw new InvalidOperationException("The target process was replaced before recording started.");
                    }
                    var startup = Stopwatch.StartNew();
                    IntPtr hwnd = process.MainWindowHandle;
                    while (hwnd == IntPtr.Zero && startup.ElapsedMilliseconds < 10000) {
                        if (process.HasExited) { throw new InvalidOperationException("The demo exited before opening its window."); }
                        Thread.Sleep(100);
                        process.Refresh();
                        hwnd = process.MainWindowHandle;
                    }
                    if (hwnd == IntPtr.Zero) { throw new InvalidOperationException("The demo has no main window."); }
                    RECT initialWindow, initialFrame;
                    Rectangle initialCrop = Bounds(hwnd, out initialWindow, out initialFrame);
                    var utf8 = new UTF8Encoding(false);
                    using (var windowFile = new StreamWriter(new FileStream(Path.Combine(dir, "window.txt"), FileMode.CreateNew), utf8)) {
                        windowFile.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}",
                            initialFrame.Left, initialFrame.Top, initialCrop.Width, initialCrop.Height));
                    }
                    var result = new CaptureResult { Width = initialCrop.Width, Height = initialCrop.Height };
                    string stop = Path.Combine(dir, "stop");
                    var elapsed = Stopwatch.StartNew();
                    using (var log = new StreamWriter(new FileStream(Path.Combine(dir, "frames.csv"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), utf8)) {
                        log.AutoFlush = true;
                        while (!File.Exists(stop) && elapsed.Elapsed.TotalSeconds < maxSeconds) {
                            uint owner;
                            GetWindowThreadProcessId(hwnd, out owner);
                            if (process.HasExited || !IsWindow(hwnd) || owner != (uint)pid) {
                                throw new InvalidOperationException("The target window closed during recording.");
                            }
                            if (IsIconic(hwnd)) { throw new InvalidOperationException("Do not minimize the demo during recording."); }
                            RECT currentWindow, currentFrame;
                            Rectangle crop = Bounds(hwnd, out currentWindow, out currentFrame);
                            if (crop.Width != initialCrop.Width || crop.Height != initialCrop.Height ||
                                currentWindow.Right - currentWindow.Left != initialWindow.Right - initialWindow.Left ||
                                currentWindow.Bottom - currentWindow.Top != initialWindow.Bottom - initialWindow.Top) {
                                throw new InvalidOperationException("The window changed size or DPI during recording.");
                            }
                            long frameStarted = elapsed.ElapsedMilliseconds;
                            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            using (var bitmap = new Bitmap(currentWindow.Right - currentWindow.Left,
                                currentWindow.Bottom - currentWindow.Top, PixelFormat.Format32bppArgb)) {
                                using (var graphics = Graphics.FromImage(bitmap)) {
                                    IntPtr hdc = graphics.GetHdc();
                                    try {
                                        // PW_RENDERFULLCONTENT captures the window, not the desktop cursor.
                                        if (!PrintWindow(hwnd, hdc, 2)) {
                                            throw new Win32Exception(Marshal.GetLastWin32Error(), "PrintWindow failed.");
                                        }
                                    } finally { graphics.ReleaseHdc(hdc); }
                                }
                                using (var cleanFrame = bitmap.Clone(crop, PixelFormat.Format24bppRgb)) {
                                    string name = string.Format(CultureInfo.InvariantCulture, "f{0:00000}.png", result.Frames);
                                    using (var output = new FileStream(Path.Combine(dir, name), FileMode.CreateNew)) {
                                        cleanFrame.Save(output, ImageFormat.Png);
                                    }
                                }
                            }
                            log.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1}", result.Frames, timestamp));
                            result.Frames++;
                            long remaining = intervalMs - (elapsed.ElapsedMilliseconds - frameStarted);
                            if (remaining > 0) { Thread.Sleep((int)remaining); }
                        }
                    }
                    result.DurationMs = elapsed.ElapsedMilliseconds;
                    result.StopReason = File.Exists(stop) ? "stop-file" : "time-limit";
                    if (result.Frames == 0) { throw new InvalidOperationException("No frames were recorded."); }
                    return result;
                }
            } finally { SetThreadDpiAwarenessContext(previousDpi); }
        }
    }
}
'@

if (-not ('Perenos.Media.DemoWindowRecorder' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition $recorderSource
}
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
Assert-NoReparsePoint $outputPath
if ([IO.Directory]::GetFileSystemEntries($outputPath).Length -ne 0) {
    throw 'OutDir became nonempty before capture started; nothing was overwritten.'
}

$utf8 = New-Object Text.UTF8Encoding($false)
$metadataPath = [IO.Path]::Combine($outputPath, 'capture.json')
$metadata = [ordered]@{
    schema_version = 1
    method = 'PrintWindow(PW_RENDERFULLCONTENT)'
    cursor_captured = $false
    format = 'PNG'
    process_id = $ProcessId
    executable = $exePath
    executable_sha256 = $exeHash
    interval_ms = $IntervalMs
    max_seconds = $MaxSeconds
    started_utc = [DateTime]::UtcNow.ToString('o')
    status = 'recording'
}
[IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json), $utf8)
try {
    $result = [Perenos.Media.DemoWindowRecorder]::Run($ProcessId, $expectedStartTicks, $outputPath, $IntervalMs, $MaxSeconds)
    $metadata.status = 'complete'
    $metadata['frames'] = $result.Frames
    $metadata['width'] = $result.Width
    $metadata['height'] = $result.Height
    $metadata['duration_ms'] = $result.DurationMs
    $metadata['stop_reason'] = $result.StopReason
    Write-Output "Recorded $($result.Frames) cursor-free PNG frames at $($result.Width)x$($result.Height) to $outputPath"
} catch {
    $metadata.status = 'failed'
    throw
} finally {
    $metadata['finished_utc'] = [DateTime]::UtcNow.ToString('o')
    [IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json), $utf8)
}
