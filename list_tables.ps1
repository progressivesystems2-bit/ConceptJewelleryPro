$path = 'E:\repos\ConceptJewelleryPro\jydata.DBC'
$bytes = [System.IO.File]::ReadAllBytes($path)

$numRecords = [BitConverter]::ToInt32($bytes, 4)
$headerLength = [BitConverter]::ToInt16($bytes, 8)
$recordLength = [BitConverter]::ToInt16($bytes, 10)

$records = @()
$tables = [System.Collections.Generic.List[PSCustomObject]]::new()
$relations = [System.Collections.Generic.List[PSCustomObject]]::new()
$views = [System.Collections.Generic.List[PSCustomObject]]::new()
$otherTypes = [System.Collections.Generic.Dictionary[string, int]]::new()

for ($i = 0; $i -lt $numRecords; $i++) {
    $recOffset = $headerLength + ($i * $recordLength)
    if ($recOffset + $recordLength -gt $bytes.Length) { break }
    
    # 1st byte is deletion flag (0x20 = active, 0x2A = deleted)
    $isDeleted = ($bytes[$recOffset] -eq 0x2A)
    if ($isDeleted) { continue }
    
    $objId = [BitConverter]::ToInt32($bytes, $recOffset + 1)
    $parentId = [BitConverter]::ToInt32($bytes, $recOffset + 5)
    $objType = [System.Text.Encoding]::ASCII.GetString($bytes, $recOffset + 9, 10).Trim()
    $objName = [System.Text.Encoding]::ASCII.GetString($bytes, $recOffset + 19, 128).Trim([char]0).Trim()
    
    if (-not $otherTypes.ContainsKey($objType)) {
        $otherTypes[$objType] = 0
    }
    $otherTypes[$objType]++

    if ($objType -eq 'Table') {
        $tables.Add([PSCustomObject]@{ Id = $objId; Name = $objName; ParentId = $parentId })
    } elseif ($objType -eq 'Relation') {
        $relations.Add([PSCustomObject]@{ Id = $objId; Name = $objName; ParentId = $parentId })
    } elseif ($objType -eq 'View') {
        $views.Add([PSCustomObject]@{ Id = $objId; Name = $objName; ParentId = $parentId })
    }
}

Write-Host "Object Types Count in Database:"
$otherTypes.GetEnumerator() | Sort-Object Value -Descending | Format-Table -AutoSize

Write-Host "`nTotal Unique Tables Found: $($tables.Count)"
Write-Host "Sample of Tables (first 50):"
$tables | Select-Object -First 50 | Format-Table -AutoSize
