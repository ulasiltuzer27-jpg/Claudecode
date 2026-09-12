--!strict
-- init.server.luau
-- ================
-- Sunucunun TEK giris noktasi. Kurulum sirasi burada, baska yerde degil.
--
-- Oyuncu, dunya kurulmadan dogmaz
-- -------------------------------
-- Yer dosyasinda `Players.CharacterAutoLoads` FALSE geliyor
-- (tools/rbxlx/tree.py). Klinik kurulduktan sonra burada true'ya
-- cekiliyor. Aksi halde oyuncu zemin olusmadan dogup bosluga duserdi.
-- Kurulum HATA VERSE BILE bu bayrak aciliyor: yoksa oyuncu karaktersiz
-- kalir ve ekranda hicbir sey olmaz - hatayi gormek yerine "oyun
-- bozuk" izlenimi edinir.

local Players = game:GetService("Players")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")

local Loc = require(Shared.Loc)
local Net = require(Shared.Net)
local Validate = require(Shared.Validate)

local AnimalAnimator = require(script.world.AnimalAnimator)
local Clinic = require(script.world.Clinic)
local SkyLighting = require(script.world.Lighting)

local PatientFlow = require(script.game.PatientFlow)
local Profile = require(script.game.Profile)
local Surgery = require(script.game.Surgery)

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

-- ── Kurulum ───────────────────────────────────────────────────────────

Net.createAll()

local built: Clinic.Built? = nil

local setupOk, setupError = pcall(function()
	SkyLighting.apply()
	built = Clinic.build(workspace)
	AnimalAnimator.start()
	PatientFlow.start(built :: Clinic.Built)
end)

if not setupOk then
	warn("[init] KLINIK KURULAMADI: " .. tostring(setupError))
	warn("[init] Oyuncular yine dogacak; Output'taki hatayi kontrol edin.")
end

Profile.startAutosave()

-- Istasyonlarda "Sirayi cagir" istemi: klavye kisayolunu bilmeyen
-- oyuncu da oyunu baslatabilsin.
if built ~= nil then
	for stationId, station in (built :: Clinic.Built).stations do
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
end

-- ── Oyuncu yasam dongusu ──────────────────────────────────────────────

local function onPlayerAdded(player: Player)
	Profile.load(player)
	-- Istemci modullerinin kurulmasini beklemeye gerek yok: istemci
	-- hazir olunca kendi durumunu istiyor (CallNext degil, ilk
	-- StateUpdate asagida yollaniyor ve istemci StateUpdate'i
	-- baglandigi anda dinlemeye basliyor).
	task.defer(function()
		PatientFlow.pushState(player)
	end)
end

Players.PlayerAdded:Connect(onPlayerAdded)
Players.PlayerRemoving:Connect(function(player: Player)
	PatientFlow.playerLeft(player)
	Surgery.cancel(player)
	Validate.forget(player)
end)

-- Oyun kurulmadan katilmis olabilecek oyuncular (Studio'da Play'e
-- basildiginda oyuncu sunucu betiginden ONCE var olabiliyor).
for _, player in Players:GetPlayers() do
	onPlayerAdded(player)
end

bind("CallNext", function(player: Player)
	PatientFlow.callNext(player)
end)
bind("SelectTool", function(_player: Player, _toolId: string)
	-- Alet secimi tamamen gorsel; sunucunun durumu yok. Yine de remote
	-- tanimli: istemci alet degistirdiginde sunucunun bunu bilmesi
	-- gerekmiyor, bu yuzden isleyici bos - ama sema iki tarafta da
	-- kayitli olsun diye duruyor.
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
	local result = require(script.game.Upgrades).buyTool(entry.data, toolId)
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
	local result = require(script.game.Upgrades).buyUpgrade(entry.data, upgradeId)
	Net.toPlayer("Notify", player, if result.ok then "good" else "warn", result.reasonKey, result.args)
	if result.ok then
		Profile.markDirty(player)
	end
	PatientFlow.pushState(player)
end)

-- Her sey bagli: artik oyuncular dogabilir.
Players.CharacterAutoLoads = true
for _, player in Players:GetPlayers() do
	if player.Character == nil then
		player:LoadCharacter()
	end
end

print("[Veteriner] sunucu hazir — klinik kuruldu, remote'lar bagli.")
