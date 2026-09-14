--!strict
-- Doors.luau
-- ==========
-- ACILAN cift kanatli kapilar: oyuncu kapiyi kendi aciyor.
--
-- Neden yaklasinca degil de BASARAK?
-- ----------------------------------
-- Once yaklasinca kendiliginden aciliyorlardi. Bu, kapiyi bir dekora
-- ceviriyordu: oyuncu kapinin ORADA oldugunu bile fark etmeden
-- geciyordu. Kapiyi acmak kucuk ama gercek bir eylem - klinikte bir
-- odadan digerine gecmenin bir agirligi oluyor.
--
-- Animasyon
-- ---------
-- Kanat MENTESEDEN donuyor (ortadan degil): gercek bir kapi gibi
-- disari savruluyor. Uc sey ayni anda oynuyor:
--   1. kanat menteseden 96 derece aciliyor (`Back` yumusatmasi -
--      sonunda hafifce salinip duruyor, tahta bir kapi gibi),
--   2. kulp once ASAGI donuyor sonra geri geliyor (mandal),
--   3. mandal sesi cikiyor, ardindan kanat sesi.
--
-- Kapanis daha yavas ve `Quad` ile: kapanan kapi savrulmaz.
--
-- Kanatlar CARPISMIYOR
-- --------------------
-- Kapali bir kanat carpisan bir duvar olsaydi, acilma gecikmesinde ya da
-- bir hata durumunda oyuncu kapida SIKISIRDI. Gorunum icin kapi, oynanis
-- icin acik gecit.
--
-- Neden CFrameValue suruyor?
-- --------------------------
-- Bir kanat tek parca degil: govde + cam panel + kulp + mentese. Tween
-- bir Model'i tasiyamiyor, ankorlu parcalara kaynak da islemiyor. Bu
-- yuzden bir `CFrameValue` tweenleniyor ve her degisiminde kanadin
-- butun parcalari o cerceveye gore yeniden konumlaniyor - tek kaynak,
-- parcalar arasi kayma imkansiz.

