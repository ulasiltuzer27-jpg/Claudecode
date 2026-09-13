--!strict
-- PatientFlow.luau
-- ================
-- Hastanin butun yasam dongusu: gelis, kuyruk, masaya alinma, teshis,
-- tedavi, taburcu. SUNUCU OTORITER - istemci yalnizca niyet bildirir.
--
-- Hastaligin kimligi sunucuda kalir
-- ---------------------------------
-- `patient.conditionId` istemciye ANCAK dogru teshisten sonra gidiyor.
-- Oncesinde istemci yalnizca BULUNMUS semptomlari goruyor.
--
-- Kuyruk paylasilir, odul taburcu edene
-- -------------------------------------
-- Klinik tek; kuyruk butun oyuncularin ortak kuyrugu. Bir hastayi
-- masaya alan oyuncu onun "sahibi" olur ve odulu o alir. Boylece cok
-- oyunculu oturumda iki veteriner ayni hastayi cekistirmez.

local Players = game:GetService("Players")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Audio = require(Shared.Audio)
local Net = require(Shared.Net)

local AnimalAnimator = require(script.Parent.Parent.world.AnimalAnimator)
local AnimalFactory = require(script.Parent.Parent.world.AnimalFactory)
local Clinic = require(script.Parent.Parent.world.Clinic)
local OwnerFactory = require(script.Parent.Parent.world.OwnerFactory)
local SkyLighting = require(script.Parent.Parent.world.Lighting)
local Achievements = require(script.Parent.Achievements)
local Diagnosis = require(script.Parent.Diagnosis)
local Economy = require(script.Parent.Economy)
local Leaderboard = require(script.Parent.Leaderboard)
local Profile = require(script.Parent.Profile)
local Surgery = require(script.Parent.Surgery)
local Tasks = require(script.Parent.Tasks)
local Treatment = require(script.Parent.Treatment)
local Upgrades = require(script.Parent.Upgrades)

local economy = require(Data.economy)

local PatientFlow = {}

-- Oyuncunun bir istasyonda islem yapabilmesi icin gereken yakinlik.
-- Sunucuda denetleniyor: istemcinin "yanindayim" demesine guvenilmiyor.
local STATION_RADIUS = 16
local VITALS_TICK = 0.25

export type Patient = {
	id: number,
	animalId: string,
	conditionId: string,
	rig: AnimalFactory.Rig,
	health: number,
	stress: number,
	revealed: { [string]: boolean },
	toolsUsed: { [string]: boolean },
	diagnosed: boolean,
	treatmentsDone: { [string]: boolean },
	vet: Player?,
	owner: OwnerFactory.Owner?,
	emergency: boolean,
	satisfaction: number,
	deadline: number,
	stationId: string?,
	state: string,
	arrivedAt: number,
	busyUntil: number,
	queueSlot: number?,
}

local clinic: Clinic.Built? = nil
local patientsFolder: Folder? = nil

local patients: { [number]: Patient } = {}
local activeOf: { [Player]: number } = {}
local occupiedStation: { [string]: number } = {}
local usedQueueSlots: { [number]: boolean } = {}

local nextPatientId = 1
local started = false

-- ── Yardimcilar ───────────────────────────────────────────────────────

local function notify(player: Player, kind: string, messageKey: string, args: { any }?)
	Net.toPlayer("Notify", player, kind, messageKey, args or {})
end

local function notifyAll(kind: string, messageKey: string, args: { any }?)
	for _, player in Players:GetPlayers() do
		notify(player, kind, messageKey, args)
	end
end

local function bestReputation(): number
	local best = economy.reputation.min
	for _, player in Players:GetPlayers() do
		local entry = Profile.get(player)
		if entry ~= nil then
			best = math.max(best, entry.data.reputation)
		end
	end
	return best
end

local function highestLevel(): number
	local best = 1
	for _, player in Players:GetPlayers() do
		local entry = Profile.get(player)
		if entry ~= nil then
			best = math.max(best, entry.data.level)
		end
	end
	return best
end

local function queueSlotCount(): number
	local clinicEffects = Upgrades.clinicEffects(Players:GetPlayers())
	local total = economy.arrival.queueSlots + clinicEffects.queueSlots
	local available = if clinic ~= nil then #clinic.queuePoints else 0
	return math.min(total, available)
end

local function waitingCount(): number
	local total = 0
	for _, patient in patients do
		if patient.state == "waiting" or patient.state == "arriving" then
			total += 1
		end
	end
	return total
end

local function freeQueueSlot(): number?
	local limit = queueSlotCount()
	for slot = 1, limit do
		if usedQueueSlots[slot] ~= true then
			return slot
		end
	end
	return nil
end

--- Oyuncu bu istasyona yeterince yakin mi? Karakteri yoksa hayir.
local function nearStation(player: Player, stationId: string): boolean
	if clinic == nil then
		return false
	end
	local station = clinic.stations[stationId]
	if station == nil then
		return false
	end
	local character = player.Character
	if character == nil then
		return false
	end
	local root = character:FindFirstChild("HumanoidRootPart")
	if root == nil or not root:IsA("BasePart") then
		return false
	end
	local flat = Vector3.new(root.Position.X, 0, root.Position.Z)
	local target = Vector3.new(station.cframe.Position.X, 0, station.cframe.Position.Z)
	return (flat - target).Magnitude <= STATION_RADIUS
end

