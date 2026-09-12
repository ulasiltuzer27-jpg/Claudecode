--!strict
-- Treatment.luau
-- ==============
-- Tedavi uygulama. SUNUCU OTORITER.
--
-- Yanlis tedavi cezasiz degil
-- ---------------------------
-- Hastaligin gerektirmedigi bir tedavi uygulamak hayvani gerer ve
-- sagligini dusurur. Yoksa oyuncu butun tedavileri sirayla uygulayip
-- bulmacayi atlayabilirdi.
--
-- Stres esigi
-- -----------
-- Stres `resistStress`'e ulastiysa hayvan DIRENIR: tedavi uygulanmaz,
-- sarf malzemesi yine gider. Oyuncunun yikama/besleme ile arada
-- hayvani sakinlestirmesi gerekiyor - oynanisin ikinci katmani bu.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Diagnosis = require(script.Parent.Diagnosis)
local Economy = require(script.Parent.Economy)

local treatmentData = require(Data.treatments)

local Treatment = {}

local byId: { [string]: any } = {}
for _, treatment in treatmentData.treatments do
	byId[treatment.id] = treatment
end

Treatment.surgeryConfig = treatmentData.surgery

function Treatment.get(id: string): any?
	return byId[id]
end

function Treatment.all(): { any }
	return treatmentData.treatments
end

--- Hastaligin gerektirdigi tedaviler.
function Treatment.requiredFor(conditionId: string): { string }
	local condition = Diagnosis.condition(conditionId)
	if condition == nil then
		return {}
	end
	return condition.treatments
end

--- Tedavi bu hastalik icin dogru mu?
function Treatment.isRequired(conditionId: string, treatmentId: string): boolean
	for _, id in Treatment.requiredFor(conditionId) do
		if id == treatmentId then
			return true
		end
	end
	return false
end

--- Gereken butun tedaviler uygulandi mi?
function Treatment.isComplete(done: { [string]: boolean }, conditionId: string): boolean
	local required = Treatment.requiredFor(conditionId)
	if #required == 0 then
		return false
	end
	for _, id in required do
		if done[id] ~= true then
			return false
		end
	end
	return true
end

--- Sarf malzemesi bedeli (eczane yukseltmesi indirim yapiyor).
function Treatment.cost(treatmentId: string, supplyDiscount: number): number
	local treatment = byId[treatmentId]
	if treatment == nil then
		return 0
	end
	return math.max(0, math.floor(treatment.supplyCost * supplyDiscount + 0.5))
end

--- Tedavinin suresi (daha iyi masa yukseltmesi kisaltiyor).
function Treatment.seconds(treatmentId: string, treatSpeed: number): number
	local treatment = byId[treatmentId]
	if treatment == nil then
		return 0
	end
	return math.max(0.2, treatment.seconds * treatSpeed)
end

export type Outcome = {
	ok: boolean,
	reasonKey: string,
	correct: boolean,
	healthDelta: number,
	stressDelta: number,
}

--- Tedaviyi hesaplar. Hastanin sagligini/stresini DEGISTIRMEZ; sonucu
--- doner, uygulamak PatientFlow'un isi (tek yazici, tek yer).
function Treatment.evaluate(
	conditionId: string,
	treatmentId: string,
	currentStress: number,
	effects: { [string]: number }
): Outcome
	local treatment = byId[treatmentId]
	if treatment == nil then
		return { ok = false, reasonKey = "treat.unknown", correct = false, healthDelta = 0, stressDelta = 0 }
	end

	local config = Economy.table()

	if currentStress >= config.patient.resistStress then
		-- Hayvan direniyor: islem bosa gitti, stres biraz daha artti.
		return {
			ok = false,
			reasonKey = "treat.resisted",
			correct = false,
			healthDelta = 0,
			stressDelta = 4,
		}
	end

	local correct = Treatment.isRequired(conditionId, treatmentId)
	if not correct then
		return {
			ok = true,
			reasonKey = "treat.wrong",
			correct = false,
			healthDelta = config.wrongTreatmentHealth,
			stressDelta = config.wrongTreatmentStress,
		}
	end

	local healthDelta = treatment.healthDelta
	-- Yikama istasyonu yukseltmesi yikamayi guclendiriyor.
	if treatment.kind == "wash" then
		healthDelta += effects.washBonus or 0
	end

	return {
		ok = true,
		reasonKey = "treat.done",
		correct = true,
		healthDelta = healthDelta,
		stressDelta = treatment.stressDelta,
	}
end

return Treatment