local Players = game:GetService("Players")
local RunService = game:GetService("RunService")
local TweenService = game:GetService("TweenService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Audio = require(Shared.Audio)
local Build = require(Shared.Build)
local Loc = require(Shared.Loc)

local plan = require(Data.clinic)

local Doors = {}

--- Kapi kurulumu icin gereken her sey. Klinik plani bunu dogrudan
--- karsiliyor; LOBI de ayni kapilari kullanabilsin diye tablo ayri bir
--- tur olarak duruyor - ikinci bir kapi modulu yazmak, ayni hatayi iki
--- yerde duzeltmek demek olurdu.
export type Source = {
	floorY: number,
	doorHeight: number,
	colors: { [string]: { number } },
	walls: { any },
}

type Leaf = {
	driver: CFrameValue,
	closed: CFrame,
	open: CFrame,
	handle: BasePart,
	handleClosed: CFrame,
}

type Door = {
	center: Vector3,
	leaves: { Leaf },
	openNow: boolean,
	prompt: ProximityPrompt,
	anchor: BasePart,
	openedAt: number,
}

local doors: { Door } = {}
local running = false

-- Acik kapi, kimse yakininda degilse kendiliginden kapaniyor. Oyuncunun
-- arkasindan kapiyi kapatmasini BEKLEMEK, kisa surede butun klinigi
-- acik kapili birakirdi.
local AUTO_CLOSE_SECONDS = 6
local AUTO_CLOSE_DISTANCE = 14
local OPEN_SECONDS = 0.5
local CLOSE_SECONDS = 0.7
local HANDLE_SECONDS = 0.16

--- Kulp mandali: once asagi doner, sonra geri gelir. Kapi acilirken de
--- kapanirken de oynuyor - mandala basmadan acilan bir kapi, animasyonu
--- olmayan bir kapidan yalnizca biraz daha iyidir.
local function turnHandle(leaf: Leaf)
	local turned = leaf.handleClosed * CFrame.Angles(math.rad(-32), 0, 0)
	local driver = leaf.driver.Value
	local down = TweenService:Create(
		leaf.handle,
		TweenInfo.new(HANDLE_SECONDS, Enum.EasingStyle.Quad, Enum.EasingDirection.Out),
		{ CFrame = driver * turned }
	)
	down:Play()
	down.Completed:Connect(function()
		TweenService:Create(
			leaf.handle,
			TweenInfo.new(HANDLE_SECONDS * 1.4, Enum.EasingStyle.Quad, Enum.EasingDirection.Out),
			{ CFrame = leaf.driver.Value * leaf.handleClosed }
		):Play()
	end)
end

local function setOpen(door: Door, open: boolean)
	if door.openNow == open then
		return
	end
	door.openNow = open
	door.openedAt = os.clock()
	door.prompt.ActionText = Loc.get(if open then "prompt.doorClose" else "prompt.doorOpen")

	Audio.play("doorLatch", door.anchor)
	for _, leaf in door.leaves do
		turnHandle(leaf)
	end

	-- Kanat mandaldan SONRA hareket ediyor: once kulp doner, sonra kapi
	-- acilir. Ayni anda olsalardi mandalin ne ise yaradigi gorunmezdi.
	task.delay(HANDLE_SECONDS, function()
		Audio.play(if open then "doorOpen" else "doorClose", door.anchor)
		for _, leaf in door.leaves do
			TweenService:Create(
				leaf.driver,
				TweenInfo.new(
					if open then OPEN_SECONDS else CLOSE_SECONDS,
					-- Acilirken `Back`: kanat sonunda hafifce salinip
					-- duruyor. Kapanirken `Quad`: kapanan kapi
					-- savrulmaz, yavaslayarak oturur.
					if open then Enum.EasingStyle.Back else Enum.EasingStyle.Quad,
					Enum.EasingDirection.Out
				),
				{ Value = if open then leaf.open else leaf.closed }
			):Play()
		end
	end)
end

--- Tek bir kapi bosluguna cift kanat + istem kurar.
local function buildDoor(
	parent: Instance,
	center: Vector3,
	horizontal: boolean,
	width: number,
	blueprint: any
)
	local height = blueprint.doorHeight - 0.4
	local leafWidth = width * 0.5 - 0.2
	local thickness = 0.24
	local woodColor = Build.color(blueprint.colors.windowFrame)
	local trimColor = Build.color(blueprint.colors.wallTrim)
	local glassColor = Build.color(blueprint.colors.glass)

	local leaves: { Leaf } = {}
	for side = -1, 1, 2 do
		local offset = if horizontal
			then Vector3.new(side * (leafWidth * 0.5 + 0.06), height * 0.5, 0)
			else Vector3.new(0, height * 0.5, side * (leafWidth * 0.5 + 0.06))
		local base = CFrame.new(center + offset)

		local parts: { { part: BasePart, offset: CFrame } } = {}
		local function add(part: BasePart)
			table.insert(parts, { part = part, offset = base:ToObjectSpace(part.CFrame) })
		end

		add(Build.part({
			name = "Kanat",
			size = if horizontal
				then Vector3.new(leafWidth, height, thickness)
				else Vector3.new(thickness, height, leafWidth),
			cf = base,
			color = woodColor,
			material = Enum.Material.Wood,
			canCollide = false,
			castShadow = false,
			parent = parent,
		}))

		-- Kanadin ust yarisinda cam panel: kapali kapinin ardini gormek,
		-- klinikte nereye gittigini bilmek demek.
		local glass = Build.part({
			name = "KanatCami",
			size = if horizontal
				then Vector3.new(leafWidth * 0.62, height * 0.34, thickness + 0.06)
				else Vector3.new(thickness + 0.06, height * 0.34, leafWidth * 0.62),
			cf = base * CFrame.new(0, height * 0.2, 0),
			color = glassColor,
			material = Enum.Material.Glass,
			canCollide = false,
			castShadow = false,
			parent = parent,
		})
		glass.Transparency = 0.62
		add(glass)

		-- Cam cercevesi: cerceveleri olmayan bir cam, kanada kesilmis
		-- bir delik gibi duruyordu.
		for _, edge in { -1, 1 } do
			add(Build.part({
				name = "CamCercevesi",
				size = if horizontal
					then Vector3.new(leafWidth * 0.66, 0.1, thickness + 0.1)
					else Vector3.new(thickness + 0.1, 0.1, leafWidth * 0.66),
				cf = base * CFrame.new(0, height * 0.2 + edge * height * 0.18, 0),
				color = trimColor,
				canCollide = false,
				castShadow = false,
				parent = parent,
			}))
		end

		-- Alt panel: duz bir dikdortgen "kapi" degil, "levha" gibi
		-- duruyordu.
		add(Build.part({
			name = "KanatPaneli",
			size = if horizontal
				then Vector3.new(leafWidth * 0.7, height * 0.3, thickness + 0.05)
				else Vector3.new(thickness + 0.05, height * 0.3, leafWidth * 0.7),
			cf = base * CFrame.new(0, -height * 0.26, 0),
			color = trimColor,
			canCollide = false,
			castShadow = false,
			parent = parent,
		}))

		-- MENTESE: kanadin hangi kenardan dondugunu gozle gosteren
		-- parca. Animasyonun inandirici olmasi, donme ekseninin
		-- GORUNMESINE bagli.
		local hingeSide = if horizontal
			then Vector3.new(side * leafWidth * 0.5, 0, 0)
			else Vector3.new(0, 0, side * leafWidth * 0.5)
		for _, level in { -0.3, 0.0, 0.3 } do
			add(Build.part({
				name = "Mentese",
				size = Vector3.new(0.16, 0.5, 0.16),
				cf = base * CFrame.new(hingeSide + Vector3.new(0, height * level, 0)),
				color = trimColor,
				material = Enum.Material.Metal,
				canCollide = false,
				castShadow = false,
				parent = parent,
			}))
		end

		-- KULP menteseden UZAK kenarda (gercek kapilarda oyle). Ayrica
		-- kendi basina donuyor, bu yuzden `parts` listesine GIRMIYOR:
		-- kanatla birlikte tasiniyor ama kendi acisini da tutuyor.
		local handleOffset = if horizontal
			then Vector3.new(-side * leafWidth * 0.34, -height * 0.02, thickness * 0.85)
			else Vector3.new(thickness * 0.85, -height * 0.02, -side * leafWidth * 0.34)
		local handle = Build.part({
			name = "KanatKulpu",
			size = Vector3.new(0.16, 0.9, 0.16),
			cf = base * CFrame.new(handleOffset),
			color = trimColor,
			material = Enum.Material.Metal,
			canCollide = false,
			castShadow = false,
			parent = parent,
		})
		local handleLocal = base:ToObjectSpace(handle.CFrame)
		add(Build.part({
			name = "KulpTabani",
			size = Vector3.new(0.3, 0.3, 0.12),
			cf = base * CFrame.new(handleOffset) * CFrame.new(0, 0, -0.1),
			color = trimColor,
			material = Enum.Material.Metal,
			canCollide = false,
			castShadow = false,
			parent = parent,
		}))

		local hinge = base * CFrame.new(hingeSide)
		local openBase = hinge * CFrame.Angles(0, side * math.rad(-96), 0) * hinge:ToObjectSpace(base)

		local driver = Instance.new("CFrameValue")
		driver.Name = "KanatSurucusu"
		driver.Value = base
		driver.Parent = parent
		driver.Changed:Connect(function(value: CFrame)
			for _, entry in parts do
				entry.part.CFrame = value * entry.offset
			end
			-- Kulp kanatla birlikte gidiyor ama kendi donusunu
			-- koruyor: `handleLocal` kanada gore konumu, kulbun
			-- kendi acisi ise `handle.CFrame` uzerinde tweenleniyor.
			handle.CFrame = value * handleLocal
		end)

		table.insert(leaves, {
			driver = driver,
			closed = base,
			open = openBase,
			handle = handle,
			handleClosed = handleLocal,
		})
	end

	-- Istem, kapi bosluğunun ortasinda gorunmez bir parcaya baglaniyor:
	-- kanada baglansaydi kapi acilirken istem de savrulurdu.
	local anchor = Build.part({
		name = "KapiIstemi",
		size = Vector3.new(1, 1, 1),
		cf = CFrame.new(center + Vector3.new(0, blueprint.doorHeight * 0.45, 0)),
		color = Color3.new(1, 1, 1),
		canCollide = false,
		castShadow = false,
		parent = parent,
	})
	anchor.Transparency = 1
	anchor.CanQuery = false

	local prompt = Instance.new("ProximityPrompt")
	prompt.Name = "Kapi"
	prompt.ActionText = Loc.get("prompt.doorOpen")
	prompt.ObjectText = Loc.get("prompt.door")
	prompt.HoldDuration = 0
	prompt.MaxActivationDistance = 10
	prompt.RequiresLineOfSight = false
	prompt.Parent = anchor

	local door: Door = {
		center = center,
		leaves = leaves,
		openNow = false,
		prompt = prompt,
		anchor = anchor,
		openedAt = 0,
	}
	table.insert(doors, door)

	-- Istem KURULUM aninda baglaniyor, `Doors.start()` icinde degil.
	-- Baglanti orada olsaydi ikinci bir bina (Studio yedeginde lobiden
	-- klinige gecis) kurulunca `running` zaten true oldugu icin yeni
	-- kapilar baglanmadan kalir ve HICBIRI acilmazdi.
	prompt.Triggered:Connect(function()
		setOpen(door, not door.openNow)
	end)
end

--- Plandaki her kapiya kanat kurar. `source` verilmezse KLINIK plani
--- kullaniliyor.
function Doors.build(parent: Instance, source: Source?): Model
	local blueprint: any = source or plan
	doors = {}
	local model = Build.model("Kapilar", parent)

	for _, wall in blueprint.walls do
		local horizontal = math.abs(wall.to[2] - wall.from[2]) < 1e-6
		local fixed = if horizontal then wall.from[2] else wall.from[1]
		for _, door in wall.doors or {} do
			local center = if horizontal
				then Vector3.new(door.at, blueprint.floorY, fixed)
				else Vector3.new(fixed, blueprint.floorY, door.at)
			buildDoor(model, center, horizontal, door.width, blueprint)
		end
	end
	return model
end

--- Kapi dongusu. Artik YAKINLIK acmiyor - yalnizca KAPATIYOR: acik
--- birakilmis bir kapi, kimse yakininda degilken kendiliginden
--- kapaniyor. Oyuncunun arkasindan kapiyi kapatmasini beklemek, kisa
--- surede butun klinigi acik kapili birakirdi.
function Doors.start()
	if running then
		return
	end
	running = true

	local accumulator = 0
	RunService.Heartbeat:Connect(function(dt: number)
		accumulator += dt
		if accumulator < 0.5 then
			return
		end
		accumulator = 0

		local positions: { Vector3 } = {}
		for _, player in Players:GetPlayers() do
			local character = player.Character
			local root = if character ~= nil then character:FindFirstChild("HumanoidRootPart") else nil
			if root ~= nil and root:IsA("BasePart") then
				table.insert(positions, root.Position)
			end
		end

		local now = os.clock()
		for _, door in doors do
			if not door.openNow or now - door.openedAt < AUTO_CLOSE_SECONDS then
				continue
			end
			local near = false
			for _, position in positions do
				if (position - door.center).Magnitude <= AUTO_CLOSE_DISTANCE then
					near = true
					break
				end
			end
			if not near then
				setOpen(door, false)
			end
		end
	end)
end

--- Kurulmus kapi sayisi (SelfTest olcuyor).
function Doors.count(): number
	return #doors
end

--- Acik kapi sayisi (SelfTest: baslangicta hepsi kapali olmali).
function Doors.openCount(): number
	local total = 0
	for _, door in doors do
		if door.openNow then
			total += 1
		end
	end
	return total
end

return Doors