--- Oyuncuya en yakin istasyon. Hicbiri menzilde degilse nil.
local function nearestStation(player: Player): string?
	if clinic == nil then
		return nil
	end
	local best: string? = nil
	local bestDistance = math.huge
	local character = player.Character
	if character == nil then
		return nil
	end
	local root = character:FindFirstChild("HumanoidRootPart")
	if root == nil or not root:IsA("BasePart") then
		return nil
	end

	for id, station in clinic.stations do
		local flat = Vector3.new(root.Position.X, 0, root.Position.Z)
		local target = Vector3.new(station.cframe.Position.X, 0, station.cframe.Position.Z)
		local distance = (flat - target).Magnitude
		if distance <= STATION_RADIUS and distance < bestDistance then
			best = id
			bestDistance = distance
		end
	end
	return best
end

local function patientOf(player: Player): Patient?
	local id = activeOf[player]
	if id == nil then
		return nil
	end
	return patients[id]
end

-- ── Istemciye yollanan goruntuler ─────────────────────────────────────

local function chartSnapshot(patient: Patient?, viewer: Player): any
	if patient == nil then
		return { present = false }
	end

	local revealed = {}
	for symptomId in patient.revealed do
		local symptom = Diagnosis.symptom(symptomId)
		table.insert(revealed, {
			id = symptomId,
			nameKey = if symptom ~= nil then symptom.nameKey else symptomId,
			severity = if symptom ~= nil then symptom.severity else 1,
		})
	end

	local animal = AnimalFactory.spec(patient.animalId)
	local entry = Profile.get(viewer)
	local level = if entry ~= nil then entry.data.level else 1

	-- Teshis adaylari: bu turun, bu seviyede gorulebilecek hastaliklari.
	local candidates = {}
	for _, condition in Diagnosis.conditionsFor(patient.animalId, level) do
		table.insert(candidates, { id = condition.id, nameKey = condition.nameKey })
	end

	local required = {}
	local done = {}
	if patient.diagnosed then
		for _, treatmentId in Treatment.requiredFor(patient.conditionId) do
			local treatment = Treatment.get(treatmentId)
			table.insert(required, {
				id = treatmentId,
				nameKey = if treatment ~= nil then treatment.nameKey else treatmentId,
				kind = if treatment ~= nil then treatment.kind else "",
				applied = patient.treatmentsDone[treatmentId] == true,
			})
		end
	end
	for treatmentId in patient.treatmentsDone do
		table.insert(done, treatmentId)
	end

	local condition = if patient.diagnosed then Diagnosis.condition(patient.conditionId) else nil
	-- Sahibin sikayeti HER ZAMAN gidiyor: NPC zaten yuksek sesle
	-- soyluyor ve bilerek teshisi ele vermeyen bir cumle. Hastaligin
	-- KIMLIGI yine yalnizca dogru teshisten sonra gidiyor
	-- (conditionNameKey), bu yuzden ayri okunuyor.
	local always = Diagnosis.condition(patient.conditionId)

	return {
		present = true,
		id = patient.id,
		animalId = patient.animalId,
		nameKey = if animal ~= nil then animal.nameKey else patient.animalId,
		health = math.floor(patient.health + 0.5),
		stress = math.floor(patient.stress + 0.5),
		revealed = revealed,
		candidates = candidates,
		diagnosed = patient.diagnosed,
		-- Hastaligin adi ANCAK dogru teshisten sonra gidiyor.
		conditionNameKey = if condition ~= nil then condition.nameKey else nil,
		required = required,
		done = done,
		stationId = patient.stationId,
		complete = patient.diagnosed and Treatment.isComplete(patient.treatmentsDone, patient.conditionId),
		dischargeHealth = economy.patient.dischargeHealth,
		emergency = patient.emergency,
		-- Kalan sure istemcide geri sayim olarak gosteriliyor. Acil
		-- olmayan hastada sonsuz; istemci bunu gizliyor.
		secondsLeft = if patient.emergency then math.max(0, patient.deadline - os.clock()) else -1,
		satisfaction = math.floor(patient.satisfaction + 0.5),
		hintKey = if always ~= nil then always.hintKey else nil,
	}
end

local function pushChart(player: Player)
	Net.toPlayer("ChartUpdate", player, chartSnapshot(patientOf(player), player))
end

local function stateSnapshot(player: Player): any
	local entry = Profile.get(player)
	if entry == nil then
		return { ready = false }
	end
	local data = entry.data

	local tools = {}
	for _, tool in Upgrades.allTools() do
		if data.tools[tool.id] == true and data.level >= tool.unlockLevel then
			table.insert(tools, { id = tool.id, nameKey = tool.nameKey, order = tool.order, useSeconds = tool.useSeconds })
		end
	end
	table.sort(tools, function(a, b)
		return a.order < b.order
	end)

	local effects = Upgrades.effectsOf(data)
	local treatments = {}
	for _, treatment in Treatment.all() do
		if data.level >= treatment.unlockLevel then
			table.insert(treatments, {
				id = treatment.id,
				nameKey = treatment.nameKey,
				kind = treatment.kind,
				order = treatment.order,
				cost = Treatment.cost(treatment.id, effects.supplyDiscount),
			})
		end
	end
	table.sort(treatments, function(a, b)
		return a.order < b.order
	end)

	return {
		ready = true,
		money = data.money,
		xp = data.xp,
		level = data.level,
		maxLevel = Economy.MAX_LEVEL,
		titleKey = Economy.titleKeyForLevel(data.level),
		progress = Economy.levelProgress(data.xp),
		reputation = data.reputation,
		transient = entry.transient,
		stats = data.stats,
		tools = tools,
		treatments = treatments,
		queueWaiting = waitingCount(),
		queueSlots = queueSlotCount(),
		shop = Upgrades.shopSnapshot(data),
		-- Ayarlar profilde saklaniyor; istemci acilista bunlari alip
		-- kamerayi ona gore kuruyor.
		settings = data.settings,
	}
