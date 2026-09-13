--!strict
-- init.server.luau
-- ================
-- Sunucunun TEK giris noktasi. Kurulum sirasi burada, baska yerde degil.
--
-- Lobi mi klinik mi?
-- ------------------
-- Oyun TEK yer dosyasi. Ayrim `game.PrivateServerId` ile yapiliyor:
--
--     PrivateServerId == ""  ->  LOBI    (herkese acik sunucu)
--     PrivateServerId ~= ""  ->  KLINIK  (ayrilmis sunucu)
--
-- Boylece iki ayri place yayinlamaya ve PlaceId yapistirmaya gerek yok;
-- oyun yayinlandigi anda lobiden klinige isinlanma calisir.
--
-- Studio'da isinlanma yok: Matchmaking basarisiz olunca `startClinic`
-- AYNI sunucuda cagriliyor. Bu yedek istege bagli degil ZORUNLU, yoksa
-- dosya Studio'da acildiginda lobiden ileri gidilemezdi.
--
-- Oyuncu, dunya kurulmadan dogmaz
-- -------------------------------
-- Yer dosyasinda `Players.CharacterAutoLoads` FALSE geliyor
-- (tools/rbxlx/tree.py). Dunya kurulduktan sonra burada true'ya
-- cekiliyor. Kurulum HATA VERSE BILE bu bayrak aciliyor: yoksa oyuncu
-- karaktersiz kalir ve ekranda hicbir sey olmaz.

local Players = game:GetService("Players")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Loc = require(Shared.Loc)
local Net = require(Shared.Net)
local Validate = require(Shared.Validate)

local AnimalAnimator = require(script.world.AnimalAnimator)
local Clinic = require(script.world.Clinic)
local Lobby = require(script.world.Lobby)
local OwnerFactory = require(script.world.OwnerFactory)
local SkyLighting = require(script.world.Lighting)

local Leaderboard = require(script.game.Leaderboard)
local Matchmaking = require(script.game.Matchmaking)
local PatientFlow = require(script.game.PatientFlow)
local Profile = require(script.game.Profile)
local Surgery = require(script.game.Surgery)
local Tasks = require(script.game.Tasks)
local Upgrades = require(script.game.Upgrades)

local settingsData = require(Data.settings)

-- ── Remote baglama ────────────────────────────────────────────────────

--- Bir remote'u dogrulama katmanindan gecirerek baglar.
--- Tip denetimi Net.Schema'daki semadan okunuyor; her baglama yerinde
--- elle yazmak, bir gun birinde unutulmasi demekti.
local function bind(name: string, handler: (player: Player, ...any) -> ())
	local schema = Net.Schema[name]
	assert(schema ~= nil, "bind: tanimsiz remote: " .. name)
	assert(schema.direction == "toServer", "bind: " .. name .. " istemciden gelmiyor")

	Net.get(name).OnServerEvent:Connect(function(player: Player, ...)
		if not Validate.rateOk(player, name) then
			return
		end

		local count = select("#", ...)
		if count ~= #schema.args then
			return
		end

		local args = table.pack(...)
		for index, kind in schema.args do
			local value = args[index]
			if kind == "string" then
				if not Validate.id(value) then
					return
				end
			elseif kind == "number" then
				if not Validate.number(value) then
					return
				end
			elseif kind == "table" then
				if type(value) ~= "table" then
					return
				end
			end
		end

		-- Oyun kurallari (para, seviye, yakinlik) ilgili modulde;
		-- burada yalnizca bicim dogrulandi.
		local ok, err = pcall(handler, player, ...)
		if not ok then
			warn(("[Net] %s isleyicisi hata verdi: %s"):format(name, tostring(err)))
		end
	end)
end

Net.createAll()
SkyLighting.apply()

-- ── Mod secimi ────────────────────────────────────────────────────────

local isReserved = game.PrivateServerId ~= ""
local mode: "lobby" | "clinic" = if isReserved then "clinic" else "lobby"
local clinicBuilt: Clinic.Built? = nil
local lobbyBuilt: Lobby.Built? = nil

