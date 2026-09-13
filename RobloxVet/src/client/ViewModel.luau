--!strict
-- ViewModel.luau
-- ==============
-- Birinci sahis el ve alet modeli.
--
-- Neden var?
-- ----------
-- Oyun yalnizca birinci sahis (Camera.luau -> LockFirstPerson) ama
-- asagi bakinca hicbir sey gorunmuyordu: ne el, ne alet. Birinci sahis
-- oyunu birinci sahis yapan sey buyuk olcude elini gormek. Alet
-- kullandiginda elin uzanmasi, "alet calisti" geri bildiriminin
-- kendisi oluyor - yazi okumaya gerek kalmiyor.
--
-- Nasil cizilyor?
-- ---------------
-- Parcalar `Workspace.CurrentCamera` altinda duruyor: kameranin
-- cocuklari SUNUCUYA REPLIKE OLMUYOR, yani baska oyuncular bizim
-- ellerimizi gormuyor ve sunucuda hicbir sey yaratilmiyor.
--
-- Her parca ANKORLU ve her karede elle konumlandiriliyor. Motor6D ve
-- Animator ile kurmak da mumkundu; ama o yol bir Animation asset'i
-- gerektiriyor ve bu depoyu ureten ortamda Roblox'a erisim yoktu -
-- yuklenemeyen bir animasyon ID'si sessizce hicbir sey oynatmaz.
-- Elle konumlandirma asset'siz calisiyor ve dogrulanabiliyor.
--
-- Sallanma nereden geliyor?
-- -------------------------
-- `Camera.motion()`den. Ayri bir faz saymiyoruz: iki sayac birkac
-- saniyede faz farkina duser ve el kameradan bagimsiz "yuzer".

local Players = game:GetService("Players")
local RunService = game:GetService("RunService")
local UserInputService = game:GetService("UserInputService")
local Workspace = game:GetService("Workspace")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local settingsData = require(Data.settings)
local toolData = require(Data.tools)

local Camera = require(script.Parent.Camera)

local ViewModel = {}

local player = Players.LocalPlayer
local config = settingsData.viewModel

local RENDER_NAME = "VeterinerElModeli"

type Piece = { part: BasePart, offset: CFrame, group: string }

local model: Model? = nil
local pieces: { Piece } = {}
local toolPieces: { [string]: { Piece } } = {}
local toolById: { [string]: any } = {}
for _, tool in toolData.tools do
	toolById[tool.id] = tool
end

local enabled = if settingsData.defaults.viewModel == nil then true else settingsData.defaults.viewModel
local started = false
local activeTool = ""
local swayX, swayY = 0, 0
local useTime = -1
local useMotion = "press"
local breath = 0

local function toColor(channels: { number }): Color3
	return Color3.fromRGB(channels[1], channels[2], channels[3])
end

--- Gorunum parcasi. Carpismaz, isin tutmaz, golge dusurmez: bunlar
--- birinci sahis modelinde yalnizca kare yakar ve oynanisi bozar
--- (elin kendi golgesi duvara vurur, raycast ele carpar).
local function piece(name: string, size: Vector3, offset: CFrame, color: Color3, group: string, shape: Enum.PartType?): BasePart
	local part = Instance.new("Part")
	part.Name = name
	part.Size = size
	part.Color = color
	part.Material = Enum.Material.SmoothPlastic
	part.Anchored = true
	part.CanCollide = false
	part.CanQuery = false
	part.CanTouch = false
	part.CastShadow = false
	part.Massless = true
	part.TopSurface = Enum.SurfaceType.Smooth
	part.BottomSurface = Enum.SurfaceType.Smooth
	if shape ~= nil then
		part.Shape = shape
	end
	part.Parent = model
	table.insert(pieces, { part = part, offset = offset, group = group })
	return part
end

--- Bir el: on kol, avuc ve uc parmak. Detayi burada bitiriyoruz;
--- birinci sahiste elin ancak bu kadari gorunuyor ve her parmak
--- eklemi cizmek gorunmeyen bir seye kare harcamak olurdu.
local function buildHand(group: string, side: number, base: CFrame)
	local skin = toColor(config.skinColor)
	local sleeve = toColor(config.sleeveColor)
	local glove = toColor(config.gloveColor)

	piece(group .. "Kol", Vector3.new(0.42, 0.42, 1.5), base * CFrame.new(0, 0, 0.75), sleeve, group)
	piece(group .. "Manset", Vector3.new(0.46, 0.46, 0.2), base * CFrame.new(0, 0, 0.02), glove, group)
	piece(group .. "Bilek", Vector3.new(0.36, 0.36, 0.3), base * CFrame.new(0, 0, -0.16), skin, group)
	piece(group .. "Avuc", Vector3.new(0.4, 0.22, 0.52), base * CFrame.new(0, 0, -0.52), skin, group)

	for index = 1, 3 do
		local spread = (index - 2) * 0.12 * side
		piece(
			group .. "Parmak" .. index,
			Vector3.new(0.1, 0.12, 0.34),
			base * CFrame.new(spread, -0.02, -0.92) * CFrame.Angles(math.rad(-8 * index), 0, 0),
			skin,
			group
		)
	end
	piece(
		group .. "Basparmak",
		Vector3.new(0.12, 0.12, 0.26),
		base * CFrame.new(0.18 * side, -0.04, -0.66) * CFrame.Angles(0, math.rad(28 * side), 0),
		skin,
		group
	)
