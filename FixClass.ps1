param(
    [string]$path
)

$lines = Get-Content $path
$out = New-Object System.Collections.Generic.List[string]

# Find the end of the class (the first '}' that closes the GymApiTests class).
# The class opens at line 8 with '{'. We track brace depth starting after the class opening.
$depth = 0
$inClass = $false
$classClosed = $false
$helperStart = -1

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match '^\s*public class GymApiTests') {
        $inClass = $true
        $depth = 1
        $out.Add($line)
        continue
    }
    if ($inClass) {
        $opens = ($line -replace '[^{]', '').Length
        $closes = ($line -replace '[^}]', '').Length
        $depth += $opens - $closes
        if ($depth -eq 0 -and -not $classClosed) {
            # This '}' closes the class
            $classClosed = $true
            # Add the ':' ... actually we need to keep this '}' 
            # But we want helpers INSIDE the class, so we need to restructure.
            # Actually the class closes at the FIRST '}' where depth becomes 0.
            # The helpers are after this, so they're outside.
            # We need to split: everything before the class close, then helpers, then the class close.
            $out.Add($line)
            $classClosedLine = $i
            continue
        }
        $out.Add($line)
    } else {
        $out.Add($line)
    }
}

# Now the helpers are the lines after the class close up to the end.
# We need to output: [everything before class close] + [helpers] + [class close]
# This is complex; instead, let's just write the corrected file directly.
"done"
