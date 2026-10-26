param([string]$BundleRoot = $PSScriptRoot, [switch]$AssembleOnly)
$ErrorActionPreference = 'Stop'
try {
    $bundle = Get-Content -Raw -LiteralPath (Join-Path $BundleRoot 'review-bundle.json') | ConvertFrom-Json
    if ($bundle.folder -notmatch '^L5R-Review-pr[0-9]+-[a-f0-9]{10}-r[0-9]+$' -or $bundle.source_commit -notmatch '^[a-f0-9]{40}$') { throw 'Invalid preview identity' }
    $payload = Join-Path $BundleRoot 'payload'
    $review = Join-Path $payload $bundle.folder
    if (-not (Test-Path -LiteralPath (Join-Path $review 'preview.json'))) {
        if (Test-Path -LiteralPath $payload) { throw 'Incomplete payload folder. Extract this version into a fresh folder and retry.' }
        $temporary = Join-Path $BundleRoot ('assembling-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $temporary | Out-Null
        try {
            $combined = Join-Path $temporary 'complete.zip'
            $destination = [System.IO.File]::Open($combined, [System.IO.FileMode]::CreateNew)
            try {
                foreach ($part in $bundle.parts) {
                    if ($part.file -notmatch '^game\.zip\.[0-9]{3}$' -or $part.sha256 -notmatch '^[a-f0-9]{64}$') { throw 'Invalid package part' }
                    $path = Join-Path $BundleRoot $part.file
                    if (-not (Test-Path -LiteralPath $path)) { throw "Missing $($part.file). Extract ALL download ZIP parts into the same destination folder, then retry." }
                    if ((Get-Item -LiteralPath $path).Length -ne $part.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $part.sha256) { throw "Damaged package part: $($part.file). Download this version again." }
                    $inputStream = [System.IO.File]::OpenRead($path)
                    try { $inputStream.CopyTo($destination) } finally { $inputStream.Dispose() }
                    # Free the extracted part now that its bytes are in the combined archive: peak disk use stays near one copy of the game, not two.
                    Remove-Item -LiteralPath $path -Force
                }
            } finally { $destination.Dispose() }
            if ($bundle.sha256 -notmatch '^[a-f0-9]{64}$' -or (Get-Item -LiteralPath $combined).Length -ne $bundle.bytes -or (Get-FileHash -LiteralPath $combined -Algorithm SHA256).Hash.ToLowerInvariant() -ne $bundle.sha256) { throw 'Combined preview checksum mismatch. Extract ALL download ZIP parts into a fresh folder and retry.' }
            Write-Host 'Preparing the complete preview. This happens only on the first launch.'
            $expanded = Join-Path $temporary 'expanded'
            Expand-Archive -LiteralPath $combined -DestinationPath $expanded
            Move-Item -LiteralPath $expanded -Destination $payload
        } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force } }
    }
    $spec = Get-Content -Raw -LiteralPath (Join-Path $review 'preview.json') | ConvertFrom-Json
    if ($spec.source_commit -ne $bundle.source_commit -or -not (Test-Path -LiteralPath (Join-Path $review 'game\L5R-Emperor.pck')) -or -not (Test-Path -LiteralPath (Join-Path $review 'game\L5R-Emperor.exe'))) { throw 'Prepared preview does not match this version' }
    Write-Host 'WINDOWS_BUNDLE_PASS: exact complete archive assembled and isolated preview ready'
    if (-not $AssembleOnly) {
        $start = New-Object System.Diagnostics.ProcessStartInfo
        $start.FileName = $env:ComSpec
        $start.Arguments = '/d /s /c ""' + (Join-Path $review 'Start Review.cmd') + '""'
        $start.UseShellExecute = $false
        $process = [System.Diagnostics.Process]::Start($start)
        if (-not $process.WaitForExit(15000) -or $process.ExitCode -ne 0) { throw 'Prepared review launcher failed' }
    }
} catch { Write-Error $_; exit 1 }
