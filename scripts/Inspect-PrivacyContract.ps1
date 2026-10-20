param(
    [Parameter(Mandatory = $true)][string]$CollectionPath,
    [string]$OutputPath = ''
)
$ErrorActionPreference = 'Stop'
if (-not $OutputPath) { $OutputPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/api-integration/endpoint-inventory.json' }
try { $collection = Get-Content -LiteralPath $CollectionPath -Raw | ConvertFrom-Json -AsHashtable }
catch { throw 'Cannot read the collection. Check the file path and JSON format; no source values were printed.' }
if ($collection['info']['name'] -ne 'OBS - Privacy PII OTP & Dashboard') { throw 'This inventory generator is scoped to the selected privacy collection.' }

# Never export body values, header values, variable values, URL hosts/query values,
# concrete record identifiers, full descriptions, or executable collection scripts.
function Variables-In([string]$text) {
    @([regex]::Matches($text, '\{\{([^}]+)\}\}') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}
function Body-Shape($value) {
    if ($null -eq $value) { return @{ Kind = 'Null' } }
    if ($value -is [System.Collections.IDictionary]) {
        $fields = [ordered]@{}
        foreach ($key in $value.Keys) { $fields[$key] = Body-Shape $value[$key] }
        return @{ Kind = 'Object'; Fields = $fields }
    }
    if ($value -is [array]) { return @{ Kind = 'Array'; Items = @($value | ForEach-Object { Body-Shape $_ }) } }
    if ($value -is [string]) { return @{ Kind = 'String'; VariableReferences = @(Variables-In $value) } }
    if ($value -is [bool]) { return @{ Kind = 'Boolean' } }
    return @{ Kind = 'Number' }
}
function Mapping([string]$name) {
    switch ($name) {
        'Authenticate (request login OTP)' { return @('Already supported', 'Staff login: request email OTP', 'SourceAuthenticationGateway.AuthenticateAsync', 'Existing login UI; selected-deployment acceptance pending') }
        'Authenticate (submit login OTP)' { return @('Already supported', 'Staff login: verify OTP', 'SourceAuthenticationGateway.AuthenticateAsync', 'Body and exact refresh/XSRF cookie names supplied by user; synthetic session parse passes; renewal and full UI acceptance pending') }
        'Terminate (logout)' { return @('Implemented', 'Staff logout', 'SourceAuthenticationGateway.TerminateAsync', 'Bodyless Privacy variant; replacement login mode remains guarded pending session compatibility; parallel screen retains existing logout') }
        'Request privacy OTP' { return @('Implemented', 'Applicant reveal: request OTP', 'PrivacyApiService.RequestOtpAsync', 'Wired in both parallel screens through shared state/controller; explicit request only; synthetic-tested; live acceptance pending') }
        'Verify privacy OTP' { return @('Implemented', 'Applicant reveal: verify OTP', 'PrivacyApiService.VerifyOtpAsync; PiiAccessToken.FromVerification', 'Wired in approved dialogs; cookie then separate status; screen/session/applicant lifetime synthetic-tested; live acceptance pending') }
        'Verification status' { return @('Implemented', 'Applicant verification validity', 'PrivacyApiService.GetStatusAsync; GetVerificationStatusAsync; PrivacyStatusReply.FromApiResult', 'Wired before reveal and after staff token rotation; expiry/cancellation clear screen; live enforcement pending') }
        'Get Bio Details' { return @('Implemented', 'Applicant bio card', 'PrivacyApiService.GetBioAsync; GetBioSnapshotAsync', 'Wired in both parallel screens using entered citizen ID; exact claim/expiry validation; live acceptance pending') }
        'Get Address' { return @('Implemented', 'Applicant address fields', 'PrivacyApiService.GetAddressAsync; GetAddressSnapshotAsync', 'Wired with ordered address rows and independent hide; exact citizen ID/claim/expiry validation; live acceptance pending') }
        'Get Emergency Contact' { return @('Implemented', 'Applicant emergency card', 'PrivacyApiService.GetEmergencyAsync; GetEmergencySnapshotAsync', 'Wired with nullable suffix and ordered rows; exact citizen ID/claim/expiry validation; live acceptance pending') }
        'Get Educational Information' { return @('Implemented', 'Applicant education card', 'PrivacyApiService.GetEducationAsync; GetEducationSnapshotAsync', 'Wired with observed other-info fields; exact citizen ID/claim/expiry validation; live acceptance pending') }
        'Information Gathering - specific applicant' { return @('Implemented', 'Information Gathering detail', 'PrivacyApiService.GetInformationGatheringAsync; GetInformationGatheringSnapshotAsync', 'Observed authorized detail mapped; server flags intersect status claims; canonical lookup and screen workflow pending') }
        'Cardholder Update - specific applicant' { return @('Implemented', 'Cardholder Update detail', 'PrivacyApiService.GetCardholderUpdateAsync; GetCardholderUpdateSnapshotAsync', 'Observed redacted detail mapped; withheld differs from authorized empty arrays; canonical lookup and screen workflow pending') }
        'Get Dashboard' { return @('Implemented', 'Dashboard overview', 'PrivacyApiService.GetDashboardAsync; GetDashboardSnapshotAsync; DashboardReply.FromApiResult', 'User-supplied response fields mapped and mock-tested; screen binding and period semantics pending; current cards remain sample data') }
        'Legacy - Request OTP' { return @('Deferred', 'Alternate personal-info OTP facade', '', 'Collection explicitly prefers modern privacy OTP; no existing caller found') }
        'Legacy - Resend OTP' { return @('Deferred', 'Alternate personal-info OTP facade', '', 'No confirmed legacy caller; do not invent modern resend behavior or automatic fallback') }
        'Legacy - Verify OTP' { return @('Deferred', 'Alternate personal-info OTP facade', '', 'No confirmed legacy caller; literal credential-bearing query omitted from inventory') }
        default { throw 'Unclassified request: review inventory generator before exporting.' }
    }
}
$requests = [Collections.Generic.List[object]]::new()
function Inspect-Items($items, [string]$folder, [string]$inheritedAuth) {
    foreach ($item in $items) {
        if ($item.Contains('item')) { Inspect-Items $item['item'] ($folder + '/' + $item['name']) $inheritedAuth }
        if (-not $item.Contains('request')) { continue }
        $request = $item['request']
        $mapping = Mapping $item['name']
        $auth = $inheritedAuth
        if ($request.Contains('auth')) { $auth = $request['auth']['type'] }
        $segments = @($request['url']['path'] | ForEach-Object {
            if ($_ -match '^\{\{[A-Za-z][A-Za-z0-9]*\}\}$' -or $_ -match '^[A-Za-z][A-Za-z-]*$' -or $_ -eq 'v1') { $_ } else { '{identifier}' }
        })
        $requestBody = if ($request.Contains('body')) { $request['body'] } else { @{} }
        $rawBody = $requestBody['raw']
        $body = $null
        if ($rawBody) {
            try { $body = Body-Shape ($rawBody | ConvertFrom-Json -AsHashtable) }
            catch { throw 'A request body is not valid JSON. Source values were not exported.' }
        }
        $scripts = @($item['event'] | Where-Object { $null -ne $_ })
        $scriptText = ($scripts | ForEach-Object { $_['script']['exec'] -join "`n" }) -join "`n"
        $responseStatuses = @([regex]::Matches($scriptText, '\.status\((\d{3})\)') | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique)
        $queryParameters = [ordered]@{}
        foreach ($query in @($request['url']['query'] | Where-Object { $null -ne $_ })) {
            $queryParameters[$query['key']] = @{ Name = $query['key']; Disabled = [bool]$query['disabled']; ValueOmitted = $true; Source = 'Structured URL query' }
        }
        # Some exports only retain query data in url.raw. Inspect it without exporting values.
        $rawUrl = [string]$request['url']['raw']
        if ($rawUrl.Contains('?')) {
            $rawQuery = ($rawUrl.Split('?', 2)[1]).Split('#', 2)[0]
            foreach ($pair in $rawQuery.Split('&', [StringSplitOptions]::RemoveEmptyEntries)) {
                $key = [Uri]::UnescapeDataString($pair.Split('=', 2)[0])
                if (-not $queryParameters.Contains($key)) { $queryParameters[$key] = @{ Name = $key; Disabled = $false; ValueOmitted = $true; Source = 'Raw URL query' } }
            }
        }
        $requests.Add([ordered]@{
            Id = ('P{0:d2}' -f ($requests.Count + 1)); Folder = $folder.TrimStart('/'); Name = $item['name']
            Method = $request['method']; Path = '/' + ($segments -join '/'); BaseVariable = 'baseUrl'
            Authentication = $auth
            Headers = @($request['header'] | Where-Object { $null -ne $_ } | ForEach-Object {
                @{ Name = $_['key']; Disabled = [bool]$_['disabled']; VariableReferences = @(Variables-In $_['value']); LiteralValueOmitted = !([string]$_['value'] -match '\{\{') }
            })
            QueryParameters = @($queryParameters.Values)
            PathVariables = @(Variables-In ($segments -join '/'))
            BodyMode = $requestBody['mode']; BodyShape = $body
            FieldRequiredness = 'No formal required/optional/nullable schema; collection fields and descriptions are evidence only'
            ResponseExamples = @($item['response'] | Where-Object { $null -ne $_ }).Count
            ScriptHttpAssertions = $responseStatuses; ScriptEvents = @($scripts | ForEach-Object { $_['listen'] })
            ScriptLogsRawResponse = [bool]($scriptText -match 'console\.log\(json\)')
            CoverageStatus = $mapping[0]; Feature = $mapping[1]; Implementation = $mapping[2]; WorkflowAndBlocker = $mapping[3]
            SourceFile = if ($mapping[2] -like '*SourceAuthentication*') { 'src/OBS.Core/SourceAuthenticationGateway.cs' } elseif ($mapping[2]) { 'src/OBS.Core/PrivacyApi.cs' } else { $null }
            Verification = if ($mapping[0] -eq 'Deferred') { 'Not tested' } else { 'Mock-tested' }
            LiveVerification = 'Not tested'; Evidence = $(if ($item['name'] -eq 'Get Dashboard') { 'Selected collection, user-supplied preproduction response shape and synthetic mapping tests; dashboard HTTP status not reported' }
                elseif ($item['name'] -in @('Verify privacy OTP', 'Verification status')) { 'Collection plus user-reported HTTP 200/business 0 and exact PII cookie/status metadata; synthetic tests; no assistant-authenticated verification' }
                elseif ($item['name'] -in @('Get Bio Details','Get Address','Get Emergency Contact','Get Educational Information','Information Gathering - specific applicant','Cardholder Update - specific applicant')) { 'Collection plus user-supplied preproduction body structure and synthetic typed mapping tests; HTTP statuses not supplied; no assistant-authenticated verification' }
                elseif ($item['name'] -eq 'Authenticate (submit login OTP)') { 'Collection plus user-reported successful login body and both exact cookie names; synthetic complete-session mapping; renewal unverified' }
                else { 'Selected collection descriptions/scripts and local source; no assistant-authenticated API verification' })
        })
    }
}
Inspect-Items $collection['item'] '' $collection['auth']['type']
if ($requests.Count -ne 16) { throw 'Selected contract request count changed. Review classifications before exporting.' }
$variables = @($collection['variable'] | ForEach-Object {
    $key = $_['key']
    @{ Name = $key; Classification = $(if ($key -in @('password','accessToken','loginOtp','otp','piiAccessToken')) { 'Secret' }
        elseif ($key -in @('username','applicationId','citizenId')) { 'Personal identifier' } else { 'Configuration' }); ValueOmitted = $true }
})
$responseExampleCount = 0
foreach ($requestRecord in $requests) { $responseExampleCount += [int]$requestRecord['ResponseExamples'] }
$inventory = [ordered]@{
    Scope = 'Owner-selected 16-request Privacy PII OTP & Dashboard contract'
    CollectionSha256 = (Get-FileHash -LiteralPath $CollectionPath -Algorithm SHA256).Hash
    RequestCount = $requests.Count; SavedResponseExamples = $responseExampleCount; CollectionAuthentication = $collection['auth']['type']
    Variables = $variables; RequestFieldsAndScriptsAreNotLiveEvidence = $true
    Requests = $requests
}
$directory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$inventory | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output 'Inventoried 16 requests. Body, credential, record, header, URL host and query values were omitted.'
