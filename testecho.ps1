Write-Host 'count=', .Count
Write-Host 'atpl=', @().Count
if () { Write-Host 'hasargs'} else { Write-Host 'no args'}
if ( -ne ) { Write-Host 'hasargs' } else { Write-Host 'no args' }
