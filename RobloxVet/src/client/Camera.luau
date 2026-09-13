--!strict
-- Camera.luau
-- ===========
-- Birinci sahis kamera: sallanma (bobbing), kosma ve FOV. Butun sayilar
-- data/settings.json'dan geliyor.
--
-- Oyun YALNIZCA birinci sahis. Ucuncu sahis secenegi bilerek yok:
-- muayene masasina egilip hayvana bakmak oyunun merkezinde ve o an
-- kameranin omuz ustunde olmasi isi uzaklastiriyor. Sallanma genlikleri
-- de buna gore DUSURULDU - birinci sahista ayni genlik ucuncu sahistan
-- cok daha guclu hissettiriyor.
--
-- Sallanmanin matematigi
-- ----------------------
-- Yuruyen bir insanin govdesi her ADIMDA bir kez inip kalkar, ama
-- agirligi bir saga bir sola gecer. Yani DIKEY bilesen adim frekansinin
-- IKI KATI, YATAY bilesen adim frekansindadir. Bu oran 2:1 olmazsa
-- yuruyus "kayiyor" gibi hissettirir - camera bobbing'i kotu yapan sey
-- neredeyse her zaman budur.
--
-- Sallanma hiza baglı: dururken tamamen kesiliyor. Dururken devam eden
-- bir sallanma, oyuncuyu birkac dakikada rahatsiz eder.
--
-- Neden RenderStepped ve neden CFrame carpimi?
-- -------------------------------------------
-- Kamerayi Roblox'un kendi kamera betigi her karede yeniden yaziyor.
-- Bizim yaptigimiz, o YAZDIKTAN SONRA (BindToRenderStep,
-- Enum.RenderPriority.Camera.Value + 1) sonucun UZERINE bir ofset
-- carpmak. Kamerayi tamamen devralmak, Roblox'un zoom/carpisma
-- davranisini da yeniden yazmak demek olurdu.

local Players = game:GetService("Players")
local RunService = game:GetService("RunService")
local UserInputService = game:GetService("UserInputService")
local Workspace = game:GetService("Workspace")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local settingsData = require(Data.settings)

local Camera = {}

local player = Players.LocalPlayer
local config = settingsData.camera
local movement = settingsData.movement

local current = {
	bobbing = settingsData.defaults.bobbing,
	fieldOfView = settingsData.defaults.fieldOfView,
	sprintEnabled = settingsData.defaults.sprintEnabled,
}

local phase = 0
local landing = 0
local smoothedSpeed = 0
local smoothedFov = current.fieldOfView
local sprinting = false
local bound = false

local RENDER_NAME = "VeterinerKamera"

local function humanoidOf(): (Humanoid?, BasePart?)
	local character = player.Character
	if character == nil then
		return nil, nil
	end
	local humanoid = character:FindFirstChildOfClass("Humanoid")
	local root = character:FindFirstChild("HumanoidRootPart")
	if humanoid == nil or root == nil or not root:IsA("BasePart") then
		return nil, nil
	end
	return humanoid, root
end

--- Oyun YALNIZCA birinci sahis. Kamerayi elle konumlandirmak yerine
--- Roblox'un kendi kilidini kullaniyoruz: fare tekerlegi ve carpisma
--- davranisi boylece bozulmuyor.
local function applyPerspective()
	player.CameraMode = Enum.CameraMode.LockFirstPerson
	player.CameraMinZoomDistance = 0.5
	player.CameraMaxZoomDistance = 0.5
end

local function applyWalkSpeed()
	local humanoid = humanoidOf()
	if humanoid == nil then
		return
	end
	local wantsSprint = sprinting and current.sprintEnabled
	humanoid.WalkSpeed = if wantsSprint then movement.sprintSpeed else movement.walkSpeed
end

