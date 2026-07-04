$apiKey = "6a8048c2342c6b08f8fdf1f64ccab2abba26efa37ad193f0ddcbc80fbd8605e2"
$filePath = "bin\Release\net10.0-windows\win-x64\publish\EldenRingAIOLauncher.exe"
$headers = @{ "x-apikey" = $apiKey }

Write-Host "Uploading $filePath to VirusTotal..."
$uploadUrl = "https://www.virustotal.com/api/v3/files"

try {
    $response = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Form @{ file = Get-Item -Path $filePath }
    $analysisId = $response.data.id
    Write-Host "Upload successful. Analysis ID: $analysisId"
} catch {
    Write-Host "Upload failed: $_"
    exit 1
}

$analysisUrl = "https://www.virustotal.com/api/v3/analyses/$analysisId"
Write-Host "Waiting for analysis to complete..."

$status = "queued"
while ($status -ne "completed") {
    Start-Sleep -Seconds 10
    $analysis = Invoke-RestMethod -Uri $analysisUrl -Method Get -Headers $headers
    $status = $analysis.data.attributes.status
    Write-Host "Current status: $status"
}

$stats = $analysis.data.attributes.stats
Write-Host "Analysis Complete!"
Write-Host "Malicious: $($stats.malicious)"
Write-Host "Suspicious: $($stats.suspicious)"
Write-Host "Undetected: $($stats.undetected)"
Write-Host "Harmless: $($stats.harmless)"

$results = $analysis.data.attributes.results
foreach ($engine in $results.Keys) {
    $result = $results.$engine
    if ($result.category -eq "malicious" -or $result.category -eq "suspicious") {
        Write-Host "FLAGGED by $engine : $($result.result)"
    }
}
