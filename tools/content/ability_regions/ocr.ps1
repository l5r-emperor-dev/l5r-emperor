# Issue #1309: reads the text lines and word boxes of card scans with the Windows OCR engine (Windows only; no extra install).
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File ocr.ps1 -List paths.txt -Out ocr.jsonl  (one JSON object per scan).
param([string]$List,[string]$Out)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics,ContentType=WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | ? { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, $t) { $task = $asTask.MakeGenericMethod($t).Invoke($null, @($op)); $task.Wait(); $task.Result }
$eng = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
$w = [System.IO.StreamWriter]::new($Out, $false, [System.Text.UTF8Encoding]::new($false))
foreach ($p in [System.IO.File]::ReadAllLines($List)) {
  $file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync(($p -replace '/','\'))) ([Windows.Storage.StorageFile])
  $stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
  $dec = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
  $bmp = Await ($dec.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
  $res = Await ($eng.RecognizeAsync($bmp)) ([Windows.Media.Ocr.OcrResult])
  $lines = foreach ($l in $res.Lines) { @{ t = $l.Text; w = @($l.Words | % { ,@($_.Text, [int]$_.BoundingRect.X, [int]$_.BoundingRect.Y, [int]$_.BoundingRect.Width, [int]$_.BoundingRect.Height) }) } }
  $w.WriteLine((@{ path = [string]$p; w = $dec.PixelWidth; h = $dec.PixelHeight; lines = @($lines) } | ConvertTo-Json -Depth 6 -Compress))
  $stream.Dispose()
}
$w.Close()
