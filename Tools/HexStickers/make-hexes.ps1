<#
.SYNOPSIS
  Builds hex map stickers (1.05" x 1.21", pointy-top, 300 dpi) from square location art
  and lays them out on US Letter sticker-paper sheets.

.DESCRIPTION
  For every PNG/JPG in -InDir (the file name, minus extension, becomes the label):
    * center-crops the art to the hex, adds a brown border and a name banner
    * extends the art past the border by -BleedPx so a slightly off cut never shows white
  Outputs to -OutDir:
    Singles\<name>.png        transparent single stickers (includes bleed)
    HexSheet-N.png / .pdf     Letter sheets, staggered honeycomb, -Copies of each hex

  PRINT AT 100% / "Actual size" (NOT "fit to page"), or the hexes will not be 1.05" wide.
  Cut along the brown border line.

.EXAMPLE
  .\make-hexes.ps1
  .\make-hexes.ps1 -Copies 6 -TextY 0.70
#>
param(
    [string]$InDir  = (Join-Path $PSScriptRoot '..\..\images\Locations\Hexes'),
    [string]$OutDir = '',
    [int]$Copies    = 1,
    [int]$BleedPx   = 9,        # 0.03" of art beyond the cut line
    [int]$GapPx     = 12,       # space between neighbouring bleed edges
    [int]$MarginPx  = 75,       # 0.25" page margin
    [double]$TextY = 0.70,        # text centre, as fraction of hex height (top ~0.34, bottom ~0.70)
    [double]$StrokeK = 0.28,    # text stroke width as a fraction of font size
    [int]$MaxFontPx = 38,
    [int]$MinFontPx = 24,
    [string]$FontName = 'Georgia',
    [switch]$NoText
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$InDir = (Resolve-Path $InDir).ProviderPath
if (-not $OutDir) { $OutDir = Join-Path $InDir 'Print' }
$SinglesDir = Join-Path $OutDir 'Singles'
New-Item -ItemType Directory -Force $SinglesDir | Out-Null

# ---- geometry (pixels at 300 dpi) ----
$W = 315; $H = 363                 # nominal hex = the cut line
$B = $BleedPx
$TW = $W + 2 * $B                  # tile width incl. bleed
$s  = $TW / $W                     # uniform scale for the bleed hex
$TH = [int][math]::Ceiling($H * $s)
$borderPx = 6
$brown  = [System.Drawing.Color]::FromArgb(255, 134, 94, 59)   # matches the supplied hex border
$ink    = [System.Drawing.Color]::FromArgb(255, 74, 46, 26)
$parch  = [System.Drawing.Color]::FromArgb(255, 245, 234, 208)

function Get-HexPoints([double]$cx, [double]$cy, [double]$w, [double]$h) {
    $l = $cx - $w / 2; $r = $cx + $w / 2; $t = $cy - $h / 2; $b = $cy + $h / 2
    return [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF(($cx), $t)),
        (New-Object System.Drawing.PointF($r, ($t + 0.25 * $h))),
        (New-Object System.Drawing.PointF($r, ($t + 0.75 * $h))),
        (New-Object System.Drawing.PointF($cx, $b)),
        (New-Object System.Drawing.PointF($l, ($t + 0.75 * $h))),
        (New-Object System.Drawing.PointF($l, ($t + 0.25 * $h)))
    )
}

