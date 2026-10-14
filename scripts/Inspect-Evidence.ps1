param(
    [string]$SourceRoot = 'C:\mkpulga\ccs-obs-stand-alone-without-git\ccs-obs-stand-alone',
    [string]$CollectionPath = 'D:\Downloads\ibayad-ors.postman_collection_current',
    [string]$WorkspaceRoot = ''
)
$ErrorActionPreference = 'Stop'
if (-not $WorkspaceRoot) { $WorkspaceRoot = Split-Path -Parent $PSScriptRoot }
$workspaceRoot = $WorkspaceRoot
$outputDirectory = Join-Path $workspaceRoot 'docs/evidence'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$collection = Get-Content -LiteralPath $CollectionPath -Raw | ConvertFrom-Json
$requests = [Collections.Generic.List[object]]::new()
function Inspect-Items($items, $folder) {
    foreach ($item in $items) {
        if ($item.item) { Inspect-Items $item.item ($folder + '/' + $item.name); continue }
        if (-not $item.request) { continue }
        $request = $item.request
        $segments = @($request.url.path)
        $routeSegments = foreach ($segment in $segments) {
            if ($segment -match '^\d|@|^\{\{') { '{identifier}' } else { $segment }
        }
        $fields = @()
        if ($request.body.raw) {
            try { $fields = @(($request.body.raw | ConvertFrom-Json).PSObject.Properties.Name) } catch { $fields = @('non-JSON') }
        } elseif ($request.body.formdata) { $fields = @($request.body.formdata.key) }
        $requests.Add([pscustomobject]@{ Folder=$folder; Name=$item.name; Method=$request.method; Route=($routeSegments -join '/');
            BodyFields=$fields; HeaderNames=@($request.header.key | Sort-Object -Unique); ResponseExamples=@($item.response).Count;
            Historical=($folder -like '*ors - disregard*'); Verification='Collection example only; not live verified' })
    }
}
Inspect-Items $collection.item ''
$requests | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputDirectory 'api-inventory.json') -Encoding utf8
$reports = foreach ($file in Get-ChildItem -LiteralPath (Join-Path $SourceRoot 'Report/RDLC') -Filter '*.rdlc') {
    [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw
    [pscustomobject]@{Template=$file.Name; Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;
        DataSets=@($xml.SelectNodes("//*[local-name()='DataSet']") | ForEach-Object { $_.Name });
        Fields=@($xml.SelectNodes("//*[local-name()='Field']") | ForEach-Object { $_.Name } | Sort-Object -Unique);
        Parameters=@($xml.SelectNodes("//*[local-name()='ReportParameter']") | ForEach-Object { $_.Name });
        HasPageNumber=[bool]($xml.InnerXml -match 'Globals!PageNumber'); HasTotalPages=[bool]($xml.InnerXml -match 'Globals!TotalPages');
        Verification='Layout and field inventory only; no generated report verified'}
}
$reports | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputDirectory 'report-inventory.json') -Encoding utf8
$sourceNames = @('Pages/Login.cs','Pages/Otp.cs','Controllers/sessionController.cs','Controllers/AccessAPI_v1.cs',
    'utils/FeatureSet.cs','utils/Dashboard.cs','Pages/Dashboard.cs','Models/userData.cs','Models/GenResponse.cs',
    'utils/DialogSteps.cs','utils/BackIdTypes.cs','Pages/components/DialogNavigationBar.cs','Pages/components/progressBar.cs')
$hashes = foreach ($name in $sourceNames) {
    [pscustomobject]@{File=$name;Sha256=(Get-FileHash -LiteralPath (Join-Path $SourceRoot $name) -Algorithm SHA256).Hash}
}
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDirectory 'source-hashes.json') -Encoding utf8
Write-Output ("Inventoried {0} requests and {1} report templates. No credential/body values copied." -f $requests.Count,@($reports).Count)
