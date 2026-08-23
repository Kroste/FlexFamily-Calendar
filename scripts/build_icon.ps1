<#
.SYNOPSIS
    Generiert das FlexFamily-Calendar-App-Icon reproduzierbar aus Code.

.DESCRIPTION
    Gleichwertiger Port von scripts/build_icon.py ohne Python-Abhängigkeit — der
    Arbeitslaptop hat nur den WindowsApps-Store-Stub, kein echtes Python.

    Motiv: dunkelblauer Rundeck-Grund (Marken-Akzent) plus weißes Kalenderblatt mit
    farbigen Punkten für die Familienmitglieder. Bei 16x16 als Kalender erkennbar
    (weiße Fläche mit farbigem Header), bei 256x256 mit Personen-Punkten lesbar.

    Ausgabe:
      src/Assets/flexfamily-calendar.png   256x256, transparent, Master
      src/Assets/flexfamily-calendar.ico   multi-res 16/24/32/48/64/128/256

    WICHTIG: Motiv und Farben mit build_icon.py synchron halten. Driften die
    auseinander, sieht das Icon je nach Rechner unterschiedlich aus.

    Die Datei ist UTF-8 mit BOM gespeichert: Windows PowerShell 5.1 liest .ps1 ohne
    BOM als ANSI und macht aus "ü" ein "³".
#>

$ErrorActionPreference = 'Stop'

# System.Drawing ist seit .NET 7 Windows-only. Auf Linux/macOS ist build_icon.py der Weg —
# ohne diesen Guard stirbt das Skript mit einem unverständlichen GDI+-Initialisierungsfehler.
if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') {
    Write-Error 'build_icon.ps1 läuft nur unter Windows. Auf Linux/macOS: python3 scripts/build_icon.py'
}

Add-Type -AssemblyName System.Drawing

$Size    = 256
$OutDir  = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Assets'
$AppName = 'flexfamily-calendar'

$Accent      = [System.Drawing.Color]::FromArgb(255, 26, 35, 126)    # #1A237E Marken-Indigo
$AccentLight = [System.Drawing.Color]::FromArgb(255, 63, 76, 179)    # Header-Streifen
$Paper       = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$Ring        = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)

# Personenfarben aus der App-Palette (UserColorPalette).
$PersonColors = @(
    [System.Drawing.Color]::FromArgb(255, 46, 134, 193)   # Blau
    [System.Drawing.Color]::FromArgb(255, 230, 126, 34)   # Orange
    [System.Drawing.Color]::FromArgb(255, 39, 174, 96)    # Grün
    [System.Drawing.Color]::FromArgb(255, 142, 68, 173)   # Violett
    [System.Drawing.Color]::FromArgb(255, 192, 57, 43)    # Rot
)

function New-RoundedPath {
    param([int]$X, [int]$Y, [int]$W, [int]$H, [int]$Radius)

    $d = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X,             $Y,             $d, $d, 180, 90)
    $path.AddArc($X + $W - $d,   $Y,             $d, $d, 270, 90)
    $path.AddArc($X + $W - $d,   $Y + $H - $d,   $d, $d,   0, 90)
    $path.AddArc($X,             $Y + $H - $d,   $d, $d,  90, 90)
    $path.CloseFigure()
    return $path
}

function Add-RoundedRect {
    param($Graphics, [int]$X, [int]$Y, [int]$W, [int]$H, [int]$Radius, $Color)

    $path  = New-RoundedPath -X $X -Y $Y -W $W -H $H -Radius $Radius
    $brush = New-Object System.Drawing.SolidBrush $Color
    try { $Graphics.FillPath($brush, $path) }
    finally { $brush.Dispose(); $path.Dispose() }
}

function Add-Circle {
    param($Graphics, [int]$CenterX, [int]$CenterY, [int]$R, $Color)

    $brush = New-Object System.Drawing.SolidBrush $Color
    try { $Graphics.FillEllipse($brush, $CenterX - $R, $CenterY - $R, $R * 2, $R * 2) }
    finally { $brush.Dispose() }
}

