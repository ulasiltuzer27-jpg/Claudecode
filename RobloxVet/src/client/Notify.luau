--!strict
-- Notify.luau
-- ===========
-- Ekranin ustunde yigilan kisa bildirimler.
--
-- Metin SUNUCUDAN GELMIYOR, anahtar geliyor: sunucu `notify.discharged`
-- gibi bir anahtar ve argumanlari yolluyor, ceviriyi istemci yapiyor.
-- Boylece dil degistirmek sunucuyu ilgilendirmiyor ve eksik anahtar
-- ekranda `[notify.discharged]` diye GORUNUYOR (Loc kurali).

local TweenService = game:GetService("TweenService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Audio = require(Shared.Audio)
local Loc = require(Shared.Loc)

local Theme = require(script.Parent.Theme)

local Notify = {}

local container: Frame? = nil
local MAX_VISIBLE = 5

function Notify.create(parent: ScreenGui)
	local frame = Theme.frame({
		name = "Bildirimler",
		size = UDim2.new(0, 330, 0, 240),
		position = UDim2.new(0, 16, 0, 158),
		anchor = Vector2.new(0, 0),
		transparency = 1,
		radius = 0,
		parent = parent,
	})
	local layout = Theme.list(frame, 6)
	layout.HorizontalAlignment = Enum.HorizontalAlignment.Left
	container = frame
end

--- Bir bildirim gosterir. `args` icindeki `xxx.yyy` bicimindeki
--- dizeler de ceviri anahtari sayilip cevriliyor: sunucu tur/hastalik
--- adlarini anahtar olarak yolluyor.
-- Bazi bildirimlerin kendi sesi var; geri kalani turune gore genel bir
-- ses caliyor. Anahtar -> ses eslemesi burada, tek yerde.
local CUE_FOR_KEY = {
	["notify.levelUp"] = "levelUp",
	["notify.achievement"] = "achievement",
	["notify.taskDone"] = "taskDone",
	["notify.discharged"] = "cash",
	["notify.dischargedTip"] = "cash",
	["notify.diagnosisCorrect"] = "diagnosisCorrect",
	["notify.diagnosisWrong"] = "diagnosisWrong",
	["shop.bought"] = "cash",
}

function Notify.push(kind: string, messageKey: string, args: { any })
	if container == nil then
		return
	end

	local cue = CUE_FOR_KEY[messageKey]
	if cue ~= nil then
		Audio.play(cue)
	elseif kind == "bad" or kind == "warn" then
		Audio.play("uiDeny")
	else
		Audio.play("notify")
	end

	local translated = {}
	for index, value in args do
		if type(value) == "string" and string.find(value, "%.") ~= nil and Loc.has(value) then
			translated[index] = Loc.get(value)
		else
			translated[index] = value
		end
	end

	local text = Loc.get(messageKey, table.unpack(translated, 1, #args))

	local card = Theme.frame({
		name = "Bildirim",
		size = UDim2.new(1, 0, 0, 34),
		color = Theme.color.panel,
		transparency = 0.1,
		radius = 8,
		parent = container,
	})
	Theme.stroke(card, Theme.kindColor(kind), 1.5)

	local accent = Theme.frame({
		name = "Serit",
		size = UDim2.new(0, 4, 1, -10),
		position = UDim2.fromOffset(6, 5),
		color = Theme.kindColor(kind),
		transparency = 0,
		radius = 2,
		parent = card,
	})
	accent.ZIndex = 2

	Theme.label({
		text = text,
		size = UDim2.new(1, -24, 1, 0),
		position = UDim2.fromOffset(18, 0),
		textSize = 14,
		bold = true,
		color = Theme.color.text,
		parent = card,
	})

	-- Ekrani doldurmasin: en eskiyi at.
	local cards = {}
	for _, child in container:GetChildren() do
		if child:IsA("Frame") then
			table.insert(cards, child)
		end
	end
	if #cards > MAX_VISIBLE then
		cards[1]:Destroy()
	end

	task.delay(4.5, function()
		if card.Parent == nil then
			return
		end
		local tween = TweenService:Create(card, TweenInfo.new(0.35), { BackgroundTransparency = 1 })
		tween:Play()
		tween.Completed:Once(function()
			card:Destroy()
		end)
	end)
end

return Notify
