Add-Type -AssemblyName System.Drawing

$inputPath = "c:\Users\Uri\Desktop\NewPCSetupWPF\Images\app_icon.png"
$outputPath = "c:\Users\Uri\Desktop\NewPCSetupWPF\Images\app_icon.png"

# Load image
$img = [System.Drawing.Bitmap]::FromFile($inputPath)

# 1. Find bounding box of non-transparent pixels
$minX = $img.Width
$minY = $img.Height
$maxX = 0
$maxY = 0
$found = $false

for ($x = 0; $x -lt $img.Width; $x++) {
    for ($y = 0; $y -lt $img.Height; $y++) {
        $pixel = $img.GetPixel($x, $y)
        if ($pixel.A -gt 10) { # Not transparent
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
            $found = $true
        }
    }
}

if (-not $found) {
    Write-Host "No content found. Exiting."
    $img.Dispose()
    exit
}

# Add a small padding
$padding = 10
$minX = [Math]::Max(0, $minX - $padding)
$minY = [Math]::Max(0, $minY - $padding)
$maxX = [Math]::Min($img.Width - 1, $maxX + $padding)
$maxY = [Math]::Min($img.Height - 1, $maxY + $padding)

$contentWidth = $maxX - $minX + 1
$contentHeight = $maxY - $minY + 1

# 2. Crop
$rect = New-Object System.Drawing.Rectangle($minX, $minY, $contentWidth, $contentHeight)
$cropped = $img.Clone($rect, $img.PixelFormat)
$img.Dispose()

# 3. Resize to square (max dimension)
$size = [Math]::Max($contentWidth, $contentHeight)
$finalSize = 256 # Target icon size
$square = New-Object System.Drawing.Bitmap($finalSize, $finalSize)
$g = [System.Drawing.Graphics]::FromImage($square)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

# Center the content, scaling up to fit 256x256
$scale = [Math]::Min($finalSize / $contentWidth, $finalSize / $contentHeight)
$newW = [int]($contentWidth * $scale)
$newH = [int]($contentHeight * $scale)
$posX = [int](($finalSize - $newW) / 2)
$posY = [int](($finalSize - $newH) / 2)

$g.DrawImage($cropped, $posX, $posY, $newW, $newH)
$g.Dispose()
$cropped.Dispose()

# Save
$square.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$square.Dispose()

Write-Host "Icon maximized."
