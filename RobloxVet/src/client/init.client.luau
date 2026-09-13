--!strict
-- init.client.luau
-- ================
-- Istemcinin TEK giris noktasi. Panelleri kurar, remote'lari dinler,
-- kisayollari baglar.
--
-- Istemci OYUN KURALI CALISTIRMAZ
-- -------------------------------
-- Burada hicbir para, seviye, teshis ya da ilerleme hesabi yok. Istemci
-- sunucudan gelen goruntuyu cizer ve niyet yollar. Bir panelin bir
-- dugmeyi "kilitli" gostermesi kolaylik; gercek denetim sunucuda.
--
-- Tek istisna KAMERA: kamera tamamen istemcinin isi, cunku sunucunun
-- oyuncunun ne gordugu hakkinda soyleyecegi bir sey yok. Ayarlar yine de
-- sunucuda saklaniyor ki oyuncu baska bir cihazda da ayni deneyimi bulsun.

local Players = game:GetService("Players")
local UserInputService = game:GetService("UserInputService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Audio = require(Shared.Audio)
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Camera = require(script.Camera)
local Chart = require(script.Chart)
local DiagnosisPanel = require(script.DiagnosisPanel)
local Fps = require(script.Fps)
local Hud = require(script.Hud)
local LobbyPanel = require(script.LobbyPanel)
local Notify = require(script.Notify)
local ProgressPanel = require(script.ProgressPanel)
local SettingsPanel = require(script.SettingsPanel)
local ShopPanel = require(script.ShopPanel)
local SurgeryPanel = require(script.SurgeryPanel)
local Theme = require(script.Theme)
local ToolBar = require(script.ToolBar)
local TreatmentPanel = require(script.TreatmentPanel)

local player = Players.LocalPlayer
local playerGui = player:WaitForChild("PlayerGui")

--- Iki ayri ScreenGui: oyun arayuzu ve lobi. Tek tek panel gizlemek
--- yerine `Enabled` cevirmek, bir panelin unutulup ekranda kalmasini
--- imkansiz kiliyor.
local screen = Instance.new("ScreenGui")
screen.Name = "VeterinerArayuz"
screen.ResetOnSpawn = false
screen.IgnoreGuiInset = true
screen.ZIndexBehavior = Enum.ZIndexBehavior.Sibling
screen.Enabled = false
screen.Parent = playerGui

local lobbyScreen = Instance.new("ScreenGui")
lobbyScreen.Name = "VeterinerLobi"
lobbyScreen.ResetOnSpawn = false
lobbyScreen.IgnoreGuiInset = true
lobbyScreen.ZIndexBehavior = Enum.ZIndexBehavior.Sibling
lobbyScreen.Enabled = false
lobbyScreen.Parent = playerGui

Hud.create(screen)
Chart.create(screen)
DiagnosisPanel.create(screen)
TreatmentPanel.create(screen)
ToolBar.create(screen)
ShopPanel.create(screen)
SurgeryPanel.create(screen)
ProgressPanel.create(screen)
Fps.create(screen)
SettingsPanel.create(screen)
Notify.create(screen)
LobbyPanel.create(lobbyScreen)

Camera.start()

-- Butun hazir sesleri bir kez yukle ve tutmayanlari Output'a raporla.
-- Bu depoyu ureten ortamda Roblox'a erisim yoktu; hangi rbxasset yolunun
-- gecerli oldugu ancak burada ortaya cikiyor.
Audio.preload()
-- Muzik once lobi parcasi; klinige gecince degisiyor.
Audio.music("lobby")

-- Kisayol serisi ekranin altinda: oyuncunun tuslari tahmin etmesi
-- gerekmesin.
local hints = Theme.label({
	name = "Kisayollar",
	text = Loc.get("hud.keys"),
	size = UDim2.new(0, 760, 0, 18),
	position = UDim2.new(0.5, 0, 1, -88),
	anchor = Vector2.new(0.5, 1),
	textSize = 12,
	align = Enum.TextXAlignment.Center,
	color = Theme.color.textDim,
	parent = screen,
})
hints.TextStrokeTransparency = 0.6

local lastState: any = nil
local lastChart: any = nil

local function redraw()
	Hud.update(lastState)
	ToolBar.update(lastState)
	ShopPanel.update(lastState)
	Chart.update(lastChart)
	DiagnosisPanel.update(lastChart)
	TreatmentPanel.update(lastChart, lastState)
end

Net.get("LobbyState").OnClientEvent:Connect(function(state: any)
	lobbyScreen.Enabled = true
	screen.Enabled = false
	Audio.music("lobby")
	LobbyPanel.update(state)
end)

Net.get("StateUpdate").OnClientEvent:Connect(function(state: any)
	-- Ilk oyun durumu geldi: lobi bitti, klinik basladi.
	lobbyScreen.Enabled = false
	screen.Enabled = true
	lastState = state
	-- Kayitli kamera ayarlari durumla birlikte geliyor.
	if state ~= nil and state.settings ~= nil then
		SettingsPanel.load(state.settings)
	end
	redraw()
end)

Net.get("ChartUpdate").OnClientEvent:Connect(function(chart: any)
	lastChart = chart
	-- Acil vakada muzik gerilime geciyor, bitince geri donuyor.
	Audio.music(if chart ~= nil and chart.emergency == true then "emergency" else "clinic")
	redraw()
end)

Net.get("ProgressUpdate").OnClientEvent:Connect(function(progress: any)
	ProgressPanel.update(progress)
end)

Net.get("Notify").OnClientEvent:Connect(function(kind: string, messageKey: string, args: { any })
	Notify.push(kind, messageKey, args)
end)

Net.get("ToolResult").OnClientEvent:Connect(function(toolId: string, payload: { any })
	-- Ameliyat adimlarinin notu ayni kanaldan geliyor: tek bir remote
	-- tanimi, iki kullanim. Ayirt edici, toolId'nin ozel degeri.
	if toolId == "surgeryStep" then
		SurgeryPanel.grade(tostring(payload[1]))
		return
	end

	if #payload == 0 then
		Notify.push("info", "notify.nothingFound", { Loc.get("tool." .. toolId) })
		return
	end
	for _, nameKey in payload do
		Notify.push("good", "notify.symptomFound", { nameKey })
	end
end)

Net.get("SurgeryBegin").OnClientEvent:Connect(function(schedule: any)
	SurgeryPanel.begin(schedule)
end)

Net.get("SurgeryEnd").OnClientEvent:Connect(function(_passed: boolean, _score: number)
	SurgeryPanel.finish()
end)

UserInputService.InputBegan:Connect(function(input: InputObject, processed: boolean)
	if processed then
		return
	end
	local key = input.KeyCode
	if key == Enum.KeyCode.B then
		ShopPanel.toggle()
	elseif key == Enum.KeyCode.G then
		ProgressPanel.toggle()
	elseif key == Enum.KeyCode.O then
		SettingsPanel.toggle()
	elseif key == Enum.KeyCode.E then
		Net.toServer("CallNext")
	elseif key == Enum.KeyCode.T then
		Net.toServer("Discharge")
	end
end)

print("[Veteriner] istemci hazir.")
