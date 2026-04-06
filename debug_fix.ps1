$bytes = [System.IO.File]::ReadAllBytes('C:\Data\NBH\UserProfilePage.xaml')
$text = [System.Text.Encoding]::UTF8.GetString($bytes)

# Test needle for u-umlaut: U+00C3, U+0192, U+00C2, U+00BC
$needle_ue = [string][char[]]@(0x00C3, 0x0192, 0x00C2, 0x00BC)
Write-Host "Needle ue length: $($needle_ue.Length)"
Write-Host "Needle ue chars: $($needle_ue.ToCharArray() | ForEach-Object { 'U+{0:X4}' -f [int]$_ })"
$idx = $text.IndexOf($needle_ue)
Write-Host "Found needle_ue at index: $idx"

# Test needle for U-umlaut: U+00C3, U+0192, U+00C5, U+201C
$needle_Ue = [string][char[]]@(0x00C3, 0x0192, 0x00C5, 0x201C)
Write-Host "Needle Ue length: $($needle_Ue.Length)"
$idx2 = $text.IndexOf($needle_Ue)
Write-Host "Found needle_Ue at index: $idx2"

# Manual direct replacement test
$repl_ue = [string][char]0x00FC
$repl_Ue = [string][char]0x00DC

$newtext = $text.Replace($needle_ue, $repl_ue).Replace($needle_Ue, $repl_Ue)
Write-Host "Before length: $($text.Length), After length: $($newtext.Length), Diff: $($text.Length - $newtext.Length)"

# Show target lines after fix
$lines = $newtext -split "`n"
for ($i = 0; $i -lt $lines.Length; $i++) {
    $line = $lines[$i]
    if ($line -match 'ber mich|Zur') {
        Write-Host "Line $($i+1) after: $($line.Trim())"
    }
}
