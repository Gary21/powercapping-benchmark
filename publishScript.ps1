$FirstFolder = "D:\RiderProjects\powercapping-benchmark\powercapping-benchmark\server"
$FirstCommand = "dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true"
$SecondFolder = "D:\RiderProjects\powercapping-benchmark\powercapping-benchmark\client"
$SecondCommand = "dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true"

if (-not $FirstFolder -or -not $FirstCommand -or -not $SecondFolder -or -not $SecondCommand) {
	throw 'Uzupełnij $FirstFolder, $FirstCommand, $SecondFolder i $SecondCommand w skrypcie.'
}

Push-Location $FirstFolder
try {
	Invoke-Expression $FirstCommand
}
finally {
	Pop-Location
}

Push-Location $SecondFolder
try {
	Invoke-Expression $SecondCommand
}
finally {
	Pop-Location
}
