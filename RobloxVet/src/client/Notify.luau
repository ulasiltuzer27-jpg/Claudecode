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
		size = UDim2.new(0, 340, 0, 260),
		position = UDim2.new(0, Theme.space.lg, 0, 186),
		anchor = Vector2.new(0, 0),
		transparency = 1,
		radius = 0,
		parent = parent,
	})
	local layout = Theme.list(frame, Theme.space.sm)
	layout.HorizontalAlignment = Enum.HorizontalAlignment.Left
	container = frame
end

-- Bildirim turu -> ikon. Ikonlar `Theme.icon` ile Frame'lerden
-- kuruluyor; hicbiri bir dokuya ya da yazi tipine bagli degil.
local ICON_FOR_KIND = {
	good = "check",
	bad = "cross",
	warn = "alert",
	info = "info",
}

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

	local color = Theme.kindColor(kind)
	-- Yuva UIListLayout'un yonettigi ogedir; kart onun ICINDE kayiyor.
	-- (Layout'un konumunu tweenlemek ise yaramaz: layout her karede
	-- konumu geri yaziyor.)
	local slot = Theme.frame({
		name = "Yuva",
		size = UDim2.new(1, 0, 0, 38),
		transparency = 1,
		radius = 0,
		parent = container,
	})
	slot.ClipsDescendants = false

	local card = Theme.card({
		name = "Bildirim",
		size = UDim2.fromScale(1, 1),
		color = Theme.color.panel,
		radius = Theme.radius.sm,
		parent = slot,
	}, color)

	local accent = Theme.frame({
		name = "Serit",
		size = UDim2.new(0, 4, 1, -12),
		position = UDim2.fromOffset(5, 6),
		color = color,
		transparency = 0,
		radius = 2,
		parent = card,
		zIndex = 2,
	})
	accent.ZIndex = 2

	Theme.icon({
		name = ICON_FOR_KIND[kind] or "info",
		size = 14,
		color = color,
		position = UDim2.fromOffset(16, 12),
		parent = card,
		zIndex = 2,
	})

	Theme.label({
		text = text,
		size = UDim2.new(1, -46, 1, 0),
		position = UDim2.fromOffset(38, 0),
		textSize = Theme.size.small,
		bold = true,
		color = Theme.color.text,
		wrapped = true,
		parent = card,
		zIndex = 2,
	})

	-- Kayarak giriyor: soldan ice. Aniden beliren bir kart, goz onu
	-- yakalayamadan yerine oturuyor.
	card.Position = UDim2.fromOffset(-40, 0)
	Theme.tween(card, { Position = UDim2.fromOffset(0, 0) }, Theme.motion.normal, Enum.EasingStyle.Back)

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
		if slot.Parent == nil then
			return
		end
		-- Cikarken de kayiyor: geldigi yone geri. Yalnizca solmak,
		-- ustuste binen bildirimlerde hangi kartin gittigini
		-- belirsiz birakiyordu.
		Theme.tween(card, { Position = UDim2.fromOffset(-30, 0) }, 0.35)
		local tween = TweenService:Create(
			card,
			TweenInfo.new(0.35),
			{ BackgroundTransparency = 1 }
		)
		tween:Play()
		tween.Completed:Once(function()
			slot:Destroy()
		end)
	end)
end

return Notify
