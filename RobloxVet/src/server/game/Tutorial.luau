--!strict
-- Tutorial.luau
-- =============
-- Ilk hastada adim adim yol gosteren ogretici.
--
-- Neden SUNUCUDA?
-- ---------------
-- Adimlar GERCEKTEN YAPILAN islerle aciliyor: "hastayi al" adimi
-- ancak hasta alininca kapaniyor, "teshis koy" adimi ancak DOGRU
-- teshis konunca. Istemcide dursaydi ogretici "tamam" diyerek
-- gecilebilirdi ve hicbir sey ogretmezdi.
--
-- Ilerleme PROFILDE (`tutorialStep`): oyuncu cikip girdiginde kaldigi
-- yerden devam ediyor, ikinci oturumunda bastan baslamiyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local plan = require(Data.tutorial)

local Tutorial = {}

Tutorial.STEPS = #plan.steps

--- Istemcinin tanidigi vurgu bolgeleri. verify_data bu kumenin
--- veriyle ayni oldugunu denetliyor: veride olup burada olmayan bir ad,
--- ekranda hicbir seyin isaretlenmemesi demek olurdu.
local REGIONS = {
	toolbar = true,
	chart = true,
	rightLower = true,
	hud = true,
}

function Tutorial.regions(): { string }
	local names = {}
	for name in REGIONS do
		table.insert(names, name)
	end
	table.sort(names)
	return names
end

function Tutorial.knownRegion(name: string): boolean
	return REGIONS[name] == true
end

function Tutorial.steps(): { any }
	return plan.steps
end

--- Ogretici bitti mi?
function Tutorial.done(data: any): boolean
	return data.tutorialStep >= Tutorial.STEPS
end

--- O anki adim (bittiyse nil).
function Tutorial.current(data: any): any?
	if Tutorial.done(data) then
		return nil
	end
	return plan.steps[data.tutorialStep + 1]
end

--- Oyuncu bir sey yapti. Bekledigimiz sey buysa adim ilerliyor ve
--- `true` donuyor (cagiran taraf profili kirli isaretliyor ve durumu
--- tazeliyor).
---
--- Beklenmeyen bir eylem hicbir sey yapmiyor: oyuncu ogreticinin
--- onunden kosarsa adimlar atlanmiyor, cunku atlanan adim ogretilmemis
--- adimdir.
function Tutorial.advance(data: any, action: string): boolean
	local step = Tutorial.current(data)
	if step == nil or step.advanceOn ~= action then
		return false
	end
	data.tutorialStep += 1
	return true
end

--- Istemciye yollanan goruntu. Bittiyse nil: HUD hicbir sey cizmiyor.
function Tutorial.snapshot(data: any): any
	local step = Tutorial.current(data)
	if step == nil then
		return nil
	end
	return {
		index = data.tutorialStep + 1,
		total = Tutorial.STEPS,
		titleKey = step.titleKey,
		bodyKey = step.bodyKey,
		highlight = step.highlight,
	}
end

return Tutorial
