--!strict
-- Fps.luau
-- ========
-- Kare hizi ve gecikme gostergesi.
--
-- Neden ortalama degil, "son 1 saniyedeki kare sayisi" ve ayrica EN
-- KOTU kare? Ortalama FPS takilmalari gizler: 60 ortalama, arada 12'ye
-- dusen bir oyun "60 FPS" gorunur. Takilmayi gosteren sey son
-- saniyedeki en uzun karedir; gosterge onu da yaziyor.

local Players = game:GetService("Players")
local RunService = game:GetService("RunService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)

local Theme = require(script.Parent.Theme)

local Fps = {}

local player = Players.LocalPlayer

local widgets: any = nil
local frames = 0
local elapsed = 0
local worstFrame = 0
local connection: RBXScriptConnection? = nil

local function colorFor(value: number): Color3
	if value >= 50 then
		return Theme.color.good
	elseif value >= 28 then
		return Theme.color.warn
	end
	return Theme.color.bad
end

function Fps.create(parent: ScreenGui)
	local panel = Theme.frame({
		name = "Fps",
		size = UDim2.fromOffset(146, 46),
		position = UDim2.new(1, -16, 0, 78),
		anchor = Vector2.new(1, 0),
		transparency = 0.25,
		radius = 8,
		parent = parent,
	})

	local rate = Theme.label({
		name = "Kare",
		text = "",
		size = UDim2.new(1, -16, 0, 20),
		position = UDim2.fromOffset(8, 4),
		textSize = 15,
		bold = true,
		parent = panel,
	})
	local detail = Theme.label({
		name = "Detay",
		text = "",
		size = UDim2.new(1, -16, 0, 16),
		position = UDim2.fromOffset(8, 24),
		textSize = 11,
		color = Theme.color.textDim,
		parent = panel,
	})

	widgets = { panel = panel, rate = rate, detail = detail }

	connection = RunService.RenderStepped:Connect(function(dt: number)
		frames += 1
		elapsed += dt
		worstFrame = math.max(worstFrame, dt)

		if elapsed < 1 then
			return
		end

		local value = math.floor(frames / elapsed + 0.5)
		-- En uzun kareden turetilen "en kotu FPS": takilmayi gosteren sey bu.
		local worst = if worstFrame > 0 then math.floor(1 / worstFrame + 0.5) else value
		local ping = math.floor(player:GetNetworkPing() * 1000 + 0.5)

		widgets.rate.Text = Loc.get("fps.rate", value)
		widgets.rate.TextColor3 = colorFor(value)
		widgets.detail.Text = Loc.get("fps.detail", worst, ping)

		frames = 0
		elapsed = 0
		worstFrame = 0
	end)
end

function Fps.setVisible(visible: boolean)
	if widgets ~= nil then
		widgets.panel.Visible = visible
	end
end

--- Gosterge kapaliyken sayim da dursun: kapatilan bir seyin her karede
--- is yapmaya devam etmesi anlamsiz.
function Fps.destroy()
	if connection ~= nil then
		connection:Disconnect()
		connection = nil
	end
end

return Fps
