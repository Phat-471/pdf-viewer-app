param(
    [string[]]$Roots = @("E:\code", "C:\Users\IT\Desktop", "C:\Users\IT\Documents", "C:\Users\IT\Downloads"),
    [switch]$AllDrives
)

$ErrorActionPreference = "Stop"

if ($AllDrives) {
    $Roots = Get-PSDrive -PSProvider FileSystem |
        Where-Object { Test-Path $_.Root } |
        ForEach-Object { $_.Root }
}

$patterns = @(
    "PdfViewerApp.csproj",
    "PdfDocumentTab.xaml.cs",
    "PdfCore/Cargo.toml",
    "PdfCore\Cargo.toml"
)

$excludeArgs = @(
    "-g", "!**/bin/**",
    "-g", "!**/obj/**",
    "-g", "!**/target/**",
    "-g", "!**/node_modules/**",
    "-g", "!**/.git/**",
    "-g", "!**/.vs/**",
    "-g", "!**/packages/**"
)

$results = New-Object System.Collections.Generic.List[string]
$rg = Get-Command rg -ErrorAction SilentlyContinue

foreach ($root in $Roots) {
    if (-not (Test-Path -LiteralPath $root)) {
        continue
    }

    Write-Host "Scanning $root" -ForegroundColor Cyan

    if ($rg) {
        foreach ($pattern in $patterns) {
            $rgOutput = & rg --files $root @excludeArgs -g $pattern 2>$null
            foreach ($line in $rgOutput) {
                if (-not [string]::IsNullOrWhiteSpace($line)) {
                    $results.Add($line)
                }
            }
        }
    }
    else {
        foreach ($name in @("PdfViewerApp.csproj", "PdfDocumentTab.xaml.cs", "Cargo.toml")) {
            Get-ChildItem -LiteralPath $root -Recurse -File -Filter $name -ErrorAction SilentlyContinue |
                Where-Object {
                    $_.FullName -notmatch '\\(bin|obj|target|node_modules|\.git|\.vs|packages)\\' -and
                    ($_.Name -ne "Cargo.toml" -or $_.FullName -match 'PdfCore|pdf|PDF')
                } |
                ForEach-Object { $results.Add($_.FullName) }
        }
    }
}

$uniqueResults = $results | Sort-Object -Unique
if (-not $uniqueResults -or $uniqueResults.Count -eq 0) {
    Write-Host "No PdfViewerApp/PdfCore source markers were found." -ForegroundColor Yellow
    exit 1
}

$uniqueResults | ForEach-Object { Write-Host $_ }