local function pushLobby(player: Player)
	-- Klinik ayni sunucuda basladiysa (Studio yedegi) eslestirme dongusu
	-- hala donuyor olabilir; lobi durumu yollamak istemciyi lobi
	-- ekranina geri atardi.
	if mode ~= "lobby" then
		return
	end
	local snapshot = Matchmaking.snapshot()
	local mine = Matchmaking.modeOf(player)
	snapshot.mine = mine
	if mine ~= nil then
		for _, entry in snapshot.modes do
			if entry.id == mine then
				snapshot.mineNameKey = entry.nameKey
			end
		end
	end
	Net.toPlayer("LobbyState", player, snapshot)
end

local function pushLobbyAll()
	if mode ~= "lobby" then
		return
	end
	for _, player in Players:GetPlayers() do
		pushLobby(player)
	end
	-- Portal tabelalarindaki canli sayac
	if lobbyBuilt ~= nil then
		local snapshot = Matchmaking.snapshot()
		for _, entry in snapshot.modes do
			local portal = lobbyBuilt.portals[entry.id]
			if portal ~= nil then
				portal.counter.Text = ("%d / %d"):format(entry.waiting, entry.capacity)
			end
		end
	end
end

--- Klinigi kurar. Hem ayrilmis sunucuda hem de isinlanma basarisiz
--- oldugunda (Studio) cagriliyor.
local function startClinic()
	if clinicBuilt ~= nil then
		return
	end
	if lobbyBuilt ~= nil then
		-- Ayni sunucuda klinige geciyoruz: lobi salonu kalkmali.
		lobbyBuilt.model:Destroy()
		lobbyBuilt = nil
	end
	mode = "clinic"

	local ok, err = pcall(function()
		clinicBuilt = Clinic.build(workspace)
		AnimalAnimator.start()
		OwnerFactory.start()
		PatientFlow.start(clinicBuilt :: Clinic.Built)
	end)
	if not ok then
		warn("[init] KLINIK KURULAMADI: " .. tostring(err))
		warn("[init] Oyuncular yine dogacak; Output'taki hatayi kontrol edin.")
		return
	end

	-- Istasyonlarda "Sirayi cagir" istemi: klavye kisayolunu bilmeyen
	-- oyuncu da oyunu baslatabilsin.
	for stationId, station in (clinicBuilt :: Clinic.Built).stations do
		local anchor = station.model.PrimaryPart
		if anchor == nil then
			continue
		end
		local prompt = Instance.new("ProximityPrompt")
		prompt.Name = "SirayiCagir"
		prompt.ActionText = Loc.get("prompt.callNext")
		prompt.ObjectText = Loc.get("prompt.station." .. stationId)
		prompt.HoldDuration = 0
		prompt.MaxActivationDistance = 12
		prompt.RequiresLineOfSight = false
		prompt.Parent = anchor
		prompt.Triggered:Connect(function(player: Player)
			PatientFlow.callNext(player)
		end)
	end

	for _, player in Players:GetPlayers() do
		if player.Character ~= nil then
			player:LoadCharacter()
		end
		PatientFlow.pushState(player)
	end
	print("[Veteriner] KLINIK hazir.")
end

local function startLobby()
	local ok, err = pcall(function()
		lobbyBuilt = Lobby.build(workspace)
	end)
	if not ok then
		warn("[init] LOBI KURULAMADI: " .. tostring(err))
		return
	end

	for _, portal in (lobbyBuilt :: Lobby.Built).portals do
		local id = portal.id
		portal.prompt.Triggered:Connect(function(player: Player)
			Matchmaking.join(player, id)
			pushLobbyAll()
		end)
	end

	Matchmaking.start(pushLobbyAll, function(group: { Player }, modeId: string)
		-- Isinlanma yapilamadi (Studio / yayinlanmamis yer): klinigi
		-- burada ac ve gruba haber ver.
		for _, player in group do
			Net.toPlayer("Notify", player, "warn", "notify.teleportFallback", {})
		end
		startClinic()
	end)

	print("[Veteriner] LOBI hazir — mod secilince ayrilmis sunucuya isinlanilacak.")
end

Profile.startAutosave()

if mode == "clinic" then
	startClinic()
else
	startLobby()
end

-- ── Oyuncu yasam dongusu ──────────────────────────────────────────────

local function onPlayerAdded(player: Player)
	local entry = Profile.load(player)
	Leaderboard.attach(player)
	Tasks.ensure(entry.data, player.UserId)

	task.defer(function()
		if mode == "clinic" then
			PatientFlow.pushState(player)
		else
			pushLobby(player)
		end
	end)
end

