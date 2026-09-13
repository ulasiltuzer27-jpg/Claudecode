--!strict
-- Touch.luau
-- ==========
-- Dokunmatik ekran kontrolleri.
--
-- Neden var?
-- ----------
-- Butun kontroller klavyedeydi: E hastayi al, T taburcu, R numune,
-- B magaza, G ilerleme, O ayarlar. Roblox oyuncularinin cogunlugu
-- telefonda ve onlar icin oyun OYNANAMAZ durumdaydi - baslatip
-- bakabiliyor, hicbir sey yapamiyorlardi.
--
-- Ne yapiyor, ne yapmiyor?
-- ------------------------
-- Yalnizca klavyedeki eylemleri ekrana tasiyor. Yeni bir oyun kurali
-- ya da dokunmatige ozel bir kolaylik EKLEMIYOR: dokunmatik oyuncu
-- klavyeli oyuncuyla ayni oyunu oynuyor.
--
-- Mini-oyunlar zaten tiklama tabanli (`ToolGamePanel` fare/dokunus
-- ayrimi yapmiyor), alet kapaklari da tiklanabilir; bu yuzden burada
-- yalnizca TUS karsiliklari var.
--
-- Dugmeler yalnizca `TouchEnabled` ve klavye YOKKEN kuruluyor. Hem
-- dokunmatik hem klavyesi olan bir cihazda (2'si 1 arada dizustu)
-- ekrani dugmelerle doldurmak, sorunu olmayan oyuncuya sorun
-- eklemek olurdu.

local UserInputService = game:GetService("UserInputService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local Touch = {}

local root: Frame? = nil

--- Dokunmatik kontrollere ihtiyac var mi?
--- Klavye varsa yok: oyuncunun zaten calisan bir yolu var.
function Touch.needed(): boolean
	return UserInputService.TouchEnabled and not UserInputService.KeyboardEnabled
end

-- Alet cubugu ekranin alt ortasinda (ToolBar), koğuş karti sol altta.
-- Eylem dugmeleri SAG ALTTA: bas parmagin dogal olarak durdugu yer ve
-- orasi (alet cubugu sola kaydirildigi icin) bos.
local BUTTONS = {
	{ key = "touch.callNext", remote = "CallNext", color = "accent" },
	{ key = "touch.discharge", remote = "Discharge", color = "good" },
	{ key = "touch.sample", remote = "AnalyzeSample", color = "xp" },
}

local MENUS = {
	{ key = "touch.shop", panel = "shop" },
	{ key = "touch.progress", panel = "progress" },
	{ key = "touch.settings", panel = "settings" },
}

--- `open(panelName)` istemcinin panel acma islevini disaridan aliyor:
--- `Touch` modulunun panelleri require etmesi dairesel bagimlilik
--- olurdu (paneller de Theme'i, Theme'i herkes kullaniyor).
function Touch.create(parent: ScreenGui, open: (string) -> ())
	if root ~= nil or not Touch.needed() then
		return
	end

	local frame = Theme.frame({
		name = "DokunmatikKontroller",
		-- ui:katman — saydam kapsayici; icindeki dugmeler kendi
		-- yuvalarini kapliyor, kapsayici bir yuva kaplamiyor.
		size = UDim2.fromOffset(150, 214),
		position = UDim2.new(1, -Theme.space.lg, 1, -Theme.space.lg),
		anchor = Vector2.new(1, 1),
		transparency = 1,
		radius = 0,
		parent = parent,
	})
	local list = Theme.list(frame, Theme.space.sm)
	list.HorizontalAlignment = Enum.HorizontalAlignment.Right
	list.VerticalAlignment = Enum.VerticalAlignment.Bottom

	for index, entry in MENUS do
		Theme.button({
			name = entry.panel,
			text = Loc.get(entry.key),
			size = UDim2.fromOffset(150, 34),
			color = Theme.color.panel,
			textSize = Theme.size.micro,
			layoutOrder = index,
			parent = frame,
			onClick = function()
				open(entry.panel)
			end,
		})
	end

	for index, entry in BUTTONS do
		Theme.button({
			name = entry.remote,
			text = Loc.get(entry.key),
			size = UDim2.fromOffset(150, 44),
			color = (Theme.color :: any)[entry.color],
			textSize = Theme.size.small,
			layoutOrder = #MENUS + index,
			parent = frame,
			onClick = function()
				Net.toServer(entry.remote)
			end,
		})
	end

	root = frame
end

--- Dokunmatik dugmeler kuruldu mu? (SelfTest'in istemci tarafi yok;
--- bunu `init.client` gunlugune yaziyor ki cihazda ne oldugu Output'tan
--- gorulebilsin.)
function Touch.active(): boolean
	return root ~= nil
end

return Touch
