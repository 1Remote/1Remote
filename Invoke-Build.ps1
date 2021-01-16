<# Invoke-Build 5.6.3
Copyright (c) Roman Kuzmin

Licensed under the Apache License, Version 2.0 (the "License"); you may not use
this file except in compliance with the License. You may obtain a copy of the
License at http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed
under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
CONDITIONS OF ANY KIND, either express or implied. See the License for the
specific language governing permissions and limitations under the License.
#>

#.ExternalHelp InvokeBuild-Help.xml
param(
	[Parameter(Position=0)][string[]]$Task,
	[Parameter(Position=1)]$File,
	$Result,
	[switch]$Safe,
	[switch]$Summary,
	[switch]$WhatIf
)

dynamicparam {

function *Path($P) {
	$PSCmdlet.GetUnresolvedProviderPathFromPSPath($P)
}

function *Die($M, $C=0) {
	$PSCmdlet.ThrowTerminatingError((New-Object System.Management.Automation.ErrorRecord ([Exception]"$M"), $null, $C, $null))
}

function Get-BuildFile($Path) {
	do {
		if (($f = [System.IO.Directory]::GetFiles($Path, '*.build.ps1')).Length -eq 1) {return $f}
		if ($f) {return $($f | Sort-Object)[0]}
		if (($c = $env:InvokeBuildGetFile) -and ($f = & $c $Path)) {return $f}
	} while($Path = Split-Path $Path)
}

if ($MyInvocation.InvocationName -eq '.') {return}
trap {*Die $_ 5}

New-Variable * -Description IB ([PSCustomObject]@{
	All = [System.Collections.Specialized.OrderedDictionary]([System.StringComparer]::OrdinalIgnoreCase)
	Tasks = [System.Collections.Generic.List[object]]@()
	Errors = [System.Collections.Generic.List[object]]@()
	Warnings = [System.Collections.Generic.List[object]]@()
	Redefined = @()
	Doubles = @()
	Started = [DateTime]::Now
	Elapsed = $null
	Error = 'Invalid arguments.'
	Task = $null
	File = $BuildFile = $PSBoundParameters['File']
	Safe = $PSBoundParameters['Safe']
	Summary = $PSBoundParameters['Summary']
	CD = *Path
	DP = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
	SP = @{}
	P = $(if ($_ = $PSCmdlet.SessionState.PSVariable.Get('*')) {if ($_.Description -eq 'IB') {$_.Value}})
	A = 1
	B = 0
	Q = 0
	H = @{}
	EnterBuild = $null
	ExitBuild = $null
	EnterTask = $null
	ExitTask = $null
	EnterJob = $null
	ExitJob = $null
	Header = {Write-Build 11 "Task $($args[0])"}
	Footer = {Write-Build 11 "Done $($args[0]) $($Task.Elapsed)"}
	Data = @{}
	XBuild = $null
	XCheck = $null
})
if ($_ = $PSBoundParameters['Result']) {
	if ($_ -is [string]) {
		New-Variable $_ ${*} -Scope 1 -Force
	}
	elseif ($_ -is [hashtable]) {
		${*}.XBuild = $_['XBuild']
		${*}.XCheck = $_['XCheck']
		$_.Value = ${*}
	}
	else {throw 'Invalid parameter Result.'}
}
$BuildTask = $PSBoundParameters['Task']
if ($BuildFile -is [scriptblock]) {
	$BuildFile = $BuildFile.File
	return
}
if ($BuildTask -eq '**') {
	if (![System.IO.Directory]::Exists(($_ = *Path $BuildFile))) {throw "Missing directory '$_'."}
	$BuildFile = @(Get-ChildItem -LiteralPath $_ -Filter *.test.ps1 -Recurse -Force)
	return
}

if ($BuildFile) {
	if (![System.IO.File]::Exists(($BuildFile = *Path $BuildFile))) {throw "Missing script '$BuildFile'."}
}
elseif (!($BuildFile = Get-BuildFile ${*}.CD)) {
	throw 'Missing default script.'
}
${*}.File = $BuildFile

if (!($_ = (Get-Command $BuildFile -ErrorAction 1).Parameters)) {
	& $BuildFile
	throw 'Invalid script.'
}
if ($_.get_Count()) {&{
	$c = 'Verbose', 'Debug', 'ErrorAction', 'WarningAction', 'ErrorVariable', 'WarningVariable', 'OutVariable', 'OutBuffer', 'PipelineVariable', 'InformationAction', 'InformationVariable'
	$r = 'Task', 'File', 'Result', 'Safe', 'Summary', 'WhatIf'
	foreach($p in $_.get_Values()) {
		if ($c -notcontains ($_ = $p.Name)) {
			if ($r -contains $_) {throw "Script uses reserved parameter '$_'."}
			${*}.DP.Add($_, (New-Object System.Management.Automation.RuntimeDefinedParameter $_, $p.ParameterType, $p.Attributes))
		}
	}
	${*}.DP
}}

} end {

#.ExternalHelp InvokeBuild-Help.xml
function Add-BuildTask(
	[Parameter(Position=0, Mandatory=1)][string]$Name,
	[Parameter(Position=1)]$Jobs,
	[string[]]$After,
	[string[]]$Before,
	$If=-9,
	$Inputs,
	$Outputs,
	$Data,
	$Done,
	$Source=$MyInvocation,
	[switch]$Partial
)
{
	trap {*Die "Task '$Name': $_" 5}
	if (${*}.A -eq 0) {throw 'Cannot add tasks.'}
	if ($Jobs -is [hashtable]) {
		if ($PSBoundParameters.get_Count() -ne 2) {throw 'Invalid parameters.'}
		Add-BuildTask $Name @Jobs -Source:$Source
		return
	}
	if ($Name[0] -eq '?') {throw 'Invalid task name.'}
	if ($_ = ${*}.All[$Name]) {${*}.Redefined += $_}
	${*}.All[$Name] = [PSCustomObject]@{
		Name = $Name
		Error = $null
		Started = $null
		Elapsed = $null
		Jobs = $1 = [System.Collections.Generic.List[object]]@()
		After = $After
		Before = $Before
		If = $If
		Inputs = $Inputs
		Outputs = $Outputs
		Data = $Data
		Done = $Done
		Partial = $Partial
		InvocationInfo = $Source
	}
	if (!$Jobs) {return}
	$1.AddRange(@($Jobs))
	$2 = @()
	foreach($j in $1) {
		$r, $null = *Job $j
		if ($2 -contains $r) {${*}.Doubles += ,($Name, $r)}
		$2 += $r
	}
}

#.ExternalHelp InvokeBuild-Help.xml
function Assert-Build([Parameter()]$Condition, [string]$Message) {
	if (!$Condition) {
		*Die "Assertion failed.$(if ($Message) {" $Message"})" 7
	}
}

#.ExternalHelp InvokeBuild-Help.xml
function Assert-BuildEquals([Parameter()]$A, $B) {
	if (![Object]::Equals($A, $B)) {
		*Die @"
Objects are not equal:
A:$(if ($null -ne $A) {" $A [$($A.GetType())]"})
B:$(if ($null -ne $B) {" $B [$($B.GetType())]"})
"@ 7
	}
}

#.ExternalHelp InvokeBuild-Help.xml
function Get-BuildError([Parameter(Mandatory=1)][string]$Task) {
	if (!($_ = ${*}.All[$Task])) {
		*Die "Missing task '$Task'." 5
	}
	$_.Error
}

#.ExternalHelp InvokeBuild-Help.xml
function Get-BuildProperty([Parameter(Mandatory=1)][string]$Name, $Value) {
	${*n} = $Name
	${*v} = $Value
	Remove-Variable Name, Value
	if (($null -ne ($_ = $PSCmdlet.GetVariableValue(${*n})) -and '' -ne $_) -or ($_ = [Environment]::GetEnvironmentVariable(${*n}))) {return $_}
	if ($null -eq ${*v}) {*Die "Missing property '${*n}'." 13}
	${*v}
}

#.ExternalHelp InvokeBuild-Help.xml
function Get-BuildSynopsis([Parameter(Mandatory=1)]$Task, $Hash=${*}.H) {
	$f = ($I = $Task.InvocationInfo).ScriptName
	if (!($d = $Hash[$f])) {
		$Hash[$f] = $d = @{T = Get-Content -LiteralPath $f; C = @{}}
		foreach($_ in [System.Management.Automation.PSParser]::Tokenize($d.T, [ref]$null)) {
			if ($_.Type -eq 15) {$d.C[$_.EndLine] = $_.Content}
		}
	}
	for($n = $I.ScriptLineNumber; --$n -ge 1) {
		if ($c = $d.C[$n]) {if ($c -match '(?m)^\s*(?:#*\s*Synopsis\s*:|\.Synopsis\s*^)(.*)') {return $Matches[1].Trim()}}
		elseif ($d.T[$n - 1].Trim()) {break}
	}
}

#.ExternalHelp InvokeBuild-Help.xml
function Invoke-BuildExec([Parameter(Mandatory=1)][scriptblock]$Command, [int[]]$ExitCode=0) {
	${private:*c} = $Command
	${private:*x} = $ExitCode
	Remove-Variable Command, ExitCode
	& ${*c}
	if (${*x} -notcontains $global:LastExitCode) {
		*Die "Command {${*c}} exited with code $global:LastExitCode." 8
	}
}

#.ExternalHelp InvokeBuild-Help.xml
function Remove-BuildItem([Parameter(Mandatory=1)][string[]]$Path) {
	if ($Path -match '^[.*/\\]*$') {*Die 'Not allowed paths.' 5}
	$v = $PSBoundParameters['Verbose']
	try {
		foreach($_ in $Path) {
			if (Get-Item $_ -Force -ErrorAction 0) {
				if ($v) {Write-Verbose "remove: removing $_" -Verbose}
				Remove-Item $_ -Force -Recurse
			}
			elseif ($v) {Write-Verbose "remove: skipping $_" -Verbose}
		}
	}
	catch {
		*Die $_
	}
}

#.ExternalHelp InvokeBuild-Help.xml
function Test-BuildAsset([Parameter(Position=0)][string[]]$Variable, [string[]]$Environment, [string[]]$Property) {
	Remove-Variable Variable, Environment, Property
	if ($_ = $PSBoundParameters['Variable']) {foreach($_ in $_) {
		if ($null -eq ($$ = $PSCmdlet.GetVariableValue($_)) -or '' -eq $$) {*Die "Missing variable '$_'." 13}
	}}
	if ($_ = $PSBoundParameters['Environment']) {foreach($_ in $_) {
		if (!([Environment]::GetEnvironmentVariable($_))) {*Die "Missing environment variable '$_'." 13}
	}}
	if ($_ = $PSBoundParameters['Property']) {foreach($_ in $_) {
		if ('' -eq (Get-BuildProperty $_ '')) {*Die "Missing property '$_'." 13}
	}}
}

#.ExternalHelp InvokeBuild-Help.xml
function Use-BuildAlias([Parameter(Mandatory=1)][string]$Path, [string[]]$Name) {
	trap {*Die $_ 5}
	$d = switch -regex ($Path) {
		'^\*|^\d+\.' {Split-Path (Resolve-MSBuild $_)}
		^Framework {"$env:windir\Microsoft.NET\$_"}
		default {*Path $_}
	}
	if (![System.IO.Directory]::Exists($d)) {throw "Cannot resolve '$Path'."}
	foreach($_ in $Name) {
		Set-Alias $_ (Join-Path $d $_) -Scope 1
	}
}

#.ExternalHelp InvokeBuild-Help.xml
function Set-BuildFooter([Parameter()][scriptblock]$Script) {${*}.Footer = $Script}

#.ExternalHelp InvokeBuild-Help.xml
function Set-BuildHeader([Parameter()][scriptblock]$Script) {${*}.Header = $Script}

#.ExternalHelp InvokeBuild-Help.xml
function Write-Build([ConsoleColor]$Color, [string]$Text) {
	$i = $Host.UI.RawUI
	$_ = $i.ForegroundColor
	try {
		$i.ForegroundColor = $Color
		$Text
	}
	finally {
		$i.ForegroundColor = $_
	}
}
if ($env:APPVEYOR) {
	function Write-Build([ConsoleColor]$Color, [string]$Text) {Write-Host $Text -ForegroundColor $Color}
}
else {
	try {$null = Write-Build 0} catch {function Write-Build($Color, [string]$Text) {$Text}}
}

function *My {
	$_.InvocationInfo.ScriptName -eq $MyInvocation.ScriptName
}

function *SL($P=$BuildRoot) {
	Set-Location -LiteralPath $P -ErrorAction 1
}

function *Fin([Parameter()]$M, $C=0) {
	*Die $M $C
}

function *Run($_) {
	if ($_) {
		*SL
		. $_ @args
	}
}

function *At($I) {
	$I.InvocationInfo.PositionMessage.Trim()
}

function *Msg($M, $I) {
@"
$M
$(*At $I)
"@
}

function *Job($J) {
	if ($J -is [string]) {if ($J[0] -eq '?') {$J.Substring(1), 1} else {$J}}
	elseif ($J -is [scriptblock]) {$J}
	else {*Fin 'Invalid job.' 5}
}

function *Unsafe($N, $J) {
	if ($J -contains $N) {return 1}
	foreach($_ in $J) {
		$r, $null = *Job $_
		if ($r -ne $N -and ($t = ${*}.All[$r]) -and $t.If -and (*Unsafe $N $t.Jobs)) {
			return 1
		}
	}
}

function *Amend($X, $J, $B) {
	$n = $X.Name
	foreach($_ in $J) {
		$r, $s = *Job $_
		if (!($t = ${*}.All[$r])) {*Fin (*Msg "Task '$n': Missing task '$r'." $X) 5}
		$j = $t.Jobs
		$i = $j.Count
		if ($B) {
			for($k = -1; ++$k -lt $i -and $j[$k] -is [string]) {}
			$i = $k
		}
		$j.Insert($i, $(if ($s) {"?$n"} else {$n}))
	}
}

function *Check($J, $T, $P=@()) {
	foreach($_ in $J) { if ($_ -is [string]) {
		$_, $null = *Job $_
		if (!($r = ${*}.All[$_])) {
			$_ = "Missing task '$_'."
			*Fin $(if ($T) {*Msg "Task '$($T.Name)': $_" $T} else {$_}) 5
		}
		if ($P -contains $r) {
			*Fin (*Msg "Task '$($T.Name)': Cyclic reference to '$_'." $T) 5
		}
		*Check $r.Jobs $r ($P + $r)
	}}
}

filter *Help {
	$r = 1 | Select-Object Name, Jobs, Synopsis
	$r.Name = $_.Name
	$r.Jobs = foreach($j in $_.Jobs) {if ($j -is [string]) {$j} else {'{}'}}
	$r.Synopsis = Get-BuildSynopsis $_
	$r
}

function *Root($A) {
	*Check $A.get_Keys()
	$h = @{}
	foreach($_ in $A.get_Values()) {foreach($_ in $_.Jobs) {
		if ($_ -is [string]) {
			$_, $null = *Job $_
			$h[$_] = 1
		}
	}}
	foreach($_ in $A.get_Keys()) {if (!$h[$_]) {$_}}
}

function *Err($T) {
	${*}.Errors.Add([PSCustomObject]@{Error = $_; File = $BuildFile; Task = $T})
	Write-Build 12 "ERROR: $(if (*My) {$_} else {*Msg $_ $_})"
	if ($T) {$T.Error = $_}
}

function *IO {
	if ((${private:*i} = $Task.Inputs) -is [scriptblock]) {
		*SL
		${*i} = @(& ${*i})
	}
	*SL
	${private:*p} = [System.Collections.Generic.List[object]]@()
	${*i} = foreach($_ in ${*i}) {
		if ($_ -isnot [System.IO.FileInfo]) {$_ = [System.IO.FileInfo](*Path $_)}
		if (!$_.Exists) {*Fin "Missing input '$_'." 13}
		${*p}.Add($_.FullName)
		$_
	}
	if (!${*p}) {return 2, 'Skipping empty input.'}

	${private:*o} = $Task.Outputs
	if ($Task.Partial) {
		${*o} = @(
			if (${*o} -is [scriptblock]) {
				${*p} | & ${*o}
				*SL
			}
			else {
				${*o}
			}
		)
		if (${*p}.Count -ne ${*o}.Count) {*Fin "Different Inputs/Outputs counts: $(${*p}.Count)/$(${*o}.Count)." 6}

		$k = -1
		$Task.Inputs = $i = [System.Collections.Generic.List[object]]@()
		$Task.Outputs = $o = [System.Collections.Generic.List[object]]@()
		foreach($_ in ${*i}) {
			$f = *Path ($p = ${*o}[++$k])
			if (![System.IO.File]::Exists($f) -or $_.LastWriteTime -gt [System.IO.File]::GetLastWriteTime($f)) {
				$i.Add(${*p}[$k])
				$o.Add($p)
			}
		}
		if ($i) {return $null, "Out-of-date outputs: $($o.Count)/$(${*p}.Count)."}
	}
	else {
		if (${*o} -is [scriptblock]) {
			$Task.Outputs = ${*o} = ${*p} | & ${*o}
			*SL
		}
		if (!${*o}) {*Fin 'Outputs must not be empty.' 5}

		$Task.Inputs = ${*p}
		$m = (${*i} | .{process{$_.LastWriteTime.Ticks}} | Measure-Object -Maximum).Maximum
		foreach($_ in ${*o}) {
			$p = *Path $_
			if (![System.IO.File]::Exists($p)) {return $null, "Missing output '$_'."}
			if ($m -gt [System.IO.File]::GetLastWriteTime($p).Ticks) {return $null, "Out-of-date output '$_'."}
		}
	}
	2, 'Skipping up-to-date output.'
}

function *Task {
	${private:*p} = "$($args[1])/$($args[0])"
	${private:*n}, ${private:*s} = *Job $args[0]
	New-Variable Task (${*}.Task = ${*}.All[${*n}]) -Option Constant

	if ($Task.Elapsed) {
		Write-Build 8 "Done ${*p}"
		return
	}

	$Task.Started = [DateTime]::Now
	if ((${private:*x} = $Task.If) -is [scriptblock]) {
		*SL
		try {
			${*x} = & ${*x}
		}
		catch {
			*Err $Task
			${*}.Tasks.Add($Task)
			Write-Build 14 (*At $Task)
			$Task.Elapsed = [TimeSpan]::Zero
			throw
		}
	}
	if (!${*x}) {
		Write-Build 8 "Task ${*p} skipped."
		return
	}

	${private:*i} = , [int]($null -ne $Task.Inputs)
	try {
		. *Run ${*}.EnterTask
		foreach(${private:*j} in $Task.Jobs) {
			if (${*j} -is [string]) {
				try {
					*Task ${*j} ${*p}
				}
				finally {
					${*}.Task = $Task
				}
				continue
			}
			& ${*}.Header ${*p}

			if (1 -eq ${*i}[0]) {
				try {
					${*i} = *IO
				}
				catch {
					*Err $Task
					throw
				}
				Write-Build 8 ${*i}[1]
			}
			if (${*i}[0]) {
				continue
			}

			try {
				. *Run ${*}.EnterJob
				*SL
				if (0 -eq ${*i}[0]) {
					& ${*j}
				}
				else {
					$Inputs = $Task.Inputs
					$Outputs = $Task.Outputs
					if ($Task.Partial) {
						${*x} = 0
						$Inputs | .{process{
							$2 = $Outputs[${*x}++]
							$_
						}} | & ${*j}
					}
					else {
						$Inputs | & ${*j}
					}
				}
			}
			catch {
				*Err $Task
				throw
			}
			finally {
				. *Run ${*}.ExitJob
			}
		}
	}
	catch {
		$Task.Error = $_
		if (!${*s} -or (*Unsafe ${*n} $BuildTask)) {throw}
	}
	finally {
		$Task.Elapsed = [DateTime]::Now - $Task.Started
		${*}.Tasks.Add($Task)
		if ($Task.Error) {
			Write-Build 14 (*At $Task)
		}
		else {
			if (${*}.XCheck) {& ${*}.XCheck}
			& ${*}.Footer ${*p}
		}
		*Run $Task.Done
		. *Run ${*}.ExitTask
	}
}

Set-Alias assert Assert-Build
Set-Alias equals Assert-BuildEquals
Set-Alias error Get-BuildError
Set-Alias exec Invoke-BuildExec
Set-Alias property Get-BuildProperty
Set-Alias remove Remove-BuildItem
Set-Alias requires Test-BuildAsset
Set-Alias task Add-BuildTask
Set-Alias use Use-BuildAlias
Set-Alias Invoke-Build ($_ = $MyInvocation.MyCommand.Path)
$_ = Split-Path $_
Set-Alias Show-TaskHelp (Join-Path $_ Show-TaskHelp.ps1)
Set-Alias Build-Parallel (Join-Path $_ Build-Parallel.ps1)
Set-Alias Resolve-MSBuild (Join-Path $_ Resolve-MSBuild.ps1)

if ($MyInvocation.InvocationName -eq '.') {
	Remove-Variable Task, File, Result, Safe, Summary, WhatIf
	return
}

function Write-Warning([Parameter()]$Message) {
	$PSCmdlet.WriteWarning($Message)
	${*}.Warnings.Add([PSCustomObject]@{Message = $Message; File = $BuildFile; Task = ${*}.Task; InvocationInfo=$MyInvocation})
}

$ErrorActionPreference = 'Stop'
foreach($_ in $PSBoundParameters.get_Keys()) {
	if (${*}.DP.ContainsKey($_)) {
		${*}.SP[$_] = $PSBoundParameters[$_]
	}
}
if (${*}.Q = $BuildTask -eq '?' -or $BuildTask -eq '??') {
	$WhatIf = $true
}
Remove-Variable Task, File, Result, Safe, Summary

${*}.Error = $null
try {
	if ($BuildTask -eq '**') {
		${*}.A = 0
		foreach($_ in $BuildFile) {
			Invoke-Build * $_.FullName -Safe:${*}.Safe
		}
		${*}.B = 1
		exit
	}

	function Enter-Build([Parameter()][scriptblock]$Script) {${*}.EnterBuild = $Script}
	function Exit-Build([Parameter()][scriptblock]$Script) {${*}.ExitBuild = $Script}
	function Enter-BuildTask([Parameter()][scriptblock]$Script) {${*}.EnterTask = $Script}
	function Exit-BuildTask([Parameter()][scriptblock]$Script) {${*}.ExitTask = $Script}
	function Enter-BuildJob([Parameter()][scriptblock]$Script) {${*}.EnterJob = $Script}
	function Exit-BuildJob([Parameter()][scriptblock]$Script) {${*}.ExitJob = $Script}
	function Set-BuildData([Parameter()]$Key, $Value) {${*}.Data[$Key] = $Value}

	*SL ($BuildRoot = if ($BuildFile) {Split-Path $BuildFile} else {${*}.CD})
	$_ = ${*}.SP
	${private:**} = @(. ${*}.File @_)
	foreach($_ in ${**}) {
		Write-Warning "Unexpected output: $_."
		if ($_ -is [scriptblock]) {*Fin "Dangling scriptblock at $($_.File):$($_.StartPosition.StartLine)" 6}
	}
	if (!(${**} = ${*}.All).get_Count()) {*Fin "No tasks in '$BuildFile'." 6}

	foreach($_ in ${**}.get_Values()) {
		if ($_.Before) {*Amend $_ $_.Before 1}
	}
	foreach($_ in ${**}.get_Values()) {
		if ($_.After) {*Amend $_ $_.After}
	}

	if (${*}.Q) {
		*Check ${**}.get_Keys()
		if ($BuildTask -eq '?') {
			${**}.get_Values() | *Help
		}
		else {
			${**}
		}
		exit
	}

	if ($BuildTask -eq '*') {
		$BuildTask = *Root ${**}
	}
	else {
		if (!$BuildTask -or '.' -eq $BuildTask) {
			$BuildTask = if (${**}['.']) {'.'} else {${**}.Item(0).Name}
		}
		*Check $BuildTask
	}
	if ($WhatIf) {
		Show-TaskHelp
		exit
	}

	New-Variable BuildRoot (*Path $BuildRoot) -Option Constant -Force
	if (![System.IO.Directory]::Exists($BuildRoot)) {*Fin "Missing build root '$BuildRoot'." 13}

	Write-Build 11 "Build $($BuildTask -join ', ') $BuildFile"
	foreach($_ in ${*}.Redefined) {
		Write-Build 8 "Redefined task '$($_.Name)'."
	}
	foreach($_ in ${*}.Doubles) {
		if (${*}.All[$_[1]].If -isnot [scriptblock]) {
			Write-Warning "Task '$($_[0])' always skips '$($_[1])'."
		}
	}

	${*}.A = 0
	try {
		. *Run ${*}.EnterBuild
		if (${*}.XBuild) {. ${*}.XBuild}
		if (${*}.XCheck) {& ${*}.XCheck}
		foreach($_ in $BuildTask) {
			*Task $_ ''
		}
	}
	finally {
		${*}.Task = $null
		. *Run ${*}.ExitBuild
	}
	${*}.B = 1
	exit
}
catch {
	${*}.B = 2
	${*}.Error = $_
	if (!${*}.Errors) {*Err}
	if ($_.FullyQualifiedErrorId -eq 'PositionalParameterNotFound,Add-BuildTask') {
		Write-Warning 'Check task parameters: Name and comma separated Jobs.'
	}
	if (${*}.Safe) {
		exit
	}
	elseif (*My) {
		$PSCmdlet.ThrowTerminatingError($_)
	}
	throw
}
finally {
	*SL ${*}.CD
	if (${*}.B -and !${*}.Q) {
		$t = ${*}.Tasks
		$e = ${*}.Errors
		if (${*}.Summary) {
			Write-Build 11 'Build summary:'
			foreach($_ in $t) {
				'{0,-16} {1} - {2}:{3}' -f $_.Elapsed, $_.Name, $_.InvocationInfo.ScriptName, $_.InvocationInfo.ScriptLineNumber
				if ($_ = $_.Error) {
					Write-Build 12 "ERROR: $(if (*My) {$_} else {*Msg $_ $_})"
				}
			}
		}
		if ($w = ${*}.Warnings) {
			foreach($_ in $w) {
				"WARNING: $(if ($_.Task) {"/$($_.Task.Name) "})$($_.InvocationInfo.ScriptName):$($_.InvocationInfo.ScriptLineNumber)"
				Write-Build 14 $_.Message
			}
		}
		if ($_ = ${*}.P) {
			$_.Tasks.AddRange($t)
			$_.Errors.AddRange($e)
			$_.Warnings.AddRange($w)
		}
		$c, $m = if (${*}.A) {12, "Build ABORTED $BuildFile"}
		elseif (${*}.B -eq 2) {12, 'Build FAILED'}
		elseif ($e) {14, 'Build completed with errors'}
		elseif ($w) {14, 'Build succeeded with warnings'}
		else {10, 'Build succeeded'}
		Write-Build $c "$m. $($t.Count) tasks, $($e.Count) errors, $($w.Count) warnings $((${*}.Elapsed = [DateTime]::Now - ${*}.Started))"
	}
}
}
