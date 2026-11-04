param(
    [string]$inPath,
    [string]$outPath
)

$lines = Get-Content $inPath
$out = New-Object System.Collections.Generic.List[string]

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match '^\s*var debtId = await AddDebtAsync\(id, amount: 100, dueDate: Day\(.-1\);\\);$') {
        $out.Add($line)
        $out.Add('')
        $out.Add('        var first = await _client.PostAsync($\"/debts/{debtId}/pay\", content: null);')
        $out.Add('        var second = await _client.PostAsync($\"/debts/{debtId}/pay\", content: null);')
        $out.Add('')
        $out.Add('        Assert.Equal(HttpStatusCode.OK, first.StatusCode);')
        $out.Add('        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);')
        $out.Add('    }')
        $i++
        continue
    }
    $out.Add($line)
    $i++
}

$content = $out -join [Environment]::NewLine
Set-Content -Path $outPath -Value $content -Force