function New-HexTile([string]$path, [string]$label) {
    $src = [System.Drawing.Image]::FromFile($path)
    $bmp = New-Object System.Drawing.Bitmap($TW, $TH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmp.SetResolution(300, 300)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $cx = $TW / 2.0; $cy = $TH / 2.0

    # art, clipped to the bleed hex, "cover" fit
    $outer = New-Object System.Drawing.Drawing2D.GraphicsPath
    $outer.AddPolygon((Get-HexPoints $cx $cy $TW $TH))
    $g.SetClip($outer)
    $k = [math]::Max($TW / $src.Width, $TH / $src.Height)
    $dw = $src.Width * $k; $dh = $src.Height * $k
    $g.DrawImage($src, ($cx - $dw / 2), ($cy - $dh / 2), $dw, $dh)

    $g.ResetClip()

    # name: cream text with a dark stroke so it reads over any art (no banner)
    if (-not $NoText -and $label) {
        $yc = $cy - $H / 2.0 + $H * $TextY
        $fTop = $TextY - 0.05; $fBot = $TextY + 0.05
        $lim = [math]::Min([math]::Min(1.0, $fTop / 0.25), [math]::Min(1.0, (1 - $fBot) / 0.25))
        $availW = $W * $lim - 28
        $family = New-Object System.Drawing.FontFamily($FontName)
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
        $fill = New-Object System.Drawing.SolidBrush($parch)
        $text = $label
        $tries = @($label)
        if ($label.Contains(' ')) {
            $mid = $label.Length / 2; $best = -1
            for ($i = 0; $i -lt $label.Length; $i++) { if ($label[$i] -eq ' ' -and ($best -lt 0 -or [math]::Abs($i - $mid) -lt [math]::Abs($best - $mid))) { $best = $i } }
            $tries += ($label.Substring(0, $best) + "`n" + $label.Substring($best + 1))
        }
        foreach ($text in $tries) {
            for ($size = $MaxFontPx; $size -ge $MinFontPx; $size--) {
                $p = New-Object System.Drawing.Drawing2D.GraphicsPath
                $p.AddString($text, $family, [int][System.Drawing.FontStyle]::Bold, $size, (New-Object System.Drawing.PointF($cx, $yc)), $sf)
                $bw = $p.GetBounds().Width + $size * 0.2
                if ($bw -le $availW) { break }
                $p.Dispose()
            }
            if ($bw -le $availW) { break }
        }
        $pen = New-Object System.Drawing.Pen($ink, [single]($size * $StrokeK))
        $pen.LineJoin = 'Round'
        $g.DrawPath($pen, $p)
        $g.FillPath($fill, $p)
    }

    # border on the nominal hex (the cut line), inset by half the stroke so it stays inside
    $bp = New-Object System.Drawing.Pen($brown, $borderPx)
    $bp.LineJoin = 'Round'
    $g.DrawPolygon($bp, (Get-HexPoints $cx $cy ($W - $borderPx) ($H - $borderPx)))

    $g.Dispose(); $src.Dispose()
    return $bmp
}

# ---- build tiles ----
$files = Get-ChildItem $InDir -File | Where-Object { $_.Extension -match '^\.(png|jpg|jpeg)$' } | Sort-Object Name
if (-not $files) { throw "No images found in $InDir" }
$tiles = @()
foreach ($f in $files) {
    $label = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
    $t = New-HexTile $f.FullName $label
    $t.Save((Join-Path $SinglesDir ($label + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    for ($i = 0; $i -lt $Copies; $i++) { $tiles += ,$t }
    Write-Host "built $label"
}

# ---- sheet layout: staggered honeycomb on Letter @ 300 dpi ----
$PW = 2550; $PH = 3300
$px = $TW + $GapPx                       # column pitch
$py = [int](0.75 * $TH + $GapPx * 0.866) # row pitch
$cols = [int][math]::Floor((($PW - 2 * $MarginPx) - $px / 2.0 + $GapPx) / $px)   # fits the offset rows too
$rows = [int][math]::Floor((($PH - 2 * $MarginPx) - $TH) / $py) + 1
$perSheet = $cols * $rows
$gridW = $cols * $px - $GapPx + $px / 2.0
$gridH = ($rows - 1) * $py + $TH
$ox = ($PW - $gridW) / 2.0

$sheets = [int][math]::Ceiling($tiles.Count / $perSheet)
Write-Host "$($tiles.Count) stickers, $cols x $rows per sheet, $sheets sheet(s)"

function Save-Pdf([System.Drawing.Bitmap]$bmp, [string]$pdfPath) {
    $tmp = [System.IO.Path]::GetTempFileName()
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $ep = New-Object System.Drawing.Imaging.EncoderParameters(1)
    $ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality, [long]95)
    $bmp.Save($tmp, $codec, $ep)
    $jpg = [System.IO.File]::ReadAllBytes($tmp); Remove-Item $tmp
    $ms = New-Object System.IO.MemoryStream
    $enc = [System.Text.Encoding]::ASCII
    $offs = @()
    function W([string]$x) { $b = $enc.GetBytes($x); $ms.Write($b, 0, $b.Length) }
    W "%PDF-1.4`n"
    $offs += $ms.Position; W "1 0 obj`n<< /Type /Catalog /Pages 2 0 R >>`nendobj`n"
    $offs += $ms.Position; W "2 0 obj`n<< /Type /Pages /Kids [3 0 R] /Count 1 >>`nendobj`n"
    $offs += $ms.Position; W "3 0 obj`n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>`nendobj`n"
    $offs += $ms.Position; W "4 0 obj`n<< /Type /XObject /Subtype /Image /Width $($bmp.Width) /Height $($bmp.Height) /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length $($jpg.Length) >>`nstream`n"
    $ms.Write($jpg, 0, $jpg.Length); W "`nendstream`nendobj`n"
    $content = "q 612 0 0 792 0 0 cm /Im0 Do Q"
    $offs += $ms.Position; W "5 0 obj`n<< /Length $($content.Length) >>`nstream`n$content`nendstream`nendobj`n"
    $xref = $ms.Position
    W "xref`n0 6`n0000000000 65535 f `n"
    foreach ($o in $offs) { W (("{0:D10} 00000 n " -f [int]$o) + "`n") }
    W "trailer`n<< /Size 6 /Root 1 0 R >>`nstartxref`n$xref`n%%EOF"
    [System.IO.File]::WriteAllBytes($pdfPath, $ms.ToArray())
}

for ($sh = 0; $sh -lt $sheets; $sh++) {
    $page = New-Object System.Drawing.Bitmap($PW, $PH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $page.SetResolution(300, 300)
    $g = [System.Drawing.Graphics]::FromImage($page)
    $g.Clear([System.Drawing.Color]::White)
    $g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'
    for ($n = 0; $n -lt $perSheet; $n++) {
        $idx = $sh * $perSheet + $n
        if ($idx -ge $tiles.Count) { break }
        $r = [math]::Floor($n / $cols); $c = $n % $cols
        $x = $ox + $c * $px + ($(if ($r % 2 -eq 1) { $px / 2.0 } else { 0 }))
        $y = $MarginPx + ($PH - 2 * $MarginPx - $gridH) / 2.0 + $r * $py
        $g.DrawImage($tiles[$idx], [int][math]::Round($x), [int][math]::Round($y), $TW, $TH)
    }
    $g.Dispose()
    $png = Join-Path $OutDir ("HexSheet-{0}.png" -f ($sh + 1))
    $page.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    Save-Pdf $page (Join-Path $OutDir ("HexSheet-{0}.pdf" -f ($sh + 1)))
    $page.Dispose()
    Write-Host "wrote $png (+ .pdf)"
}
Write-Host "Done. Print the PDF at 100% / Actual size on Letter."
