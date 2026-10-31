# Appends one JSON line per Copilot hook event to logs/ai-interactions.jsonl.
# Every line has the SAME fields, so the log is structured and uniform.
# Usage (from the hook config): log-interaction.ps1 <eventName>
# The script never blocks the agent: it always exits 0.

param([string]$EventName = "unknown")

try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
    $logDir = Join-Path $repoRoot "logs"
    $logFile = Join-Path $logDir "ai-interactions.jsonl"
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }

    $raw = [Console]::In.ReadToEnd()
    $payload = $null
    if ($raw -and $raw.Trim().Length -gt 0) {
        try { $payload = $raw | ConvertFrom-Json } catch { $payload = $null }
    }

    # Return the first property that exists (supports camelCase and snake_case payloads).
    function Get-First($obj, [string[]]$names) {
        if ($null -eq $obj) { return $null }
        foreach ($name in $names) {
            $prop = $obj.PSObject.Properties[$name]
            if ($null -ne $prop -and $null -ne $prop.Value) { return $prop.Value }
        }
        return $null
    }

    # Keep entries short and mask obvious secrets.
    function Format-Text($value, [int]$max = 600) {
        if ($null -eq $value) { return "" }
        if ($value -isnot [string]) { $value = $value | ConvertTo-Json -Compress -Depth 5 }
        $value = $value -replace '(ghp_|github_pat_|sk-)[A-Za-z0-9_\-]+', '$1***'
        $value = $value -replace '\s+', ' '
        if ($value.Length -gt $max) { $value = $value.Substring(0, $max) + "..." }
        return $value.Trim()
    }

    $toolResult = Get-First $payload @("toolResult", "tool_response")
    $resultText = if ($toolResult -is [string]) { $toolResult }
                  else { Get-First $toolResult @("textResultForLlm", "resultType") }

    $entry = [ordered]@{
        timestamp      = (Get-Date).ToUniversalTime().ToString("o")
        event          = $EventName
        session_id     = Format-Text (Get-First $payload @("sessionId", "session_id")) 80
        agent          = Format-Text (Get-First $payload @("agent_name", "agentName", "agent_display_name")) 80
        prompt         = Format-Text (Get-First $payload @("prompt"))
        tool           = Format-Text (Get-First $payload @("toolName", "tool_name")) 80
        tool_input     = Format-Text (Get-First $payload @("toolArgs", "tool_input")) 300
        result_summary = Format-Text $resultText 300
        reason         = Format-Text (Get-First $payload @("reason", "stop_reason")) 80
    }

    $line = $entry | ConvertTo-Json -Compress
    Add-Content -Path $logFile -Value $line -Encoding utf8
}
catch {
    # Logging must never break the agent session.
}
exit 0