end

local function pushState(player: Player)
	local entry = Profile.get(player)
	if entry ~= nil then
		-- Tab listesi HUD ile ayni anda tazelensin.
		Leaderboard.update(player, entry.data)
	end
	Net.toPlayer("StateUpdate", player, stateSnapshot(player))
end

--- Odullerin XP'si seviye atlatabilir; seviye atlama tek yerde olsun
--- diye ayri fonksiyon. Bedava aletleri de burada aciyor.
local function applyLevel(player: Player, data: any, previousLevel: number)
	data.level = Economy.levelForXp(data.xp)
	if data.level <= previousLevel then
		return
	end
	local granted = Upgrades.grantFreeTools(data)
	notify(player, "good", "notify.levelUp", { data.level, Economy.titleKeyForLevel(data.level) })
	for _, toolId in granted do
		local tool = Upgrades.tool(toolId)
		notify(player, "good", "notify.toolUnlocked", { if tool ~= nil then tool.nameKey else toolId })
	end
end

local function pushProgress(player: Player)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	Tasks.ensure(entry.data, player.UserId)
	Net.toPlayer("ProgressUpdate", player, {
		stats = entry.data.stats,
		achievements = Achievements.snapshot(entry.data),
		tasks = Tasks.snapshot(entry.data),
	})
end

--- Gorev ve basarim denetimini yapar, odulleri verir, bildirir.
--- Sayaclari degistiren HER yol bunun uzerinden gecmeli; aksi halde
--- odul sessizce verilmemis olur.
local function settleProgress(player: Player, data: any)
	Tasks.ensure(data, player.UserId)

	local previousLevel = data.level
	for _, completed in Tasks.check(data) do
		notify(player, "good", "notify.taskDone", { completed.nameKey, completed.money, completed.xp })
	end
	for _, unlocked in Achievements.check(data) do
		notify(player, "good", "notify.achievement", { unlocked.nameKey, unlocked.money, unlocked.xp })
	end
	applyLevel(player, data, previousLevel)

	Profile.markDirty(player)
	pushProgress(player)
end

function PatientFlow.pushProgress(player: Player)
	pushProgress(player)
end

function PatientFlow.pushState(player: Player)
	pushState(player)
	pushChart(player)
	pushProgress(player)
end

local function pushStateAll()
	for _, player in Players:GetPlayers() do
		pushState(player)
	end
end

-- ── Hasta uretimi ─────────────────────────────────────────────────────

