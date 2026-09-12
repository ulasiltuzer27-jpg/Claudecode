--!strict
-- Diagnosis.luau
-- ==============
-- Alet -> semptom -> hastalik cikarimi. SUNUCU OTORITER.
--
-- Hastaligin kimligi ISTEMCIYE HIC GITMIYOR
-- -----------------------------------------
-- Istemciye yalnizca BULUNMUS semptomlar yollaniyor. Hastaligi istemciye
-- yollayip "gostermezsin" demek, oynanisi bir bellek okumasiyla
-- cozulebilir hale getirirdi. Tahmin sunucuda karsilastiriliyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local conditionData = require(Data.conditions)
local symptomData = require(Data.symptoms)

local Diagnosis = {}

local conditionById: { [string]: any } = {}
for _, condition in conditionData.conditions do
	conditionById[condition.id] = condition
end

local symptomById: { [string]: any } = {}
for _, symptom in symptomData.symptoms do
	symptomById[symptom.id] = symptom
end

-- Alet -> o aletin actigi semptomlar. Veri tablosundan bir kez kuruluyor.
local symptomsByTool: { [string]: { string } } = {}
for _, symptom in symptomData.symptoms do
	local list = symptomsByTool[symptom.revealedBy]
	if list == nil then
		list = {}
		symptomsByTool[symptom.revealedBy] = list
	end
	table.insert(list, symptom.id)
end

function Diagnosis.condition(id: string): any?
	return conditionById[id]
end

function Diagnosis.symptom(id: string): any?
	return symptomById[id]
end

function Diagnosis.allConditions(): { any }
	return conditionData.conditions
end

function Diagnosis.allSymptoms(): { any }
	return symptomData.symptoms
end

--- Bir turun, verilen seviyede yakalanabilecegi hastaliklar.
function Diagnosis.conditionsFor(animalId: string, level: number): { any }
	local result = {}
	for _, condition in conditionData.conditions do
		if condition.unlockLevel > level then
			continue
		end
		for _, species in condition.species do
			if species == animalId then
				table.insert(result, condition)
				break
			end
		end
	end
	return result
end

--- Aletin bu hastada ACABILECEGI semptomlar (henuz bulunmamis olanlar).
--- Alet hastaligin hicbir semptomunu acmiyorsa bos liste doner - bu bir
--- hata degil, "bu alette bir sey cikmadi" sonucu.
function Diagnosis.revealWith(revealed: { [string]: boolean }, conditionId: string, toolId: string): { string }
	local condition = conditionById[conditionId]
	if condition == nil then
		return {}
	end

	local toolSymptoms = symptomsByTool[toolId]
	if toolSymptoms == nil then
		return {}
	end

	local found = {}
	for _, symptomId in condition.symptoms do
		if revealed[symptomId] == true then
			continue
		end
		for _, candidate in toolSymptoms do
			if candidate == symptomId then
				table.insert(found, symptomId)
				break
			end
		end
	end
	return found
end

--- Bu hastaligin butun semptomlari bulundu mu? Tahmin dugmesini acmak
--- icin degil (oyuncu erken tahmin edebilir), arayuzde ipucu gostermek icin.
function Diagnosis.allRevealed(revealed: { [string]: boolean }, conditionId: string): boolean
	local condition = conditionById[conditionId]
	if condition == nil then
		return false
	end
	for _, symptomId in condition.symptoms do
		if revealed[symptomId] ~= true then
			return false
		end
	end
	return true
end

--- Tahmin dogru mu?
function Diagnosis.check(conditionId: string, guessId: string): boolean
	return conditionId == guessId
end

--- Semptomu hangi alet aciyor? Arayuz "hangi aleti almaliyim" ipucu icin.
function Diagnosis.toolFor(symptomId: string): string?
	local symptom = symptomById[symptomId]
	return if symptom ~= nil then symptom.revealedBy else nil
end

return Diagnosis
