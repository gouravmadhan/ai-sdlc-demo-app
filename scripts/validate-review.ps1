# Validates that a review findings file (or PR description saved to a file)
# contains a table in the fixed format:
#   | Finding | Severity | Verdict | Reason |
# Verdict must be Fixed, Dismissed or Deferred and Reason must not be empty.
#
# Usage:
#   .\scripts\validate-review.ps1 -Path review-findings.md
#   .\scripts\validate-review.ps1 -Path review-findings.md -AllowPending   # agent output, verdicts not given yet

param(
    [Parameter(Mandatory = $true)][string]$Path,
    [switch]$AllowPending
)

$allowedVerdicts = @("Fixed", "Dismissed", "Deferred")
if ($AllowPending) { $allowedVerdicts += "Pending" }
$allowedSeverities = @("High", "Medium", "Low", "Info")

if (-not (Test-Path $Path)) { Write-Error "File not found: $Path"; exit 1 }

$lines = Get-Content $Path
$headerIndex = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\|\s*Finding\s*\|\s*Severity\s*\|\s*Verdict\s*\|\s*Reason\s*\|\s*$') { $headerIndex = $i; break }
}
if ($headerIndex -lt 0) {
    Write-Error "Missing table header: | Finding | Severity | Verdict | Reason |"
    exit 1
}

$errors = @()
$rowCount = 0
for ($i = $headerIndex + 2; $i -lt $lines.Count; $i++) {
    $line = $lines[$i].Trim()
    if (-not $line.StartsWith("|")) { break }
    $cells = $line.Trim("|").Split("|") | ForEach-Object { $_.Trim() }
    $rowCount++
    if ($cells.Count -ne 4) { $errors += "Row ${rowCount}: expected 4 columns, found $($cells.Count)"; continue }
    if (-not $cells[0]) { $errors += "Row ${rowCount}: Finding is empty" }
    if ($allowedSeverities -notcontains $cells[1]) { $errors += "Row ${rowCount}: Severity '$($cells[1])' must be one of $($allowedSeverities -join ', ')" }
    if ($allowedVerdicts -notcontains $cells[2]) { $errors += "Row ${rowCount}: Verdict '$($cells[2])' must be one of $($allowedVerdicts -join ', ')" }
    if (-not $cells[3] -or $cells[3] -eq "-") { $errors += "Row ${rowCount}: Reason is empty (every verdict needs a reason or commit)" }
}

if ($rowCount -eq 0) { $errors += "Table has no findings rows" }

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Host "FAIL: $_" -ForegroundColor Red }
    exit 1
}
Write-Host "OK: $rowCount findings, format valid." -ForegroundColor Green
exit 0