function New-Master {
    $bmp = New-Object System.Drawing.Bitmap $Size, $Size,
        ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.Clear([System.Drawing.Color]::Transparent)

        # Rundeck-Grund (App-Akzent).
        Add-RoundedRect -Graphics $g -X 0 -Y 0 -W $Size -H $Size -Radius 44 -Color $Accent

        # Kalenderblatt.
        $padX = 44; $padTop = 68; $padBot = 40
        $paperX = $padX
        $paperY = $padTop
        $paperW = $Size - 2 * $padX
        $paperH = $Size - $padBot - $padTop
        Add-RoundedRect -Graphics $g -X $paperX -Y $paperY -W $paperW -H $paperH -Radius 14 -Color $Paper

        # Header-Streifen; die unteren Ecken werden wieder eckig überzeichnet.
        $headerH = 34
        Add-RoundedRect -Graphics $g -X $paperX -Y $paperY -W $paperW -H $headerH -Radius 14 -Color $AccentLight
        $brush = New-Object System.Drawing.SolidBrush $AccentLight
        try { $g.FillRectangle($brush, $paperX, $paperY + $headerH - 14, $paperW, 14) }
        finally { $brush.Dispose() }

        # Zwei Aufhänger oben (typisches Kalender-Detail).
        $ringR = 7
        $ringY = $paperY - 4
        foreach ($cx in @($paperX + 40, $paperX + $paperW - 40)) {
            Add-Circle -Graphics $g -CenterX $cx -CenterY $ringY -R $ringR -Color $Ring
        }

        # Familien-Punkte: 2 Reihen x 3 Spalten, fünf Farben.
        $innerTop   = $paperY + $headerH + 22
        $innerBot   = $paperY + $paperH - 22
        $innerLeft  = $paperX + 24
        $innerRight = $paperX + $paperW - 24
        $dotR = 18
        $cols = 3; $rows = 2
        $stepX = ($innerRight - $innerLeft) / ($cols - 1)
        $stepY = ($innerBot - $innerTop) / ($rows - 1)

        $i = 0
        for ($r = 0; $r -lt $rows; $r++) {
            for ($c = 0; $c -lt $cols; $c++) {
                if ($i -ge $PersonColors.Count) { break }
                $cx = [int]($innerLeft + $c * $stepX)
                $cy = [int]($innerTop  + $r * $stepY)
                Add-Circle -Graphics $g -CenterX $cx -CenterY $cy -R $dotR -Color $PersonColors[$i]
                $i++
            }
        }
    }
    finally { $g.Dispose() }

    return $bmp
}

function Save-MultiResIco {
    param($Master, [string]$Path, [int[]]$Sizes)

    # System.Drawing kann kein Multi-Res-ICO schreiben — das Containerformat entsteht
    # hier von Hand. Seit Vista dürfen die Einträge eingebettete PNGs sein, das spart
    # den DIB-Weg samt AND-Maske.
    $pngs = @()
    foreach ($s in $Sizes) {
        $scaled = New-Object System.Drawing.Bitmap $s, $s,
            ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($scaled)
        try {
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.DrawImage($Master, 0, 0, $s, $s)
        }
        finally { $g.Dispose() }

        $ms = New-Object System.IO.MemoryStream
        $scaled.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngs += , @{ Size = $s; Bytes = $ms.ToArray() }
        $ms.Dispose(); $scaled.Dispose()
    }

    $fs = [System.IO.File]::Create($Path)
    $bw = New-Object System.IO.BinaryWriter $fs
    try {
        # ICONDIR
        $bw.Write([uint16]0)                  # reserviert
        $bw.Write([uint16]1)                  # Typ 1 = Icon
        $bw.Write([uint16]$pngs.Count)

        # ICONDIRENTRY je Bild; 256 wird als 0 kodiert (ein Byte pro Kantenlänge).
        $offset = 6 + 16 * $pngs.Count
        foreach ($p in $pngs) {
            $dim = if ($p.Size -ge 256) { 0 } else { $p.Size }
            $bw.Write([byte]$dim)             # Breite
            $bw.Write([byte]$dim)             # Höhe
            $bw.Write([byte]0)                # Palettenfarben
            $bw.Write([byte]0)                # reserviert
            $bw.Write([uint16]1)              # Farbebenen (bei PNG-Einträgen ignoriert)
            $bw.Write([uint16]32)             # Bits pro Pixel
            $bw.Write([uint32]$p.Bytes.Length)
            $bw.Write([uint32]$offset)
            $offset += $p.Bytes.Length
        }

        foreach ($p in $pngs) { $bw.Write($p.Bytes) }
    }
    finally { $bw.Dispose(); $fs.Dispose() }
}

# --- Ablauf ------------------------------------------------------------------

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$master = New-Master
try {
    $pngPath = Join-Path $OutDir "$AppName.png"
    $master.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "geschrieben: $pngPath"

    $icoPath = Join-Path $OutDir "$AppName.ico"
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    Save-MultiResIco -Master $master -Path $icoPath -Sizes $sizes
    Write-Host "geschrieben: $icoPath (Größen: $($sizes -join ', '))"
}
finally { $master.Dispose() }
