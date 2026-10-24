# Engine launch circuit breaker. Keep this check before fixture writes/builds.
if (Test-Path -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) '.engine-execution-blocked')) {
    throw 'Engine execution is blocked after repeated security dialogs. Do not retry or remove the block until the dialog cause is resolved.'
}
