Add-Type -AssemblyName System.Drawing

$inputPath = "c:\Users\Uri\Desktop\NewPCSetupWPF\Images\app_icon.png"
$outputPath = "c:\Users\Uri\Desktop\NewPCSetupWPF\Images\app_icon.png"

# Load image
$img = [System.Drawing.Bitmap]::FromFile($inputPath)
$newImg = New-Object System.Drawing.Bitmap($img.Width, $img.Height)
$newImg.SetResolution($img.HorizontalResolution, $img.VerticalResolution)

# Lock bits for speed (optional, but good for pixel iteration)
# For simplicity in PS, we'll iterate pixels. It's slow but fine for one icon.

$tolerance = 40 # 0-255

for ($x = 0; $x -lt $img.Width; $x++) {
    for ($y = 0; $y -lt $img.Height; $y++) {
        $pixel = $img.GetPixel($x, $y)
        
        # Check if pixel is "black-ish"
        # Simple distance check: if R+G+B is low
        $brightness = $pixel.R + $pixel.G + $pixel.B
        
        if ($brightness -lt $tolerance) {
            # Fully transparent
            $newImg.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
        }
        else {
            # Keep original
            $newImg.SetPixel($x, $y, $pixel)
        }
    }
}

$img.Dispose() # Release file lock

# Save
$newImg.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$newImg.Dispose()

Write-Host "Smart transparency applied."
