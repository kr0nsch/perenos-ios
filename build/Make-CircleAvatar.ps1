# Generates a Telegram-ready circular avatar from the rounded-square app logo:
# the brand gradient fills the whole square and only the white glyph is kept,
# so the inscribed-circle crop used by Telegram cuts nothing important.
param(
    [string]$Source = "$PSScriptRoot/../assets/logo-1024.png",
    [string]$Output = "$PSScriptRoot/../assets/avatar-circle-1024.png",
    [string]$CirclePreview = ""
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourcePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Source)
$outputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)
$src = [System.Drawing.Bitmap]::new($sourcePath)
$w = $src.Width; $h = $src.Height
$bg = $src.GetPixel(2, 2)

function ColorDiff($a, $b) {
    [Math]::Abs($a.R - $b.R) + [Math]::Abs($a.G - $b.G) + [Math]::Abs($a.B - $b.B)
}

$cy = [int]($h / 2); $cx = [int]($w / 2)
$left = 0;     while ($left -lt $w -and (ColorDiff $src.GetPixel($left, $cy) $bg) -le 24) { $left++ }
$right = $w - 1; while ($right -ge 0 -and (ColorDiff $src.GetPixel($right, $cy) $bg) -le 24) { $right-- }
$top = 0;      while ($top -lt $h -and (ColorDiff $src.GetPixel($cx, $top) $bg) -le 24) { $top++ }
$bottom = $h - 1; while ($bottom -ge 0 -and (ColorDiff $src.GetPixel($cx, $bottom) $bg) -le 24) { $bottom-- }
$side = [Math]::Min($right - $left + 1, $bottom - $top + 1)
$radius = [int]($side * 0.22)
Write-Host "icon bounds: left=$left top=$top right=$right bottom=$bottom radius=$radius"

$c1 = $src.GetPixel($left + [int]($side * 0.12), $top + [int]($side * 0.12))
$c2 = $src.GetPixel($right - [int]($side * 0.12), $bottom - [int]($side * 0.12))
$out = [System.Drawing.Bitmap]::new($w, $h)
$g = [System.Drawing.Graphics]::FromImage($out)
$brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.Point]::new(0, 0), [System.Drawing.Point]::new($w, $h), $c1, $c2)
$g.FillRectangle($brush, 0, 0, $w, $h)
$g.Dispose(); $brush.Dispose()

$innerL = $left + $radius; $innerR = $right - $radius
$innerT = $top + $radius;  $innerB = $bottom - $radius
$copied = 0
for ($y = $top; $y -le $bottom; $y++) {
    for ($x = $left; $x -le $right; $x++) {
        $px = $src.GetPixel($x, $y)
        if ([Math]::Min($px.R, [Math]::Min($px.G, $px.B)) -lt 235) { continue }
        $qx = [Math]::Max($innerL, [Math]::Min($innerR, $x))
        $qy = [Math]::Max($innerT, [Math]::Min($innerB, $y))
        $dx = $x - $qx; $dy = $y - $qy
        if (($dx * $dx + $dy * $dy) -gt ($radius * $radius)) { continue }
        $out.SetPixel($x, $y, $px)
        $copied++
    }
}
Write-Host "glyph pixels copied: $copied"
$out.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)

if ($CirclePreview) {
    $previewPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($CirclePreview)
    $prev = [System.Drawing.Bitmap]::new($w, $h)
    $gp = [System.Drawing.Graphics]::FromImage($prev)
    $gp.Clear([System.Drawing.Color]::White)
    $clip = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $clip.AddEllipse(0, 0, $w, $h)
    $gp.SetClip($clip)
    $gp.DrawImage($out, 0, 0, $w, $h)
    $gp.Dispose(); $clip.Dispose()
    $prev.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $prev.Dispose()
    Write-Host "circle preview: $previewPath"
}

$src.Dispose(); $out.Dispose()
Write-Host "saved: $outputPath"