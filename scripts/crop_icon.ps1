param(
    [string]$SourceImage = "$PSScriptRoot\..\assets\keydeck-icon-minimal.png"
)

Add-Type -AssemblyName System.Drawing

$imagePath = if (Test-Path $SourceImage) {
    [System.IO.Path]::GetFullPath($SourceImage)
} else {
    [System.IO.Path]::GetFullPath("$PSScriptRoot\..\assets\keydeck-icon.png")
}

if (-not (Test-Path $imagePath)) {
    Write-Error "Source image not found at $imagePath"
    exit 1
}

Write-Host "Loading image: $imagePath"
$bmp = [System.Drawing.Bitmap]::FromFile($imagePath)
Write-Host "Original dimensions: $($bmp.Width) x $($bmp.Height)"

$minX = $bmp.Width
$minY = $bmp.Height
$maxX = 0
$maxY = 0

# Sample pixels to find non-white keypad bounds
for ($y = 0; $y -lt $bmp.Height; $y++) {
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        # Background is white/near-white: R>235, G>235, B>235
        if ($c.R -lt 235 -or $c.G -lt 235 -or $c.B -lt 235) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

Write-Host "Detected Keypad Bounds: minX=$minX, minY=$minY, maxX=$maxX, maxY=$maxY"
$width = $maxX - $minX + 1
$height = $maxY - $minY + 1
Write-Host "Keypad size: $width x $height"

# Minimal padding so the keypad fills the icon completely without clipping rounded corners
$pad = 6
$boxMinX = [Math]::Max(0, $minX - $pad)
$boxMinY = [Math]::Max(0, $minY - $pad)
$boxMaxX = [Math]::Min($bmp.Width - 1, $maxX + $pad)
$boxMaxY = [Math]::Min($bmp.Height - 1, $maxY + $pad)

$boxWidth = $boxMaxX - $boxMinX + 1
$boxHeight = $boxMaxY - $boxMinY + 1
$size = [Math]::Max($boxWidth, $boxHeight)

# Center the square crop
$centerX = ($boxMinX + $boxMaxX) / 2
$centerY = ($boxMinY + $boxMaxY) / 2

$cropX = [Math]::Max(0, [int]($centerX - ($size / 2)))
$cropY = [Math]::Max(0, [int]($centerY - ($size / 2)))

if ($cropX + $size -gt $bmp.Width) { $cropX = $bmp.Width - $size }
if ($cropY + $size -gt $bmp.Height) { $cropY = $bmp.Height - $size }

Write-Host "Cropping tightly to square region: x=$cropX, y=$cropY, size=$size"

$cropRect = New-Object System.Drawing.Rectangle($cropX, $cropY, $size, $size)
$croppedBmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($croppedBmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.DrawImage($bmp, (New-Object System.Drawing.Rectangle(0, 0, $size, $size)), $cropRect, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$bmp.Dispose()

# Flood-fill outer background to transparent starting from edges
Write-Host "Flood-filling exterior white border with alpha transparency..."
$visited = New-Object 'bool[,]' $croppedBmp.Width, $croppedBmp.Height
$queue = New-Object System.Collections.Generic.Queue[System.Drawing.Point]

for ($x = 0; $x -lt $croppedBmp.Width; $x++) {
    $queue.Enqueue((New-Object System.Drawing.Point($x, 0)))
    $queue.Enqueue((New-Object System.Drawing.Point($x, ($croppedBmp.Height - 1))))
}
for ($y = 0; $y -lt $croppedBmp.Height; $y++) {
    $queue.Enqueue((New-Object System.Drawing.Point(0, $y)))
    $queue.Enqueue((New-Object System.Drawing.Point(($croppedBmp.Width - 1), $y)))
}

while ($queue.Count -gt 0) {
    $pt = $queue.Dequeue()
    $px = $pt.X
    $py = $pt.Y
    if ($px -lt 0 -or $px -ge $croppedBmp.Width -or $py -lt 0 -or $py -ge $croppedBmp.Height) { continue }
    if ($visited[$px, $py]) { continue }
    $visited[$px, $py] = $true
    
    $c = $croppedBmp.GetPixel($px, $py)
    if ($c.R -gt 230 -and $c.G -gt 230 -and $c.B -gt 230) {
        $croppedBmp.SetPixel($px, $py, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
        $queue.Enqueue((New-Object System.Drawing.Point(($px + 1), $py)))
        $queue.Enqueue((New-Object System.Drawing.Point(($px - 1), $py)))
        $queue.Enqueue((New-Object System.Drawing.Point($px, ($py + 1))))
        $queue.Enqueue((New-Object System.Drawing.Point($px, ($py - 1))))
    } elseif ($c.R -gt 210 -and $c.G -gt 210 -and $c.B -gt 210) {
        # Soft antialiasing for outer corner edges
        $avg = ($c.R + $c.G + $c.B) / 3.0
        $alpha = [int]([Math]::Max(0, [Math]::Min(255, (230 - $avg) / 20.0 * 255.0)))
        $croppedBmp.SetPixel($px, $py, [System.Drawing.Color]::FromArgb($alpha, $c.R, $c.G, $c.B))
    }
}

# Save new high quality cropped PNG
$outPng = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\assets\keydeck-icon.png")
$croppedBmp.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved cropped PNG to $outPng"

# Generate multi-resolution ICO file (256, 128, 64, 48, 32, 16)
$sizes = @(256, 128, 64, 48, 32, 16)
$iconFrames = @()

foreach ($sz in $sizes) {
    $frameBmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $fg = [System.Drawing.Graphics]::FromImage($frameBmp)
    $fg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $fg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $fg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $fg.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $fg.DrawImage($croppedBmp, 0, 0, $sz, $sz)
    $fg.Dispose()
    
    $ms = New-Object System.IO.MemoryStream
    $frameBmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $ms.ToArray()
    $ms.Dispose()
    $frameBmp.Dispose()

    $iconFrames += [PSCustomObject]@{
        Size = $sz
        PngBytes = $pngBytes
    }
}
$croppedBmp.Dispose()

# Write ICO binary format
function Write-IcoFile($outputPath, $frames) {
    $dir = [System.IO.Path]::GetDirectoryName($outputPath)
    if (-not (Test-Path $dir)) {
        [System.IO.Directory]::CreateDirectory($dir) | Out-Null
    }
    
    $fs = [System.IO.File]::Create($outputPath)
    $bw = New-Object System.IO.BinaryWriter($fs)
    
    # ICONDIR
    $bw.Write([uint16]0) # Reserved
    $bw.Write([uint16]1) # Image type: 1 = ICO
    $bw.Write([uint16]$frames.Count) # Count
    
    $offset = 6 + ($frames.Count * 16)
    
    # ICONDIRENTRY list
    foreach ($f in $frames) {
        $w = if ($f.Size -eq 256) { [byte]0 } else { [byte]$f.Size }
        $h = if ($f.Size -eq 256) { [byte]0 } else { [byte]$f.Size }
        $bw.Write($w)               # Width
        $bw.Write($h)               # Height
        $bw.Write([byte]0)          # Color count (0 for 32bpp)
        $bw.Write([byte]0)          # Reserved
        $bw.Write([uint16]1)        # Planes
        $bw.Write([uint16]32)       # Bits per pixel
        $bw.Write([uint32]$f.PngBytes.Length) # Image size
        $bw.Write([uint32]$offset)  # Image offset
        $offset += $f.PngBytes.Length
    }
    
    # Image data
    foreach ($f in $frames) {
        $bw.Write($f.PngBytes)
    }
    
    $bw.Flush()
    $bw.Close()
    $fs.Close()
}

$icoDest1 = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\src\KeyDeck\app.ico")
$icoDest2 = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\assets\app.ico")

Write-IcoFile $icoDest1 $iconFrames
Write-IcoFile $icoDest2 $iconFrames

Write-Host "Successfully generated ICO files at $icoDest1 and $icoDest2"
