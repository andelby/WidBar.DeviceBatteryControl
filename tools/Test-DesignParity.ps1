param([string]$ReferenceRoot = (Join-Path $PSScriptRoot '..\..\WidBar.StockWatcher'))
$ErrorActionPreference = 'Stop'
$viewRoot = Join-Path $PSScriptRoot '../src/Widget/Pages'
$referenceViews = Join-Path $ReferenceRoot 'WidBar.StockWatcher.ExtensionApp\Views'
function Read-Xaml([string]$Path) {
    $document = [System.Xml.XmlDocument]::new()
    $document.Load((Resolve-Path -LiteralPath $Path))
    return ,$document
}
function Canonical([System.Xml.XmlNode]$Node) {
    $attributes = @($Node.Attributes | Where-Object { $_.Prefix -ne 'xmlns' -and $_.Name -ne 'xmlns' } |
        Sort-Object Name | ForEach-Object { $_.Name + '=' + $_.Value }) -join ';'
    $children = @($Node.ChildNodes | Where-Object NodeType -EQ Element | ForEach-Object { Canonical $_ }) -join ''
    return '<' + $Node.LocalName + ':' + $attributes + '>' + $children + '</' + $Node.LocalName + '>'
}
function Compare-Node($Reference, $Actual, [string]$Description) {
    if ($null -eq $Reference -or $null -eq $Actual -or (Canonical $Reference) -ne (Canonical $Actual)) {
        throw "Design parity failed: $Description"
    }
    Write-Output "PASS $Description"
}
$reference = Read-Xaml (Join-Path $referenceViews 'StockFlyoutView.xaml')
$actual = Read-Xaml (Join-Path $viewRoot 'BatteryFlyoutPage.xaml')
foreach ($key in @('HeaderButtonStyle','SectionCardStyle','SectionHeadingStyle','SecondaryCaptionStyle','PillStyle','ActivePillStyle')) {
    $selector = "//*[local-name()='Style' and @*[local-name()='Key' and .='$key']]"
    Compare-Node ($reference.SelectSingleNode($selector)) ($actual.SelectSingleNode($selector)) $key
}
Compare-Node ($reference.SelectSingleNode("//*[local-name()='ListView.ItemContainerStyle']")) `
    ($actual.SelectSingleNode("//*[local-name()='ListView.ItemContainerStyle']")) 'List container / native hover'
Compare-Node ($reference.SelectSingleNode("//*[local-name()='ListView.ItemContainerTransitions']")) `
    ($actual.SelectSingleNode("//*[local-name()='ListView.ItemContainerTransitions']")) 'List transitions'
$referenceSettings = Read-Xaml (Join-Path $referenceViews 'StockSettingsView.xaml')
$actualSettings = Read-Xaml (Join-Path $viewRoot 'BatterySettingsPage.xaml')
foreach ($key in @('CardExpanderStyle','SectionHeaderStyle','CardComboStyle')) {
    $selector = "//*[local-name()='Style' and @*[local-name()='Key' and .='$key']]"
    Compare-Node ($referenceSettings.SelectSingleNode($selector)) ($actualSettings.SelectSingleNode($selector)) $key
}
$referenceCard = Read-Xaml (Join-Path $referenceViews 'SettingsCard.xaml')
$actualCard = Read-Xaml (Join-Path $PSScriptRoot '../src/Widget/Controls/SettingsCard.xaml')
Compare-Node ($referenceCard.SelectSingleNode("/*/*[local-name()='Border']")) `
    ($actualCard.SelectSingleNode("/*/*[local-name()='Border']")) 'SettingsCard layout'
$referenceRow = Read-Xaml (Join-Path $referenceViews 'WatchlistRow.xaml')
$actualRow = Read-Xaml (Join-Path $PSScriptRoot '../src/Widget/Controls/BatteryDeviceRow.xaml')
$referenceGrid = $referenceRow.SelectSingleNode("/*/*[local-name()='Grid']")
$actualGrid = $actualRow.SelectSingleNode("/*/*[local-name()='Grid']")
foreach ($property in @('Height','Padding','ColumnSpacing')) {
    if ($referenceGrid.GetAttribute($property) -ne $actualGrid.GetAttribute($property)) { throw "Row $property differs" }
}
Compare-Node ($referenceGrid.SelectSingleNode("*[local-name()='Grid.ColumnDefinitions']")) `
    ($actualGrid.SelectSingleNode("*[local-name()='Grid.ColumnDefinitions']")) 'Row size, padding and columns'
Write-Output 'Design parity checks passed.'
