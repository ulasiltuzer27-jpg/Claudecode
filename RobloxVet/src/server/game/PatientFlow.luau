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

local Net = require(Shared.Net)

local AnimalAnimator = require(script.Parent.Parent.world.AnimalAnimator)
local AnimalFactory = require(script.Parent.Parent.world.AnimalFactory)
local Clinic = require(script.Parent.Parent.world.Clinic)
local Diagnosis = require(script.Parent.Diagnosis)
local Economy = require(script.Parent.Economy)
local Profile = require(script.Parent.Profile)
local Surgery = require(script.Parent.Surgery)
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
	owner: Player?,
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
	}
end

local function pushState(player: Player)
	Net.toPlayer("StateUpdate", player, stateSnapshot(player))
end

function PatientFlow.pushState(player: Player)
	pushState(player)
	pushChart(player)
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

	local id = nextPatientId
	nextPatientId += 1

	local patient: Patient = {
		id = id,
		animalId = animalId,
		conditionId = conditionId,
		rig = rig,
		health = math.random(economy.patient.startHealthMin, economy.patient.startHealthMax),
		stress = math.random(economy.patient.startStressMin, economy.patient.startStressMax),
		revealed = {},
		toolsUsed = {},
		diagnosed = false,
		treatmentsDone = {},
		owner = nil,
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

	-- Topallayan hastalik gozle belli olsun: oyuncu daha aleti eline
	-- almadan "bacakta bir sey var" diyebilmeli.
	local condition = Diagnosis.condition(conditionId)
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

	updateNameplate(patient)
	notifyAll("info", "notify.patientArrived", { animalId })
	pushStateAll()
end

local function releasePatient(patient: Patient)
	if patient.queueSlot ~= nil then
		usedQueueSlots[patient.queueSlot] = nil
	end
	if patient.stationId ~= nil then
		occupiedStation[patient.stationId] = nil
	end
	local owner = patient.owner
	if owner ~= nil and activeOf[owner] == patient.id then
		activeOf[owner] = nil
		Surgery.cancel(owner)
	end
	patients[patient.id] = nil

	AnimalAnimator.unregister(patient.rig)
	patient.rig.model:Destroy()
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
	local owner = patient.owner
	if owner ~= nil and activeOf[owner] == patient.id then
		activeOf[owner] = nil
		Surgery.cancel(owner)
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
end

-- ── Oyuncu eylemleri ──────────────────────────────────────────────────

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
		if existing.stationId ~= nil then
			occupiedStation[existing.stationId] = nil
		end
		occupiedStation[stationId] = existing.id
		existing.stationId = stationId
		local station = clinic.stations[stationId]
		AnimalAnimator.placeAt(existing.rig, station.cframe * CFrame.new(0, 3.6, 0))
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
		if patient.owner == nil and (patient.state == "waiting" or patient.state == "arriving") then
			if chosen == nil or patient.arrivedAt < chosen.arrivedAt then
				chosen = patient
			end
		end
	end

	if chosen == nil then
		notify(player, "info", "notify.queueEmpty")
		return
	end

	chosen.owner = player
	chosen.state = "station"
	chosen.stationId = stationId
	if chosen.queueSlot ~= nil then
		usedQueueSlots[chosen.queueSlot] = nil
		chosen.queueSlot = nil
	end
	occupiedStation[stationId] = chosen.id
	activeOf[player] = chosen.id

	local station = clinic.stations[stationId]
	AnimalAnimator.placeAt(chosen.rig, station.cframe * CFrame.new(0, 3.6, 0))

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
		local condition = Diagnosis.condition(conditionId)
		notify(player, "good", "notify.diagnosisCorrect", { condition.nameKey })
	else
		data.reputation = Economy.clampReputation(data.reputation + economy.reputation.wrongDiagnosis)
		data.stats.wrong += 1
		patient.stress = math.clamp(patient.stress + economy.wrongDiagnosisStress, 0, 100)
		AnimalAnimator.setStress(patient.rig, patient.stress)
		notify(player, "bad", "notify.diagnosisWrong")
	end

	Profile.markDirty(player)
	pushState(player)
	pushChart(player)
end

local function finishTreatment(player: Player, patient: Patient, treatmentId: string, outcome: Treatment.Outcome)
	patient.health = math.clamp(patient.health + outcome.healthDelta, 0, 100)
	patient.stress = math.clamp(patient.stress + outcome.stressDelta, 0, 100)
	AnimalAnimator.setStress(patient.rig, patient.stress)

	if outcome.correct then
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
		if current == nil or current.owner ~= player then
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

	data.money += fee
	data.xp += condition.xp
	data.stats.patients += 1
	data.stats.earned += fee

	if patient.health >= economy.patient.healthyDischargeThreshold then
		data.reputation = Economy.clampReputation(
			data.reputation + economy.reputation.healthyDischarge * effects.repGain
		)
	elseif patient.stress >= 70 then
		data.reputation = Economy.clampReputation(data.reputation + economy.reputation.stressedDischarge)
	end

	local previousLevel = data.level
	data.level = Economy.levelForXp(data.xp)
	if data.level > previousLevel then
		local granted = Upgrades.grantFreeTools(data)
		notify(player, "good", "notify.levelUp", { data.level, Economy.titleKeyForLevel(data.level) })
		for _, toolId in granted do
			local tool = Upgrades.tool(toolId)
			notify(player, "good", "notify.toolUnlocked", { if tool ~= nil then tool.nameKey else toolId })
		end
	end

	notify(player, "good", "notify.discharged", { fee, condition.xp })
	Profile.markDirty(player)

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
	patient.owner = nil
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

		-- Bekleyip bekleyip kacan hasta: butun klinigin itibari duser.
		if patient.owner == nil and patient.state == "waiting" then
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
