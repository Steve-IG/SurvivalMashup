# After Coplay MCP tools that mutate the Unity editor, drop a sentinel file the editor polls and
# flushes via SceneAutoSave. Keeps the main thread free of "save modified scene?" modals.
$ErrorActionPreference = 'SilentlyContinue'

$stdin = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($stdin)) {
    exit 0
}

try {
    $payload = $stdin | ConvertFrom-Json
} catch {
    exit 0
}

$server = $payload.server
if ([string]::IsNullOrWhiteSpace($server)) {
    $server = $payload.mcpServer
}

if ($server -ne 'user-coplay-mcp') {
    exit 0
}

$tool = $payload.tool_name
if ([string]::IsNullOrWhiteSpace($tool)) {
    $tool = $payload.toolName
}

$saveTools = @(
    'duplicate_game_object',
    'set_property',
    'set_transform',
    'rename_game_object',
    'add_component',
    'remove_component',
    'create_game_object',
    'delete_game_object',
    'parent_game_object',
    'place_asset_in_scene',
    'set_tag',
    'set_layer',
    'create_prefab',
    'create_prefab_variant',
    'add_nested_object_to_prefab',
    'save_scene',
    'open_scene',
    'play_game',
    'stop_game',
    'execute_script'
)

if ($saveTools -notcontains $tool) {
    exit 0
}

# Repo layout: <repo>/.cursor/hooks/... and <repo>/SurvivalMashup/ is the Unity project root.
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sentinelDir = Join-Path $repoRoot 'SurvivalMashup\.cursor'
$sentinel = Join-Path $sentinelDir 'unity-save-requested'

New-Item -ItemType Directory -Force -Path $sentinelDir | Out-Null
New-Item -ItemType File -Force -Path $sentinel | Out-Null
exit 0