Players.PlayerAdded:Connect(onPlayerAdded)
Players.PlayerRemoving:Connect(function(player: Player)
	PatientFlow.playerLeft(player)
	Surgery.cancel(player)
	Matchmaking.leave(player)
	Validate.forget(player)
end)

for _, player in Players:GetPlayers() do
	onPlayerAdded(player)
end

-- ── Remote'lar ────────────────────────────────────────────────────────

bind("CallNext", function(player: Player)
	PatientFlow.callNext(player)
end)
bind("SelectTool", function(_player: Player, _toolId: string)
	-- Alet secimi tamamen gorsel; sunucunun durumu yok. Sema iki tarafta
	-- da kayitli olsun diye isleyici bos duruyor.
end)
bind("UseTool", function(player: Player, toolId: string)
	PatientFlow.useTool(player, toolId)
end)
bind("SubmitDiagnosis", function(player: Player, conditionId: string)
	PatientFlow.submitDiagnosis(player, conditionId)
end)
bind("ApplyTreatment", function(player: Player, treatmentId: string)
	PatientFlow.applyTreatment(player, treatmentId)
end)
bind("SurgeryInput", function(player: Player, step: number)
	PatientFlow.surgeryInput(player, step)
end)
bind("Discharge", function(player: Player)
	PatientFlow.discharge(player)
end)
bind("BuyTool", function(player: Player, toolId: string)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local result = Upgrades.buyTool(entry.data, toolId)
	Net.toPlayer("Notify", player, if result.ok then "good" else "warn", result.reasonKey, result.args)
	if result.ok then
		Profile.markDirty(player)
	end
	PatientFlow.pushState(player)
end)
bind("BuyUpgrade", function(player: Player, upgradeId: string)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local result = Upgrades.buyUpgrade(entry.data, upgradeId)
	Net.toPlayer("Notify", player, if result.ok then "good" else "warn", result.reasonKey, result.args)
	if result.ok then
		Profile.markDirty(player)
	end
	PatientFlow.pushState(player)
end)

--- Ayarlar SUNUCUDA siniriyor: istemciden gelen degere guvenilmiyor.
--- Bozuk bir deger (NaN, negatif FOV) kamerayi kilitleyebilirdi.
local function sanitizeSettings(raw: any, current: any): any
	local limits = settingsData.limits
	local result = table.clone(current)

	if type(raw.showFps) == "boolean" then
		result.showFps = raw.showFps
	end
	if type(raw.sprintEnabled) == "boolean" then
		result.sprintEnabled = raw.sprintEnabled
	end
	if type(raw.bobbing) == "number" and raw.bobbing == raw.bobbing then
		result.bobbing = math.clamp(raw.bobbing, limits.bobbing.min, limits.bobbing.max)
	end
	if type(raw.fieldOfView) == "number" and raw.fieldOfView == raw.fieldOfView then
		result.fieldOfView = math.clamp(raw.fieldOfView, limits.fieldOfView.min, limits.fieldOfView.max)
	end
	for _, key in { "masterVolume", "musicVolume", "sfxVolume" } do
		local value = raw[key]
		-- `value == value` NaN'i eliyor: NaN her karsilastirmada false.
		if type(value) == "number" and value == value then
			result[key] = math.clamp(value, limits[key].min, limits[key].max)
		end
	end
	return result
end

bind("SaveSettings", function(player: Player, raw: any)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	entry.data.settings = sanitizeSettings(raw, entry.data.settings)
	Profile.markDirty(player)
	if mode == "clinic" then
		PatientFlow.pushState(player)
	else
		pushLobby(player)
	end
end)

bind("RequestProgress", function(player: Player)
	PatientFlow.pushProgress(player)
end)

bind("JoinQueue", function(player: Player, modeId: string)
	if mode ~= "lobby" then
		return
	end
	Matchmaking.join(player, modeId)
	pushLobbyAll()
end)

bind("LeaveQueue", function(player: Player)
	if mode ~= "lobby" then
		return
	end
	Matchmaking.leave(player)
	pushLobbyAll()
end)

-- Her sey bagli: artik oyuncular dogabilir.
Players.CharacterAutoLoads = true
for _, player in Players:GetPlayers() do
	if player.Character == nil then
		player:LoadCharacter()
	end
end

print(("[Veteriner] sunucu hazir — mod: %s"):format(mode))