end

--- Aletin govdesi. `viewModel.body`/`tip` olculerinden biri sifirsa o
--- parca hic kurulmuyor: "gozle muayene"nin elinde alet yok.
local function buildTool(tool: any)
	local spec = tool.viewModel
	if spec == nil then
		return
	end
	local before = #pieces
	local color = toColor(tool.color)
	local tipColor = toColor(spec.tipColor)

	local body = spec.body
	if body[1] > 0 and body[2] > 0 and body[3] > 0 then
		piece(
			tool.id .. "Govde",
			Vector3.new(body[1], body[2], body[3]),
			CFrame.new(0, 0, -body[3] * 0.5),
			color,
			"tool"
		)
	end
	local tip = spec.tip
	if tip[1] > 0 and tip[2] > 0 and tip[3] > 0 then
		piece(
			tool.id .. "Uc",
			Vector3.new(tip[1], tip[2], tip[3]),
			CFrame.new(0, 0, -body[3] - tip[3] * 0.5),
			tipColor,
			"tool"
		)
	end

	-- Bu aletin parcalarini ayri tutuyoruz: alet degisince yalnizca
	-- secili olan gorunur oluyor, digerleri gizleniyor. Her secimde
	-- yeniden yaratmak, secim sirasinda bir kare bosluk demekti.
	local list = {}
	for index = before + 1, #pieces do
		table.insert(list, pieces[index])
		pieces[index].part.Transparency = 1
	end
	toolPieces[tool.id] = list
end

local function build()
	if model ~= nil then
		return
	end
	local camera = Workspace.CurrentCamera
	if camera == nil then
		return
	end

	model = Instance.new("Model")
	;(model :: Model).Name = "ElModeli"
	;(model :: Model).Parent = camera

	local right = config.rightOffset
	local left = config.leftOffset
	buildHand("right", 1, CFrame.new(right[1], right[2], right[3]) * CFrame.Angles(math.rad(-8), math.rad(-6), 0))
	buildHand("left", -1, CFrame.new(left[1], left[2], left[3]) * CFrame.Angles(math.rad(-12), math.rad(10), 0))

	for _, tool in toolData.tools do
		buildTool(tool)
	end
end

--- Alet kullanim animasyonunun o anki donusumu. `motion` alanina gore
--- degisiyor: iğne BASTIRIYOR, otoskop UZANIYOR, rontgen SUPURUYOR.
local function useTransform(progress: number): CFrame
	-- 0 -> 1 -> 0 arasi yumusak bir yay: animasyon basta ve sonda
	-- durgun, ortada en guclu.
	local swing = math.sin(math.clamp(progress, 0, 1) * math.pi)
	if useMotion == "press" then
		return CFrame.new(0, -0.18 * swing, -0.42 * swing) * CFrame.Angles(math.rad(-16 * swing), 0, 0)
	elseif useMotion == "reach" then
		return CFrame.new(0, 0.06 * swing, -0.7 * swing) * CFrame.Angles(math.rad(-6 * swing), 0, 0)
	elseif useMotion == "twist" then
		return CFrame.new(0, -0.08 * swing, -0.2 * swing) * CFrame.Angles(0, 0, math.rad(48 * swing))
	elseif useMotion == "sweep" then
		return CFrame.new(0.34 * math.sin(progress * math.pi * 2), 0, -0.2 * swing)
			* CFrame.Angles(0, math.rad(18 * math.sin(progress * math.pi * 2)), 0)
	end
	-- "point": alet yok, el isaret ediyor.
	return CFrame.new(0, 0.1 * swing, -0.5 * swing) * CFrame.Angles(math.rad(-18 * swing), 0, 0)
end