--- Seviyeye uygun (tur, hastalik) cifti secer. Hicbir cift yoksa nil.
local function pickCase(level: number): (string?, string?)
	local candidates = {}
	for _, animalId in AnimalFactory.allIds() do
		local animal = AnimalFactory.spec(animalId)
		if animal == nil or animal.unlockLevel > level then
			continue
		end
		for _, condition in Diagnosis.conditionsFor(animalId, level) do
			table.insert(candidates, { animalId, condition.id })
		end
	end
	if #candidates == 0 then
		return nil, nil
	end
	local pick = candidates[math.random(1, #candidates)]
	return pick[1], pick[2]
end

local function updateNameplate(patient: Patient)
	local frame = patient.rig.nameplate:FindFirstChild("Frame")
	if frame == nil then
		return
	end
	local health = frame:FindFirstChild("Saglik")
	local stress = frame:FindFirstChild("Stres")
	if health ~= nil then
		local fill = health:FindFirstChild("Dolgu")
		if fill ~= nil and fill:IsA("Frame") then
			fill.Size = UDim2.fromScale(math.clamp(patient.health / 100, 0, 1), 1)
		end
	end
	if stress ~= nil then
		local fill = stress:FindFirstChild("Dolgu")
		if fill ~= nil and fill:IsA("Frame") then
			fill.Size = UDim2.fromScale(math.clamp(patient.stress / 100, 0, 1), 1)
		end
	end
end

local function spawnPatient()
	if clinic == nil or patientsFolder == nil then
		return
	end

	local slot = freeQueueSlot()
	if slot == nil then
		return -- bekleme yeri dolu
	end

	local animalId, conditionId = pickCase(highestLevel())
	if animalId == nil or conditionId == nil then
		warn("[PatientFlow] bu seviyede uygun hasta bulunamadi - veri tablolarini kontrol edin")
		return
	end

	local spawnAt = CFrame.new(clinic.entranceOutside + Vector3.new(math.random(-6, 6), 0, -14))
	local rig = AnimalFactory.create(animalId, spawnAt, patientsFolder)
	if rig == nil then
		return
	end

	-- Acil vaka: ambulansla gelen agir hasta. minLevel'den once cikmiyor -
	-- yeni oyuncunun ilk dakikalarinda kaldiramayacagi bir sey olmasin.
	local emergency = highestLevel() >= economy.emergency.minLevel
		and math.random() < economy.emergency.chance

	local id = nextPatientId
	nextPatientId += 1

	local health = math.random(economy.patient.startHealthMin, economy.patient.startHealthMax)
	if emergency then
		health = math.max(8, health - economy.emergency.healthPenalty)
	end

	local patient: Patient = {
		id = id,
		animalId = animalId,
		conditionId = conditionId,
		rig = rig,
		health = health,
		stress = math.random(economy.patient.startStressMin, economy.patient.startStressMax),
		revealed = {},
		toolsUsed = {},
		diagnosed = false,
		treatmentsDone = {},
		vet = nil,
		owner = nil,
		emergency = emergency,
		satisfaction = economy.owner.startSatisfaction,
		deadline = if emergency then os.clock() + economy.emergency.timeLimitSeconds else math.huge,
		stationId = nil,
		state = "arriving",
		arrivedAt = os.clock(),
		busyUntil = 0,
		queueSlot = slot,
	}
	patients[id] = patient
	usedQueueSlots[slot] = true

	AnimalAnimator.register(rig)
	AnimalAnimator.setStress(rig, patient.stress)

	-- Hayvanin sahibi: hastaligin `hintKey` ipucunu soyluyor ve
	-- memnuniyeti bahsisi belirliyor.
	local condition = Diagnosis.condition(conditionId)
	local hintKey = if condition ~= nil then condition.hintKey else "hint.generic"
	local owner = OwnerFactory.create(
		CFrame.new(spawnAt.Position + Vector3.new(3.5, 0, -2)),
		hintKey,
		patientsFolder
	)
	patient.owner = owner
	OwnerFactory.register(owner)
	OwnerFactory.updateSatisfaction(owner, patient.satisfaction)

	if emergency then
		-- Acil vaka gorsel olarak bagirsin: kirmizi donen isik.
		local lamp = SkyLighting.lamp("emergency")
		local beacon = Instance.new("PointLight")
		beacon.Name = "AcilIsigi"
		beacon.Color = lamp.color
		beacon.Brightness = lamp.brightness
		beacon.Range = lamp.range
		beacon.Parent = rig.root
		Audio.ambience("emergencySiren", rig.root)
		OwnerFactory.say(owner, "hint.emergency")
	end

	-- Topallayan hastalik gozle belli olsun: oyuncu daha aleti eline
	-- almadan "bacakta bir sey var" diyebilmeli.
	if condition ~= nil then
		for _, symptomId in condition.symptoms do
			if symptomId == "limp" then
				AnimalAnimator.setLimp(rig, math.random(1, 4), 0.8)
				break
			end
		end
	end

	local queuePoint = clinic.queuePoints[slot]
	AnimalAnimator.walkPath(rig, {
		clinic.entranceOutside,
		clinic.entranceInside,
		queuePoint.Position,
	}, function()
		if patients[id] ~= nil and patients[id].state == "arriving" then
			patients[id].state = "waiting"
		end
	end)

	-- Sahip hayvanin yaninda yuruyor; bekleme noktasinin biraz saginda
	-- duruyor ki ikisi ust uste binmesin.
	local ownerSpot = queuePoint.Position + Vector3.new(3, 0, 1.5)
	OwnerFactory.walkPath(owner, {
		clinic.entranceOutside + Vector3.new(3, 0, 0),
		clinic.entranceInside + Vector3.new(3, 0, 0),
		ownerSpot,
	}, function()
		local current = patients[id]
		if current ~= nil and current.owner ~= nil then
			OwnerFactory.faceTowards(current.owner, current.rig.root.Position)
		end
	end)

	updateNameplate(patient)
	local animal = AnimalFactory.spec(animalId)
	local animalKey = if animal ~= nil then animal.nameKey else animalId
	notifyAll(
		if emergency then "bad" else "info",
		if emergency then "notify.emergencyArrived" else "notify.patientArrived",
		{ animalKey }
	)
	pushStateAll()
end

local function releasePatient(patient: Patient)
	if patient.queueSlot ~= nil then
		usedQueueSlots[patient.queueSlot] = nil
	end
	if patient.stationId ~= nil then
		occupiedStation[patient.stationId] = nil
	end
	local vet = patient.vet
	if vet ~= nil and activeOf[vet] == patient.id then
		activeOf[vet] = nil
		Surgery.cancel(vet)
	end
	patients[patient.id] = nil

	AnimalAnimator.unregister(patient.rig)
	patient.rig.model:Destroy()

	if patient.owner ~= nil then
		OwnerFactory.unregister(patient.owner)
		patient.owner.model:Destroy()
		patient.owner = nil
	end
end

--- Hasta klinigi terk eder (taburcu ya da bekleyip kacti).
local function sendAway(patient: Patient, walkOut: boolean)
	if clinic == nil then
		releasePatient(patient)
		return
	end

	patient.state = "leaving"
	if patient.queueSlot ~= nil then
		usedQueueSlots[patient.queueSlot] = nil
		patient.queueSlot = nil
	end
	if patient.stationId ~= nil then
		occupiedStation[patient.stationId] = nil
		patient.stationId = nil
	end
	local vet = patient.vet
	if vet ~= nil and activeOf[vet] == patient.id then
		activeOf[vet] = nil
		Surgery.cancel(vet)
	end

	if not walkOut then
		releasePatient(patient)
		return
	end

	AnimalAnimator.setLimp(patient.rig, 0, 0)
	AnimalAnimator.walkPath(patient.rig, {
		clinic.entranceInside,
		clinic.entranceOutside,
		clinic.entranceOutside + Vector3.new(0, 0, -30),
	}, function()
		local current = patients[patient.id]
		if current ~= nil then
			releasePatient(current)
		end
	end)

	if patient.owner ~= nil then
		OwnerFactory.walkPath(patient.owner, {
			clinic.entranceInside + Vector3.new(3, 0, 0),
			clinic.entranceOutside + Vector3.new(3, 0, 0),
			clinic.entranceOutside + Vector3.new(3, 0, -30),
		})
	end
end

-- ── Oyuncu eylemleri ──────────────────────────────────────────────────

--- Hayvan bu masaya sigiyor mu? Ati kucuk bir yikama kuvetine koymak
--- gercekci degil; kural oynanisin parcasi. Yilan kivrildigi icin
--- govde uzunlugu degil `tableFootprint` kullaniliyor.
local function fitsStation(animalId: string, stationId: string): boolean
	if clinic == nil then
		return false
	end
	local station = clinic.stations[stationId]
	local spec = AnimalFactory.spec(animalId)
	if station == nil or spec == nil then
		return false
	end
	local length, width = AnimalFactory.tableFootprint(spec)
	return length <= station.surface[1] + 1e-6 and width <= station.surface[2] + 1e-6
end

--- Hastayi bir istasyona yerlestirir: kivrilma ve ses dahil.
local function placeOnStation(patient: Patient, stationId: string)
	local station = (clinic :: Clinic.Built).stations[stationId]
	AnimalAnimator.placeAt(patient.rig, station.cframe * CFrame.new(0, 3.6, 0))
	AnimalAnimator.setCoiled(patient.rig, patient.rig.spec.rig == "serpentine")
	Audio.play("patientArrive", patient.rig.root)
end

--- Siradaki hastayi masaya alir; oyuncunun elinde hasta varsa onu
--- yakindaki baska istasyona tasir.
function PatientFlow.callNext(player: Player)
	if clinic == nil then
		return
	end

	local stationId = nearestStation(player)
	if stationId == nil then
		notify(player, "warn", "notify.notAtStation")
		return
	end

	local existing = patientOf(player)
	if existing ~= nil then
		-- Zaten bir hastasi var: onu buraya tasi.
		if existing.stationId == stationId then
			notify(player, "info", "notify.alreadyHere")
			return
		end
		if occupiedStation[stationId] ~= nil then
			notify(player, "warn", "notify.stationBusy")
			return
		end
		if not fitsStation(existing.animalId, stationId) then
			local animal = AnimalFactory.spec(existing.animalId)
			notify(player, "warn", "notify.tooBigForStation", {
				if animal ~= nil then animal.nameKey else existing.animalId,
			})
			return
		end
		if existing.stationId ~= nil then
			occupiedStation[existing.stationId] = nil
		end
		occupiedStation[stationId] = existing.id
		existing.stationId = stationId
		placeOnStation(existing, stationId)
		notify(player, "info", "notify.patientMoved")
		pushChart(player)
		return
	end

	if occupiedStation[stationId] ~= nil then
		notify(player, "warn", "notify.stationBusy")
		return
	end

	-- Kuyrukta en uzun bekleyen, sahipsiz hastayi al.
	local chosen: Patient? = nil
	for _, patient in patients do
		if patient.vet == nil and (patient.state == "waiting" or patient.state == "arriving") then
			if chosen == nil or patient.arrivedAt < chosen.arrivedAt then
				chosen = patient
			end
		end
	end

	if chosen == nil then
		notify(player, "info", if skipped then "notify.noneFitHere" else "notify.queueEmpty")
		return
	end

	chosen.vet = player
	chosen.state = "station"
	chosen.stationId = stationId
	if chosen.queueSlot ~= nil then
		usedQueueSlots[chosen.queueSlot] = nil
		chosen.queueSlot = nil
	end
	occupiedStation[stationId] = chosen.id
	activeOf[player] = chosen.id

	placeOnStation(chosen, stationId)

	notify(player, "good", "notify.patientTaken")
	pushState(player)
	pushChart(player)
	pushStateAll()
end

function PatientFlow.useTool(player: Player, toolId: string)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local patient = patientOf(player)
	if patient == nil then
		notify(player, "warn", "notify.noPatient")
		return
	end
	if patient.stationId == nil or not nearStation(player, patient.stationId) then
		notify(player, "warn", "notify.tooFar")
		return
	end
	if not Upgrades.hasTool(entry.data, toolId) then
		notify(player, "warn", "notify.toolLocked")
		return
	end
	if os.clock() < patient.busyUntil then
		return -- islem suruyor
	end

	local tool = Upgrades.tool(toolId)
	if tool == nil then
		return
	end
	patient.busyUntil = os.clock() + tool.useSeconds

	local found = Diagnosis.revealWith(patient.revealed, patient.conditionId, toolId)
	for _, symptomId in found do
		patient.revealed[symptomId] = true
	end
	patient.toolsUsed[toolId] = true

	-- Her alet kullanimi hayvani biraz gerer: butun aletleri sirayla
	-- denemek bedava degil.
	patient.stress = math.clamp(patient.stress + economy.toolStress, 0, 100)
	AnimalAnimator.setStress(patient.rig, patient.stress)

	local names = {}
	for _, symptomId in found do
		local symptom = Diagnosis.symptom(symptomId)
		table.insert(names, if symptom ~= nil then symptom.nameKey else symptomId)
	end

	Audio.play("tool" .. toolId:sub(1, 1):upper() .. toolId:sub(2), patient.rig.root)
	Net.toPlayer("ToolResult", player, toolId, names)
	pushChart(player)
end

function PatientFlow.submitDiagnosis(player: Player, conditionId: string)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local patient = patientOf(player)
	if patient == nil then
		notify(player, "warn", "notify.noPatient")
		return
	end
	if patient.diagnosed then
		notify(player, "info", "notify.alreadyDiagnosed")
		return
	end
	if patient.stationId == nil or not nearStation(player, patient.stationId) then
		notify(player, "warn", "notify.tooFar")
		return
	end
	if Diagnosis.condition(conditionId) == nil then
		return
	end

	local data = entry.data
	local effects = Upgrades.effectsOf(data)

	if Diagnosis.check(patient.conditionId, conditionId) then
		patient.diagnosed = true
		data.reputation = Economy.clampReputation(
			data.reputation + economy.reputation.correctDiagnosis * effects.repGain
		)
		data.stats.correct += 1
		data.stats.streak += 1
		data.stats.bestStreak = math.max(data.stats.bestStreak, data.stats.streak)
		Tasks.bump(data, "correct", 1)
		Tasks.set(data, "streak", data.stats.streak)

		patient.satisfaction = math.clamp(
			patient.satisfaction + economy.owner.correctDiagnosisGain,
			0,
			100
		)
		local condition = Diagnosis.condition(conditionId)
		if patient.owner ~= nil then
			OwnerFactory.updateSatisfaction(patient.owner, patient.satisfaction)
			OwnerFactory.say(patient.owner, "hint.relieved")
		end
		Audio.play("animal" .. patient.animalId:sub(1, 1):upper() .. patient.animalId:sub(2), patient.rig.root)
		notify(player, "good", "notify.diagnosisCorrect", { condition.nameKey })
	else
		data.reputation = Economy.clampReputation(data.reputation + economy.reputation.wrongDiagnosis)
		data.stats.wrong += 1
		-- Seri tek yanlista sifirlaniyor: "kusursuz" basarimlarinin
		-- anlami olsun.
		data.stats.streak = 0
		patient.stress = math.clamp(patient.stress + economy.wrongDiagnosisStress, 0, 100)
		AnimalAnimator.setStress(patient.rig, patient.stress)

		patient.satisfaction = math.clamp(
			patient.satisfaction + economy.owner.wrongDiagnosisHit,
			0,
			100
		)
		if patient.owner ~= nil then
			OwnerFactory.updateSatisfaction(patient.owner, patient.satisfaction)
			OwnerFactory.say(patient.owner, "hint.worried")
		end
		notify(player, "bad", "notify.diagnosisWrong")
	end

	settleProgress(player, data)
	pushState(player)
	pushChart(player)
end

local function finishTreatment(player: Player, patient: Patient, treatmentId: string, outcome: Treatment.Outcome)
	patient.health = math.clamp(patient.health + outcome.healthDelta, 0, 100)
	patient.stress = math.clamp(patient.stress + outcome.stressDelta, 0, 100)
	AnimalAnimator.setStress(patient.rig, patient.stress)

	if outcome.correct then
		Audio.play("treat" .. treatmentId:sub(1, 1):upper() .. treatmentId:sub(2), patient.rig.root)
		patient.treatmentsDone[treatmentId] = true
		-- Kirik iyilesince topallama biter: gorunur geri bildirim.
		if treatmentId == "surgery" or treatmentId == "bandage" then
			AnimalAnimator.setLimp(patient.rig, 0, 0)
		end
	end

	updateNameplate(patient)
	pushChart(player)
	pushState(player)
end

function PatientFlow.applyTreatment(player: Player, treatmentId: string)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local patient = patientOf(player)
	if patient == nil then
		notify(player, "warn", "notify.noPatient")
		return
	end
	if patient.stationId == nil or not nearStation(player, patient.stationId) then
		notify(player, "warn", "notify.tooFar")
		return
	end
	if not patient.diagnosed then
		notify(player, "warn", "notify.diagnoseFirst")
		return
	end
	if os.clock() < patient.busyUntil then
		return
	end

	local treatment = Treatment.get(treatmentId)
	if treatment == nil then
		return
	end

	local data = entry.data
	if data.level < treatment.unlockLevel then
		notify(player, "warn", "notify.treatmentLocked")
		return
	end
	if patient.treatmentsDone[treatmentId] == true then
		notify(player, "info", "notify.treatmentDone")
		return
	end

	local effects = Upgrades.effectsOf(data)
	local cost = Treatment.cost(treatmentId, effects.supplyDiscount)
	if data.money < cost then
		notify(player, "warn", "notify.noMoneyForSupply", { cost })
		return
	end

	-- Ameliyat, ameliyat masasinda yapilir. Mekani oynanisin parcasi
	-- yapan tek kural bu.
	if treatment.minigame == "surgery" and patient.stationId ~= "surgery" then
		notify(player, "warn", "notify.needSurgeryRoom")
		return
	end

	data.money -= cost
	Profile.markDirty(player)

	if treatment.minigame == "surgery" then
		if Surgery.active(player) ~= nil then
			return
		end
		local patientId = patient.id
		local schedule = Surgery.begin(player, patientId, effects.surgeryTolerance, function(passed, score)
			local current = patients[patientId]
			Net.toPlayer("SurgeryEnd", player, passed, score)
			if current == nil then
				return
			end
			if passed then
				finishTreatment(player, current, treatmentId, {
					ok = true,
					reasonKey = "treat.done",
					correct = true,
					healthDelta = treatment.healthDelta * (0.6 + 0.4 * score),
					stressDelta = treatment.stressDelta,
				})
				data.stats.surgeries += 1
				Tasks.bump(data, "surgeries", 1)
				-- "Kusursuz" ameliyat: 0.9 uzeri puan. Basarim ve gunluk
				-- gorev buna bakiyor.
				if score >= 0.9 then
					data.stats.perfectSurgeries += 1
					Tasks.bump(data, "perfectSurgeries", 1)
				end
				settleProgress(player, data)
				notify(player, "good", "notify.surgerySuccess", { math.floor(score * 100 + 0.5) })
			else
				finishTreatment(player, current, treatmentId, {
					ok = true,
					reasonKey = "treat.wrong",
					correct = false,
					healthDelta = economy.wrongTreatmentHealth,
					stressDelta = treatment.stressDelta,
				})
				notify(player, "bad", "notify.surgeryFailed", { math.floor(score * 100 + 0.5) })
			end
		end)
		Net.toPlayer("SurgeryBegin", player, schedule)
		pushState(player)
		return
	end

	local seconds = Treatment.seconds(treatmentId, effects.treatSpeed)
	patient.busyUntil = os.clock() + seconds

	local outcome = Treatment.evaluate(patient.conditionId, treatmentId, patient.stress, effects)
	if not outcome.ok then
		patient.stress = math.clamp(patient.stress + outcome.stressDelta, 0, 100)
		AnimalAnimator.setStress(patient.rig, patient.stress)
		notify(player, "bad", outcome.reasonKey)
		pushChart(player)
		return
	end

	local patientId = patient.id
	task.delay(seconds, function()
		local current = patients[patientId]
		if current == nil or current.vet ~= player then
			return
		end
		finishTreatment(player, current, treatmentId, outcome)
		if outcome.correct then
			notify(player, "good", "notify.treatmentApplied", { treatment.nameKey })
		else
			notify(player, "bad", "notify.treatmentWrong")
		end
	end)
end

function PatientFlow.surgeryInput(player: Player, step: number)
	local result = Surgery.input(player, step)
	if result.accepted then
		Net.toPlayer("ToolResult", player, "surgeryStep", { result.grade })
	end
end

function PatientFlow.discharge(player: Player)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local patient = patientOf(player)
	if patient == nil then
		notify(player, "warn", "notify.noPatient")
		return
	end
	if not patient.diagnosed then
		notify(player, "warn", "notify.diagnoseFirst")
		return
	end
	if not Treatment.isComplete(patient.treatmentsDone, patient.conditionId) then
		notify(player, "warn", "notify.treatmentsIncomplete")
		return
	end

	local data = entry.data
	local effects = Upgrades.effectsOf(data)
	local animal = AnimalFactory.spec(patient.animalId)
	local condition = Diagnosis.condition(patient.conditionId)
	if animal == nil or condition == nil then
		return
	end

	local fee = Economy.fee({
		baseFee = animal.baseFee,
		feeMultiplier = condition.feeMultiplier,
		reputation = data.reputation,
		health = patient.health,
		feeBonus = effects.feeBonus,
	})

	-- Seviye, XP eklenmeden ONCE okunmali: sonra okunursa atlama hic
	-- fark edilmez.
	local previousLevel = data.level
	local xpGain = condition.xp
	if patient.emergency then
		fee = math.floor(fee * economy.emergency.feeMultiplier + 0.5)
		xpGain = math.floor(xpGain * economy.emergency.xpMultiplier + 0.5)
		data.stats.emergencies += 1
		Tasks.bump(data, "emergencies", 1)
		data.reputation = Economy.clampReputation(
			data.reputation + economy.emergency.reputationGain * effects.repGain
		)
	end

	-- Bahsis: sahibi yeterince memnunsa ucretin uzerine birakiyor.
	-- Bekletmemenin ve dogru teshisin gorunur karsiligi bu.
	local tip = 0
	if patient.satisfaction >= economy.owner.minTipSatisfaction then
		local share = (patient.satisfaction - economy.owner.minTipSatisfaction)
			/ math.max(1, 100 - economy.owner.minTipSatisfaction)
		tip = math.floor(fee * economy.owner.tipShare * share + 0.5)
	end

	data.money += fee + tip
	data.xp += xpGain
	data.stats.patients += 1
	data.stats.earned += fee + tip
	data.stats.tips += tip
	data.species[patient.animalId] = true

	Tasks.bump(data, "patients", 1)
	Tasks.bump(data, "earned", fee + tip)
	Tasks.markSpecies(data, patient.animalId)

	if patient.health >= economy.patient.healthyDischargeThreshold then
		data.reputation = Economy.clampReputation(
			data.reputation + economy.reputation.healthyDischarge * effects.repGain
		)
		patient.satisfaction = math.clamp(
			patient.satisfaction + economy.owner.healthyDischargeGain,
			0,
			100
		)
	elseif patient.stress >= 70 then
		data.reputation = Economy.clampReputation(data.reputation + economy.reputation.stressedDischarge)
	end

	if patient.owner ~= nil then
		OwnerFactory.updateSatisfaction(patient.owner, patient.satisfaction)
		OwnerFactory.say(patient.owner, if tip > 0 then "hint.thanks" else "hint.goodbye")
	end

	applyLevel(player, data, previousLevel)

	if tip > 0 then
		notify(player, "good", "notify.dischargedTip", { fee, tip, xpGain })
	else
		notify(player, "good", "notify.discharged", { fee, xpGain })
	end

	settleProgress(player, data)

	Audio.play("discharge", patient.rig.root)
	sendAway(patient, true)
	pushState(player)
	pushChart(player)
	pushStateAll()
end

--- Oyuncu ayrildi: hastasini kuyruga geri koymak yerine birakiyoruz ki
--- baska bir veteriner alabilsin.
function PatientFlow.playerLeft(player: Player)
	Surgery.cancel(player)
	local patient = patientOf(player)
	activeOf[player] = nil
	if patient == nil then
		return
	end
	patient.vet = nil
	-- Masada kalsin; siradaki oyuncu "Sirayi cagir" ile devralabilir.
	patient.state = "waiting"
end

-- ── Dongu ─────────────────────────────────────────────────────────────

local function tickVitals(dt: number)
	local config = economy.patient
	for _, patient in patients do
		if patient.state == "leaving" then
			continue
		end

		-- Tedavi edilmeyen hasta yavas yavas kotuluyor.
		if not Treatment.isComplete(patient.treatmentsDone, patient.conditionId) then
			patient.health = math.clamp(patient.health - config.healthDrainPerSecond * dt, 0, 100)
		end

		-- Stres kendiliginden yavas yavas duser.
		if patient.stress > 0 then
			patient.stress = math.clamp(patient.stress - config.stressDecayPerSecond * dt, 0, 100)
			AnimalAnimator.setStress(patient.rig, patient.stress)
		end

		AnimalAnimator.setState(patient.rig, if patient.stress >= 70 then "distress" else "idle")
		updateNameplate(patient)

		-- Sahibin sabri: bekledikce tukeniyor. Hastasi masaya alinmis
		-- olsa bile isin uzamasi memnuniyeti dusuruyor.
		if patient.owner ~= nil and patient.state ~= "leaving" then
			patient.satisfaction = math.clamp(
				patient.satisfaction - economy.owner.decayPerSecond * dt,
				0,
				100
			)
			OwnerFactory.updateSatisfaction(patient.owner, patient.satisfaction)
		end

		-- Acil vakanin suresi: dolarsa hasta kaybediliyor. Bu, oyunun
		-- kaybedilebilir TEK durumu; bu yuzden ceza da agir.
		if patient.emergency and os.clock() > patient.deadline then
			local animal = AnimalFactory.spec(patient.animalId)
			local animalKey = if animal ~= nil then animal.nameKey else patient.animalId
			notifyAll("bad", "notify.emergencyLost", { animalKey })
			local vet = patient.vet
			if vet ~= nil then
				local entry = Profile.get(vet)
				if entry ~= nil then
					entry.data.reputation = Economy.clampReputation(
						entry.data.reputation + economy.emergency.reputationLoss
					)
					entry.data.stats.lost += 1
					entry.data.stats.streak = 0
					Profile.markDirty(vet)
				end
			end
			sendAway(patient, true)
			pushStateAll()
			continue
		end

		-- Bekleyip bekleyip kacan hasta: butun klinigin itibari duser.
		if patient.vet == nil and patient.state == "waiting" then
			if os.clock() - patient.arrivedAt > economy.arrival.patienceSeconds then
				local animal = AnimalFactory.spec(patient.animalId)
				notifyAll("bad", "notify.patientLeft", { if animal ~= nil then animal.nameKey else patient.animalId })
				for _, player in Players:GetPlayers() do
					local entry = Profile.get(player)
					if entry ~= nil then
						entry.data.reputation = Economy.clampReputation(
							entry.data.reputation + economy.reputation.timeout
						)
						Profile.markDirty(player)
					end
				end
				sendAway(patient, true)
				pushStateAll()
			end
		end
	end
end

--- Klinigi bagla ve dongulari baslat.
function PatientFlow.start(built: Clinic.Built)
	if started then
		return
	end
	started = true
	clinic = built

	local folder = Instance.new("Folder")
	folder.Name = "Hastalar"
	folder.Parent = workspace
	patientsFolder = folder

	-- Klinigin ortam ugultusu: sessiz bir odadan cok daha yasayan bir yer.
	local ambience = Instance.new("Part")
	ambience.Name = "OrtamSesi"
	ambience.Size = Vector3.new(1, 1, 1)
	ambience.CFrame = CFrame.new(built.entranceInside)
	ambience.Anchored = true
	ambience.Transparency = 1
	ambience.CanCollide = false
	ambience.CanQuery = false
	ambience.Parent = workspace
	Audio.ambience("clinicAmbience", ambience)

	-- Hasta gelisi
	task.spawn(function()
		-- Ilk hasta hemen gelmesin: oyuncu once etrafa bakabilsin.
		task.wait(6)
		while true do
			if #Players:GetPlayers() > 0 then
				spawnPatient()
			end
			local clinicEffects = Upgrades.clinicEffects(Players:GetPlayers())
			task.wait(Economy.arrivalSeconds(bestReputation(), clinicEffects.arrivalScale))
		end
	end)

	-- Yasamsal belirtiler
	task.spawn(function()
		while true do
			task.wait(VITALS_TICK)
			tickVitals(VITALS_TICK)
		end
	end)

	-- Arayuzun kuyruk sayaci canli kalsin.
	task.spawn(function()
		while true do
			task.wait(2)
			for _, player in Players:GetPlayers() do
				pushChart(player)
			end
		end
	end)
end

--- Kac hasta canli? SelfTest ve tanilama icin.
function PatientFlow.patientCount(): number
	local total = 0
	for _ in patients do
		total += 1
	end
	return total
end

return PatientFlow