local function step(dt: number)
	local camera = Workspace.CurrentCamera
	if camera == nil then
		return
	end

	local humanoid, root = humanoidOf()
	local speed = 0
	if humanoid ~= nil and root ~= nil then
		-- Yatay hiz: zipla/dusme dikey bileseni sallanmayi tetiklemesin.
		speed = Vector3.new(root.AssemblyLinearVelocity.X, 0, root.AssemblyLinearVelocity.Z).Magnitude
		if humanoid:GetState() == Enum.HumanoidStateType.Freefall then
			speed = 0
		end
	end

	-- Hizi yumusatiyoruz: ani durup kalkmalarda sallanma zıplamasin.
	smoothedSpeed += (speed - smoothedSpeed) * math.clamp(config.smoothing * dt, 0, 1)

	local sprintRatio = 0
	if movement.sprintSpeed > movement.walkSpeed then
		sprintRatio = math.clamp(
			(smoothedSpeed - movement.walkSpeed) / (movement.sprintSpeed - movement.walkSpeed),
			0,
			1
		)
	end

	-- FOV: kosarken aciliyor. Hiz hissini veren sey buyuk olcude bu.
	local targetFov = current.fieldOfView + config.sprintFovBoost * sprintRatio
	smoothedFov += (targetFov - smoothedFov) * math.clamp(config.fovSmoothing * dt, 0, 1)
	camera.FieldOfView = smoothedFov

	local moving = smoothedSpeed > config.minSpeedForBob
	if moving then
		local frequency = config.walkFrequency + (config.sprintFrequency - config.walkFrequency) * sprintRatio
		phase += dt * frequency
	else
		-- Durunca fazi sifira degil, en yakin "ayak yerde" noktasina
		-- getiriyoruz: kamera zıplayarak degil, yumusak duruyor.
		phase += dt * 2
	end

	-- Inisin ardindan kisa bir cokme. Zeminle temasin agirligini veriyor.
	landing = math.max(0, landing - dt * config.landingRecover)

	local strength = current.bobbing * (if moving then math.min(smoothedSpeed / movement.walkSpeed, 1.4) else 0)

	-- DIKEY: adim frekansinin iki kati. YATAY: adim frekansinda.
	local vertical = math.sin(phase * 2) * config.verticalAmplitude * strength - landing
	local horizontal = math.cos(phase) * config.horizontalAmplitude * strength
	local roll = math.rad(math.cos(phase) * config.rollDegrees) * strength

	-- Birinci sahiste kamera goz hizasinda; ucuncu sahiste Roblox'un
	-- kendi konumu korunuyor, yalnizca ofset ekleniyor.
	local offset = CFrame.new(horizontal, vertical, 0) * CFrame.Angles(0, 0, roll)
	camera.CFrame = camera.CFrame * offset
end

--- Sunucudan gelen ayarlari uygular.
function Camera.apply(settings: any)
	if settings == nil then
		return
	end
	if type(settings.bobbing) == "number" then
		current.bobbing = settings.bobbing
	end
	if type(settings.fieldOfView) == "number" then
		current.fieldOfView = settings.fieldOfView
	end
	if type(settings.sprintEnabled) == "boolean" then
		current.sprintEnabled = settings.sprintEnabled
	end
	applyPerspective()
	applyWalkSpeed()
end

function Camera.settings(): any
	return table.clone(current)
end

function Camera.start()
	if bound then
		return
	end
	bound = true

	applyPerspective()

	-- Roblox'un kamera betiginden SONRA calis: yoksa yazdigimiz ofseti
	-- o ezer.
	RunService:BindToRenderStep(RENDER_NAME, Enum.RenderPriority.Camera.Value + 1, step)

	UserInputService.InputBegan:Connect(function(input: InputObject, processed: boolean)
		if processed then
			return
		end
		if input.KeyCode == Enum.KeyCode.LeftShift or input.KeyCode == Enum.KeyCode.RightShift then
			sprinting = true
			applyWalkSpeed()
		end
	end)
	UserInputService.InputEnded:Connect(function(input: InputObject)
		if input.KeyCode == Enum.KeyCode.LeftShift or input.KeyCode == Enum.KeyCode.RightShift then
			sprinting = false
			applyWalkSpeed()
		end
	end)

	local function hookCharacter(character: Model)
		local humanoid = character:WaitForChild("Humanoid", 10)
		if humanoid == nil or not humanoid:IsA("Humanoid") then
			return
		end
		applyWalkSpeed()
		-- Yere inis: kamerayi kisa sureligine cokturuyoruz.
		humanoid.StateChanged:Connect(function(_old, new)
			if new == Enum.HumanoidStateType.Landed then
				landing = config.landingDip * current.bobbing
			end
		end)
	end

	if player.Character ~= nil then
		hookCharacter(player.Character)
	end
	player.CharacterAdded:Connect(hookCharacter)
end

return Camera