local function step(dt: number)
	local camera = Workspace.CurrentCamera
	if model == nil or camera == nil then
		return
	end

	-- ── Savrulma (sway) ───────────────────────────────────────────────
	-- Fare hareketi eli GECIKMELI takip ediyor: kamera aninda donuyor,
	-- el arkasindan yetisiyor. Birinci sahisi "agir" hissettiren sey bu.
	local delta = UserInputService:GetMouseDelta()
	local response = math.clamp(config.swayResponse * dt, 0, 1)
	swayX += (-delta.X * 0.06 - swayX) * response
	swayY += (-delta.Y * 0.06 - swayY) * response
	swayX = math.clamp(swayX, -config.swayDegrees, config.swayDegrees)
	swayY = math.clamp(swayY, -config.swayDegrees, config.swayDegrees)

	-- ── Yuruyus sallanmasi ────────────────────────────────────────────
	local motion = Camera.motion()
	local bob = config.bobScale * motion.strength
	local vertical = math.sin(motion.phase * 2) * 0.1 * bob - motion.landing * 0.4
	local horizontal = math.cos(motion.phase) * 0.08 * bob
	local roll = math.rad(math.cos(motion.phase) * 1.6) * bob

	-- ── Nefes ─────────────────────────────────────────────────────────
	breath += dt * config.breathSpeed
	local idle = CFrame.new(0, math.sin(breath) * config.breathAmount, 0)
		* CFrame.Angles(math.rad(math.sin(breath * 0.6) * 0.7), 0, 0)

	local sway = CFrame.Angles(math.rad(swayY), math.rad(swayX), math.rad(roll))
	local walk = CFrame.new(horizontal, vertical, 0)

	local use = CFrame.identity
	if useTime >= 0 then
		useTime += dt
		local progress = useTime / config.useSeconds
		if progress >= 1 then
			useTime = -1
		else
			use = useTransform(progress)
		end
	end

	local base = camera.CFrame * walk * sway * idle

	for _, entry in pieces do
		local groupCF = if entry.group == "left" then CFrame.identity else use
		entry.part.CFrame = base * groupCF * entry.offset
	end

	-- Secili aletin parcalari sag elin ONUNDE duruyor: aletin kendi
	-- ofseti ele gore degil, elin tuttugu noktaya gore.
	local hold = base * use * CFrame.new(
		config.rightOffset[1],
		config.rightOffset[2] + 0.05,
		config.rightOffset[3] - 0.8
	)
	local list = toolPieces[activeTool]
	if list ~= nil then
		for _, entry in list do
			entry.part.CFrame = hold * entry.offset
		end
	end
end

--- Yerel oyuncunun GERCEK govdesi gizleniyor. Roblox birinci sahiste
--- yalnizca kafayi gizliyor; asagi bakinca havada yuzen bir govde
--- gormek birinci sahis hissini bozuyor. `LocalTransparencyModifier`
--- her karede sifirlandigi icin surekli yazmak gerekiyor.
local function hideCharacter()
	local character = player.Character
	if character == nil then
		return
	end
	for _, item in character:GetDescendants() do
		if item:IsA("BasePart") then
			item.LocalTransparencyModifier = 1
		elseif item:IsA("Decal") or item:IsA("Texture") then
			item.Transparency = 1
		end
	end
end

local function refreshVisibility()
	for id, list in toolPieces do
		local show = enabled and id == activeTool
		for _, entry in list do
			entry.part.Transparency = if show then 0 else 1
		end
	end
	for _, entry in pieces do
		if entry.group ~= "tool" then
			entry.part.Transparency = if enabled then 0 else 1
		end
	end
end

--- Secili aleti bildirir; el o aletin bicimine gecer.
function ViewModel.setTool(toolId: string)
	if activeTool == toolId then
		return
	end
	activeTool = toolId
	local tool = toolById[toolId]
	useMotion = if tool ~= nil and tool.viewModel ~= nil then tool.viewModel.motion else "press"
	refreshVisibility()
	-- Alet degistirmenin kendisi de kucuk bir hareket: el bir kez
	-- asagi inip cikiyor.
	useTime = 0
end

--- Alet kullanildi: kullanim animasyonunu bastan oynatir.
function ViewModel.playUse()
	useTime = 0
end

function ViewModel.setEnabled(value: boolean)
	enabled = value
	if model ~= nil then
		refreshVisibility()
	end
end

function ViewModel.isEnabled(): boolean
	return enabled
end

function ViewModel.start()
	if started then
		return
	end
	started = true
	build()
	refreshVisibility()

	RunService:BindToRenderStep(RENDER_NAME, Enum.RenderPriority.Camera.Value + 2, function(dt: number)
		if enabled then
			step(dt)
		end
		hideCharacter()
	end)

	player.CharacterAdded:Connect(function()
		task.defer(hideCharacter)
	end)

	-- Kamera degisirse (oyuncu yeniden dogunca olabiliyor) model yeni
	-- kameraya tasinmali; yoksa eski kamerayla birlikte yok oluyor.
	Workspace:GetPropertyChangedSignal("CurrentCamera"):Connect(function()
		local camera = Workspace.CurrentCamera
		if camera == nil then
			return
		end
		if model == nil then
			-- Baslangicta kamera henuz yoksa model kurulamamis olabilir;
			-- kamera gelince kuruluyor. Aksi halde el hic gorunmezdi.
			build()
			refreshVisibility()
			return
		end
		(model :: Model).Parent = camera
	end)
end

return ViewModel
