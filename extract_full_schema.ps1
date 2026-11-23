$path = 'E:\repos\ConceptJewelleryPro\jydata.DBC'
$bytes = [System.IO.File]::ReadAllBytes($path)

$numRecords = [BitConverter]::ToInt32($bytes, 4)
$headerLength = [BitConverter]::ToInt16($bytes, 8)
$recordLength = [BitConverter]::ToInt16($bytes, 10)

$tablesById = @{}
$fieldsByParentId = @{}

for ($i = 0; $i -lt $numRecords; $i++) {
    $recOffset = $headerLength + ($i * $recordLength)
    if ($recOffset + $recordLength -gt $bytes.Length) { break }
    if ($bytes[$recOffset] -eq 0x2A) { continue } # Deleted record

    $objId = [BitConverter]::ToInt32($bytes, $recOffset + 1)
    $parentId = [BitConverter]::ToInt32($bytes, $recOffset + 5)
    $objType = [System.Text.Encoding]::ASCII.GetString($bytes, $recOffset + 9, 10).Trim()
    $objName = [System.Text.Encoding]::ASCII.GetString($bytes, $recOffset + 19, 128).Trim([char]0).Trim()

    if ($objType -eq 'Table') {
        $tablesById[$objId] = $objName
        if (-not $fieldsByParentId.ContainsKey($objId)) {
            $fieldsByParentId[$objId] = [System.Collections.Generic.List[string]]::new()
        }
    } elseif ($objType -eq 'Field') {
        if (-not $fieldsByParentId.ContainsKey($parentId)) {
            $fieldsByParentId[$parentId] = [System.Collections.Generic.List[string]]::new()
        }
        $fieldsByParentId[$parentId].Add($objName)
    }
}

$allTableNames = $tablesById.Values | Sort-Object
Write-Host "All $($allTableNames.Count) Tables in Database Catalog:"
$allTableNames -join ", " | Write-Host

# Write full schema summary to a markdown / text file
$outputLines = [System.Collections.Generic.List[string]]::new()
$outputLines.Add("# Complete Database Dictionary (from jydata.DBC)")
$outputLines.Add("Total Tables: $($tablesById.Count)")
$outputLines.Add("")

foreach ($tableId in ($tablesById.Keys | Sort-Object { $tablesById[$_] })) {
    $tableName = $tablesById[$tableId]
    $fields = $fieldsByParentId[$tableId]
    $outputLines.Add("## Table: $tableName (ID: $tableId, Total Columns: $($fields.Count))")
    $outputLines.Add("Columns: " + ($fields -join ", "))
    $outputLines.Add("")
}

[System.IO.File]::WriteAllLines('E:\repos\ConceptJewelleryPro\Database_Schema_Dictionary.md', $outputLines)
Write-Host "`nGenerated full schema dictionary at: E:\repos\ConceptJewelleryPro\Database_Schema_Dictionary.md"
