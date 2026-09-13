--!strict
-- Crosshair.luau
-- ==============
-- Ekranin ortasindaki baglama duyarli nisangah.
--
-- Neden gerekli?
-- --------------
-- Birinci sahiste "neye baktigim" bilgisi kaybolur: ekranin ortasinda
-- bir isaret yoksa oyuncu masaya mi yoksa duvara mi baktigini
-- denemeyerek ogrenir. Bos haldeyken kucuk bir nokta, etkilesim
-- menzilinde acilan bir halka ve eylemin adi.
--
-- Menzili kim soyluyor?
-- ---------------------
-- ProximityPromptService. Klinikteki istasyonlarda zaten ProximityPrompt
-- var (init.server.luau kuruyor); onlarin gorunme/kaybolma olaylarini
-- dinlemek, kendi raycast'imizi yazmaktan hem ucuz hem de DOGRU:
-- menzil tanimi tek yerde kaliyor ve ikisi birbirinden kayamiyor.

local ProximityPromptService = game:GetService("ProximityPromptService")

local Theme = require(script.Parent.Theme)

local Crosshair = {}

local widgets: any = nil
local activePrompts = 0

local DOT = 4
local RING_IDLE = 14
local RING_ACTIVE = 26

function Crosshair.create(parent: ScreenGui)
	local root = Theme.frame({
		name = "Nisangah",
		-- ui:katman — saydam, ekranin tam ortasinda duran nisan
		-- katmani. Bir yuva kaplamiyor; acilan paneller onun
		-- uzerine geliyor ve nisangah zaten o sirada gizleniyor.
		size = UDim2.fromOffset(160, 80),
		position = UDim2.fromScale(0.5, 0.5),
		anchor = Vector2.new(0.5, 0.5),
		transparency = 1,
		radius = 0,
		parent = parent,
		zIndex = 1,
	})

	local dot = Theme.frame({
		name = "Nokta",
		size = UDim2.fromOffset(DOT, DOT),
		position = UDim2.fromScale(0.5, 0.5),
		anchor = Vector2.new(0.5, 0.5),
		color = Theme.color.text,
		transparency = 0.15,
		radius = DOT,
		parent = root,
		zIndex = 3,
	})

	local ring = Theme.frame({
		name = "Halka",
		size = UDim2.fromOffset(RING_IDLE, RING_IDLE),
		position = UDim2.fromScale(0.5, 0.5),
		anchor = Vector2.new(0.5, 0.5),
		transparency = 1,
		radius = RING_IDLE,
		parent = root,
		zIndex = 2,
	})
	local stroke = Theme.stroke(ring, Theme.color.text, 1.5)
	stroke.Transparency = 0.55

	local action = Theme.label({
		name = "Eylem",
		text = "",
		size = UDim2.new(1, 0, 0, 16),
		position = UDim2.new(0.5, 0, 0.5, 24),
		anchor = Vector2.new(0.5, 0),
		textSize = Theme.size.micro,
		bold = true,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.accentGlow,
		parent = root,
		zIndex = 3,
	})
	action.TextStrokeTransparency = 0.6
	action.TextTransparency = 1

	widgets = { root = root, dot = dot, ring = ring, stroke = stroke, action = action }

	-- Istem gorununce halka aciliyor, kaybolunca kapaniyor. Sayac
	-- tutuyoruz: iki istem ust uste gorunurse birinin kapanmasi
	-- digerini de kapatmasin.
	ProximityPromptService.PromptShown:Connect(function(prompt: ProximityPrompt)
		activePrompts += 1
		Crosshair.setAction(prompt.ActionText)
	end)
	ProximityPromptService.PromptHidden:Connect(function()
		activePrompts = math.max(0, activePrompts - 1)
		if activePrompts == 0 then
			Crosshair.setAction("")
		end
	end)
end

--- Eylem adini gosterir. Bos dize nisangahi bos haline dondurur.
function Crosshair.setAction(text: string)
	if widgets == nil then
		return
	end
	local active = text ~= ""
	local size = if active then RING_ACTIVE else RING_IDLE
	Theme.tween(widgets.ring, { Size = UDim2.fromOffset(size, size) }, Theme.motion.quick)
	Theme.tween(widgets.stroke, {
		Transparency = if active then 0.1 else 0.55,
		Color = if active then Theme.color.accentGlow else Theme.color.text,
	}, Theme.motion.quick)
	Theme.tween(widgets.dot, {
		BackgroundTransparency = if active then 0.6 else 0.15,
	}, Theme.motion.quick)

	if active then
		widgets.action.Text = text
	end
	Theme.tween(widgets.action, { TextTransparency = if active then 0 else 1 }, Theme.motion.quick)
end

--- Alet kullanildiginda nisangah bir kez aciliyor: "gitti" geri
--- bildirimi, alet cubuguna bakmadan ekranin ortasinda.
function Crosshair.kick()
	if widgets == nil then
		return
	end
	widgets.ring.Size = UDim2.fromOffset(RING_ACTIVE + 14, RING_ACTIVE + 14)
	local target = if activePrompts > 0 then RING_ACTIVE else RING_IDLE
	Theme.tween(widgets.ring, { Size = UDim2.fromOffset(target, target) }, Theme.motion.normal)
end

function Crosshair.setVisible(visible: boolean)
	if widgets ~= nil then
		widgets.root.Visible = visible
	end
end

return Crosshair
