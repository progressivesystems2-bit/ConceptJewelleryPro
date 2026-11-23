$path = 'E:\repos\ConceptJewelleryPro\jydata.DBC'
$bytes = [System.IO.File]::ReadAllBytes($path)

$numRecords = [BitConverter]::ToInt32($bytes, 4)
$headerLength = [BitConverter]::ToInt16($bytes, 8)
$recordLength = [BitConverter]::ToInt16($bytes, 10)

Write-Host "Header Length: $headerLength"
Write-Host "Record Length: $recordLength"
Write-Host "Total Records: $numRecords"

$fields = @()
$offset = 32
while ($offset -lt $headerLength -and $bytes[$offset] -ne 0x0D -and $bytes[$offset] -ne 0x00) {
    $fieldName = [System.Text.Encoding]::ASCII.GetString($bytes, $offset, 11).Trim([char]0).Trim()
    $fieldType = [char]$bytes[$offset + 11]
    $fieldLen = $bytes[$offset + 16]
    $fieldDec = $bytes[$offset + 17]
    $fields += [PSCustomObject]@{ Name = $fieldName; Type = $fieldType; Length = $fieldLen; Dec = $fieldDec }
    $offset += 32
}

Write-Host "Fields in jydata.DBC:"
$fields | Format-Table -AutoSize
