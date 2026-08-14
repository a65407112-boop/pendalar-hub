-- File: PENDALAR_HUB_STANDALONE.lua

local UI_LIBRARY_URL =
	"https://raw.githubusercontent.com/shidemuri/scripts/main/newuilib.lua"

local SCRIPTS = {
	{
		Name = "Neko Hub 😏",
		Description = "Launch Neko Hub",
		Url = "https://raw.githubusercontent.com/a65407112-boop/Neko-Script/main/loader.lua",
	},
}

local COLOR_REPLACEMENTS = {
	{
		From = "Color3.fromRGB(38, 45, 71)",
		To = "Color3.fromRGB(166, 58, 99)",
	},
	{
		From = "Color3.fromRGB(26, 32, 58)",
		To = "Color3.fromRGB(137, 43, 79)",
	},
	{
		From = "Color3.fromRGB(26,32,58)",
		To = "Color3.fromRGB(137,43,79)",
	},
	{
		From = "Color3.fromRGB(69, 69, 107)",
		To = "Color3.fromRGB(194, 73, 115)",
	},
	{
		From = "Color3.fromRGB(103, 103, 158)",
		To = "Color3.fromRGB(219, 110, 149)",
	},
	{
		From = "Color3.fromRGB(100, 100, 156)",
		To = "Color3.fromRGB(208, 97, 137)",
	},
	{
		From = "Color3.fromRGB(53, 53, 82)",
		To = "Color3.fromRGB(126, 38, 70)",
	},
	{
		From = "Color3.fromRGB(102, 61, 255)",
		To = "Color3.fromRGB(255, 110, 173)",
	},
	{
		From = "Color3.fromRGB(70, 70, 224)",
		To = "Color3.fromRGB(221, 107, 145)",
	},
}

local function normalizeEntry(entry, index)
	if type(entry) ~= "table" then
		return {
			Name = "Script " .. tostring(index),
			Description = "Run script",
			Url = "",
		}
	end

	return {
		Name = tostring(
			entry.Name
			or entry.name
			or entry[1]
			or ("Script " .. tostring(index))
		),
		Description = tostring(
			entry.Description
			or entry.description
			or entry.Desc
			or entry.desc
			or "Run script"
		),
		Url =
			entry.Url
			or entry.URL
			or entry.url
			or entry[2]
			or "",
	}
end

local function fetch(url)
	local ok, body = pcall(function()
		return game:HttpGet(url)
	end)

	if ok and type(body) == "string" and body ~= "" then
		return body
	end

	return nil, tostring(body or "HTTP request failed")
end

local function replacePlain(source, from, to)
	local parts = {}
	local startIndex = 1
	local replacements = 0

	while true do
		local first, last = string.find(
			source,
			from,
			startIndex,
			true
		)

		if not first then
			table.insert(parts, string.sub(source, startIndex))
			break
		end

		table.insert(
			parts,
			string.sub(source, startIndex, first - 1)
		)
		table.insert(parts, to)

		replacements = replacements + 1
		startIndex = last + 1
	end

	return table.concat(parts), replacements
end

local function recolorLibrary(source)
	local totalReplacements = 0

	for _, replacement in ipairs(COLOR_REPLACEMENTS) do
		local count

		source, count = replacePlain(
			source,
			replacement.From,
			replacement.To
		)

		totalReplacements = totalReplacements + count
	end

	if totalReplacements == 0 then
		error(
			"[Pendalar Hub] Recolor failed: "
				.. "no Pendulum colors were found.",
			0
		)
	end

	return source, totalReplacements
end

local function runScript(entry, index)
	local scriptEntry = normalizeEntry(entry, index)

	if scriptEntry.Url == "" then
		warn(
			"[Pendalar Hub] Missing URL for "
				.. scriptEntry.Name
		)
		return
	end

	local source, problem = fetch(scriptEntry.Url)

	if not source then
		warn(
			"[Pendalar Hub] Failed to download "
				.. scriptEntry.Name
				.. ": "
				.. tostring(problem)
		)
		return
	end

	if type(loadstring) ~= "function" then
		warn("[Pendalar Hub] loadstring() is unavailable")
		return
	end

	local chunk, compileProblem = loadstring(
		source,
		"=" .. scriptEntry.Name
	)

	if not chunk then
		warn(
			"[Pendalar Hub] Compile error in "
				.. scriptEntry.Name
				.. ": "
				.. tostring(compileProblem)
		)
		return
	end

	local ok, runtimeProblem = pcall(chunk)

	if not ok then
		warn(
			"[Pendalar Hub] Runtime error in "
				.. scriptEntry.Name
				.. ": "
				.. tostring(runtimeProblem)
		)
	end
end

local librarySource, libraryProblem = fetch(UI_LIBRARY_URL)

if not librarySource then
	error(
		"[Pendalar Hub] Failed to load Pendulum UI library: "
			.. tostring(libraryProblem),
		0
	)
end

local recolorCount
librarySource, recolorCount = recolorLibrary(librarySource)

print(
	"[Pendalar Hub] Applied "
		.. tostring(recolorCount)
		.. " UI color replacements."
)

local libraryChunk, libraryCompileProblem = loadstring(
	librarySource,
	"=PendalarPinkPendulumUI"
)

if not libraryChunk then
	error(
		"[Pendalar Hub] UI library compile error: "
			.. tostring(libraryCompileProblem),
		0
	)
end

local Library = libraryChunk()

if type(Library) ~= "table"
	or type(Library.New) ~= "function"
then
	error(
		"[Pendalar Hub] Invalid Pendulum UI library.",
		0
	)
end

local Pendalar = Library:New("Pendalar Hub")

local ScriptsTab = Pendalar:NewTab("Scripts")
local CreditsTab = Pendalar:NewTab("Credits")

for index, entry in ipairs(SCRIPTS) do
	local scriptEntry = normalizeEntry(entry, index)

	ScriptsTab:NewButton(
		scriptEntry.Name,
		scriptEntry.Description,
		function()
			runScript(entry, index)
		end
	)
end

CreditsTab:NewLabel("Alpha Sigma Male")
CreditsTab:NewLabel("Larry")

Pendalar:SetMainTab(ScriptsTab)
Pendalar:SetFooter("Current version: V1")
