--!strict
-- Doors.luau
-- ==========
-- Yaklasinca acilan cift kanatli kapilar.
--
-- Neden kapi?
-- -----------
-- Klinikte kapi bosluklari vardi ama kapi yoktu: odalar birbirine acik
-- deliklerle bagliydi ve bina "bitmemis" duruyordu. Yaklasinca acilan
-- kanatlar hem binayi bitiriyor hem de klinikte "gecis" hissini veriyor.
--
-- Kanatlar CARPISMIYOR
-- --------------------
-- Kapali bir kanat carpisan bir duvar olsaydi, acilma gecikmesinde ya da
-- bir hata durumunda oyuncu kapida SIKISIRDI. Gorunum icin kapi, oynanis
-- icin acik gecit. Yerlesim denetimi (tools/verify_layout.py) kapi
-- bosluklarinin bos kalmasini zaten sart kosuyor; kanatlar o bosluga
-- oturuyor ve esyalarla asla cakismiyor.
--
-- Neden CFrameValue suruyor?
-- --------------------------
-- Bir kanat tek parca degil: govde + cam panel + kulp. TweenService bir
-- Model'i tasiyamiyor, ankorlu parcalara kaynak (weld) da islemiyor.
-- Bu yuzden bir `CFrameValue` tweenleniyor ve her degisiminde kanadin
-- butun parcalari o cerceveye gore yeniden konumlaniyor - tek kaynak,
-- parcalar arasi kayma imkansiz.

local Players = game:GetService("Players")
local RunService = game:GetService("RunService")
local TweenService = game:GetService("TweenService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Build = require(Shared.Build)

local plan = require(Data.clinic)

local Doors = {}

type Leaf = {
	driver: CFrameValue,
	closed: CFrame,
	open: CFrame,
}

type Door = {
	center: Vector3,
	leaves: { Leaf },
	openNow: boolean,
}

local doors: { Door } = {}
local running = false

-- Oyuncu bu mesafeye girince kapi aciliyor. Kapi genisliginden bagimsiz
-- sabit: kapi ne kadar genis olursa olsun "yaklasma" ayni.
local TRIGGER_DISTANCE = 9
local SWING_SECONDS = 0.45

--- Tek bir kapi bosluguna cift kanat kurar.
local function buildDoor(parent: Instance, center: Vector3, horizontal: boolean, width: number)
	local height = plan.doorHeight - 0.4
	local leafWidth = width * 0.5 - 0.2
	local thickness = 0.24
	local woodColor = Build.color(plan.colors.windowFrame)
	local trimColor = Build.color(plan.colors.wallTrim)
	local glassColor = Build.color(plan.colors.glass)

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

		-- Alt panel ve kulp: duz bir dikdortgen "kapi" degil, "levha"
		-- gibi duruyordu.
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

		local handleSide = if horizontal
			then Vector3.new(-side * leafWidth * 0.34, -height * 0.02, thickness * 0.85)
			else Vector3.new(thickness * 0.85, -height * 0.02, -side * leafWidth * 0.34)
		add(Build.part({
			name = "KanatKulpu",
			size = Vector3.new(0.16, 0.9, 0.16),
			cf = base * CFrame.new(handleSide),
			color = trimColor,
			material = Enum.Material.Metal,
			canCollide = false,
			castShadow = false,
			parent = parent,
		}))

		-- Mentese DIS kenarda: kanat gecidin disina dogru aciliyor.
		local hingeOffset = if horizontal
			then Vector3.new(side * leafWidth * 0.5, 0, 0)
			else Vector3.new(0, 0, side * leafWidth * 0.5)
		local hinge = base * CFrame.new(hingeOffset)
		local openBase = hinge * CFrame.Angles(0, side * math.rad(-95), 0) * hinge:ToObjectSpace(base)

		local driver = Instance.new("CFrameValue")
		driver.Name = "KanatSurucusu"
		driver.Value = base
		driver.Parent = parent
		driver.Changed:Connect(function(value: CFrame)
			for _, entry in parts do
				entry.part.CFrame = value * entry.offset
			end
		end)

		table.insert(leaves, { driver = driver, closed = base, open = openBase })
	end

	table.insert(doors, { center = center, leaves = leaves, openNow = false })
end

--- Klinik planindaki her kapiya kanat kurar.
function Doors.build(parent: Instance): Model
	doors = {}
	local model = Build.model("Kapilar", parent)

	for _, wall in plan.walls do
		local horizontal = math.abs(wall.to[2] - wall.from[2]) < 1e-6
		local fixed = if horizontal then wall.from[2] else wall.from[1]
		for _, door in wall.doors or {} do
			local center = if horizontal
				then Vector3.new(door.at, plan.floorY, fixed)
				else Vector3.new(fixed, plan.floorY, door.at)
			buildDoor(model, center, horizontal, door.width)
		end
	end
	return model
end

local function setOpen(door: Door, open: boolean)
	if door.openNow == open then
		return
	end
	door.openNow = open
	for _, leaf in door.leaves do
		TweenService:Create(
			leaf.driver,
			TweenInfo.new(SWING_SECONDS, Enum.EasingStyle.Quad, Enum.EasingDirection.Out),
			{ Value = if open then leaf.open else leaf.closed }
		):Play()
	end
end

--- Yakinlik dongusu. Her karede degil, saniyede ~6 kez bakiyor: acilma
--- mesafesi 9 stud ve en hizli oyuncu bu surede ~4 stud gidiyor - yani
--- kapi hep zamaninda aciliyor ama 60 yerine 6 kez hesap yapiliyor.
function Doors.start()
	if running then
		return
	end
	running = true

	local accumulator = 0
	RunService.Heartbeat:Connect(function(dt: number)
		accumulator += dt
		if accumulator < 1 / 6 then
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

		for _, door in doors do
			local near = false
			for _, position in positions do
				if (position - door.center).Magnitude <= TRIGGER_DISTANCE then
					near = true
					break
				end
			end
			setOpen(door, near)
		end
	end)
end

--- Kurulmus kapi sayisi (SelfTest olcuyor).
function Doors.count(): number
	return #doors
end

return Doors
