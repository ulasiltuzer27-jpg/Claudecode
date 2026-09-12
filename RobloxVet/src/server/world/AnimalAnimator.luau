--!strict
-- AnimalAnimator.luau
-- ===================
-- Hayvanlarin prosedurel animasyonu ve yurumesi.
--
-- Neden prosedurel?
-- -----------------
-- Roblox animasyonlari Creator Store'a yuklenmis asset'lerdir; bu depo
-- Roblox'a ag erisimi olmayan bir ortamda uretildi ve hicbir animasyon
-- ID'si dogrulanamazdi. Bunun yerine eklem Transform'lari her karede
-- sinuslerle suruluyor: nefes, kuyruk sallama, kafa oynatma, yuruyus
-- ve HASTA HAYVANDA TOPALLAMA. Hicbir dis asset gerekmiyor.
--
-- Topallama bilincli bir oynanis isareti: oyuncu daha aleti eline
-- almadan "bu hayvanin bacaginda bir sey var" diyebilmeli.

local RunService = game:GetService("RunService")

local AnimalFactory = require(script.Parent.AnimalFactory)

local Animator = {}

export type State = "idle" | "walk" | "rest" | "distress"

type Entry = {
	rig: AnimalFactory.Rig,
	state: State,
	phase: number,
	speed: number,
	limpLeg: number,
	limpStrength: number,
	stressLevel: number,
	waypoints: { Vector3 },
	onArrive: (() -> ())?,
	facing: Vector3,
}

local entries: { [Model]: Entry } = {}
local running = false

local ARRIVE_DISTANCE = 1.2
local TURN_RATE = 7

--- Rigi animasyon dongusune alir.
function Animator.register(rig: AnimalFactory.Rig)
	entries[rig.model] = {
		rig = rig,
		state = "idle",
		phase = math.random() * math.pi * 2,
		speed = rig.spec.walkSpeed,
		limpLeg = 0,
		limpStrength = 0,
		stressLevel = 0,
		waypoints = {},
		onArrive = nil,
		facing = rig.root.CFrame.LookVector,
	}
end

function Animator.unregister(rig: AnimalFactory.Rig)
	entries[rig.model] = nil
end

local function entryOf(rig: AnimalFactory.Rig): Entry?
	return entries[rig.model]
end

function Animator.setState(rig: AnimalFactory.Rig, state: State)
	local entry = entryOf(rig)
	if entry ~= nil then
		entry.state = state
	end
end

--- Stres 0..100. Yuksek stres nefesi hizlandirir, kulaklari yatirir.
function Animator.setStress(rig: AnimalFactory.Rig, stress: number)
	local entry = entryOf(rig)
	if entry ~= nil then
		entry.stressLevel = math.clamp(stress, 0, 100)
	end
end

--- Topallama. `leg` 0 ise topallama yok; 1..4 bacak numarasi.
function Animator.setLimp(rig: AnimalFactory.Rig, leg: number, strength: number)
	local entry = entryOf(rig)
	if entry ~= nil then
		entry.limpLeg = leg
		entry.limpStrength = math.clamp(strength, 0, 1)
	end
end

--- Hayvani bir noktaya yurutur. Varinca `onArrive` cagrilir.
function Animator.walkTo(rig: AnimalFactory.Rig, target: Vector3, onArrive: (() -> ())?)
	local entry = entryOf(rig)
	if entry == nil then
		return
	end
	entry.waypoints = { target }
	entry.onArrive = onArrive
	entry.state = "walk"
end

--- Birden fazla ara nokta (kapidan gecip odaya girmek gibi).
function Animator.walkPath(rig: AnimalFactory.Rig, points: { Vector3 }, onArrive: (() -> ())?)
	local entry = entryOf(rig)
	if entry == nil then
		return
	end
	entry.waypoints = table.clone(points)
	entry.onArrive = onArrive
	entry.state = if #entry.waypoints > 0 then "walk" else "idle"
end

--- Aninda konumlandirir (masaya koymak gibi). Yuruyusu iptal eder.
function Animator.placeAt(rig: AnimalFactory.Rig, cframe: CFrame)
	local entry = entryOf(rig)
	local torsoY = rig.spec.legs.length + rig.spec.body.height * 0.5
	rig.root.CFrame = cframe * CFrame.new(0, torsoY, 0)
	if entry ~= nil then
		entry.waypoints = {}
		entry.onArrive = nil
		entry.state = "idle"
		entry.facing = rig.root.CFrame.LookVector
	end
end

--- Hareket: kok parcayi hedefe dogru tasir ve yonunu yumusatir.
local function stepMovement(entry: Entry, dt: number): number
	local rig = entry.rig
	if #entry.waypoints == 0 then
		return 0
	end

	local root = rig.root
	local target = entry.waypoints[1]
	local here = root.Position
	local flatTarget = Vector3.new(target.X, here.Y, target.Z)
	local delta = flatTarget - here
	local distance = delta.Magnitude

	if distance <= ARRIVE_DISTANCE then
		table.remove(entry.waypoints, 1)
		if #entry.waypoints == 0 then
			entry.state = "idle"
			local callback = entry.onArrive
			entry.onArrive = nil
			if callback ~= nil then
				task.spawn(callback)
			end
		end
		return 0
	end

	local direction = delta.Unit
	-- Hasta hayvan yavas yurur; topallama ne kadar guclüyse o kadar yavas.
	local speed = entry.speed * (1 - entry.limpStrength * 0.35)
	local move = math.min(speed * dt, distance)

	entry.facing = entry.facing:Lerp(direction, math.clamp(TURN_RATE * dt, 0, 1))
	if entry.facing.Magnitude < 1e-3 then
		entry.facing = direction
	end

	local newPosition = here + direction * move
	root.CFrame = CFrame.lookAt(newPosition, newPosition + entry.facing.Unit)
	return speed
end

--- Animasyon: eklem Transform'larini surer.
local function stepPose(entry: Entry, dt: number, movingSpeed: number)
	local rig = entry.rig
	local joints = rig.joints
	local walking = entry.state == "walk" and movingSpeed > 0

	local stress = entry.stressLevel / 100
	local breathRate = 1.4 + stress * 2.2 + (if entry.state == "distress" then 1.6 else 0)
	local strideRate = if walking then math.max(movingSpeed, 1) * 1.15 else 0

	entry.phase += dt * (if walking then strideRate else breathRate)
	local phase = entry.phase

	-- Govde: nefes alip verme + yururken hafif salinim.
	if joints.rootJoint ~= nil then
		local breath = math.sin(phase * (if walking then 0.5 else 1)) * (0.035 + stress * 0.03)
		local sway = if walking then math.sin(phase * 2) * 0.03 else 0
		local tilt = entry.limpStrength * math.sin(phase) * 0.05
		joints.rootJoint.Transform = CFrame.new(0, breath, 0) * CFrame.Angles(sway, 0, tilt)
	end

	-- Bacaklar: capraz yuruyus. Topallayan bacagin genligi dusuk ve
	-- fazi kaymis - gozle "aksiyor" diye okunuyor.
	local amplitude = if walking then 0.55 else 0.0
	for index = 1, 4 do
		local joint = joints["leg" .. index]
		if joint ~= nil then
			local offset = if index % 2 == 0 then math.pi else 0
			if index > 2 then
				offset += math.pi
			end
			local legAmplitude = amplitude
			local extra = 0
			if index == entry.limpLeg then
				legAmplitude *= (1 - entry.limpStrength * 0.75)
				extra = entry.limpStrength * 0.22
			end
			local swing = math.sin(phase + offset) * legAmplitude + extra
			joint.Transform = CFrame.Angles(swing, 0, 0)
		end
	end

	-- Kafa: yururken hafif asagi/yukari, dururken merakli saga-sola.
	if joints.neck ~= nil then
		local nod = if walking then math.sin(phase * 2) * 0.08 else math.sin(phase * 0.55) * 0.05
		local turn = if walking then 0 else math.sin(phase * 0.32) * 0.22
		local droop = (if entry.state == "distress" then 0.2 else 0) + stress * 0.12
		joints.neck.Transform = CFrame.Angles(nod - droop, turn, 0)
	end

	-- Kuyruk: rahatken genis sallanir, stresliyken kisilir ve asagi duser.
	if joints.tail ~= nil then
		local wagAmplitude = (0.45 - stress * 0.35) * (if walking then 1.3 else 1)
		local wag = math.sin(phase * 2.4) * math.max(wagAmplitude, 0.05)
		joints.tail.Transform = CFrame.Angles(-stress * 0.5, wag, 0)
	end

	-- Kulaklar: stres arttikca geriye yatiyor, arada bir seyiriyor.
	for index = 1, 2 do
		local joint = joints["ear" .. index]
		if joint ~= nil then
			local side = if index == 1 then -1 else 1
			local twitch = math.sin(phase * 3.1 + index) * 0.05
			joint.Transform = CFrame.Angles(-stress * 0.55, 0, side * (twitch + stress * 0.25))
		end
	end

	-- Yilan: govde halkalari boyunca ilerleyen bir dalga. Faz kaymasi
	-- halkadan halkaya sabit; dalga bastan kuyruga akiyor, yani hayvan
	-- kendi uzerinde SURUNUYOR gibi gorunuyor. Dururken de kisik bir
	-- genlikle devam ediyor: canli bir yilan tas gibi durmaz.
	do
		local index = 2
		local amplitude = if walking then 0.42 else 0.12
		while true do
			local joint = joints["segment" .. index]
			if joint == nil then
				break
			end
			local offset = (index - 1) * 0.7
			joint.Transform = CFrame.Angles(0, math.sin(phase * 1.6 - offset) * amplitude, 0)
			index += 1
		end
	end

	-- Kanatlar: yururken cirpinir, dururken govdeye yapisir.
	for index = 1, 2 do
		local joint = joints["wing" .. index]
		if joint ~= nil then
			local side = if index == 1 then -1 else 1
			local flap = if walking then math.abs(math.sin(phase * 3)) * 0.6 else math.sin(phase) * 0.06
			joint.Transform = CFrame.Angles(0, 0, side * -flap)
		end
	end
end

--- Heartbeat dongusunu baslatir. init.server.luau bir kez cagirir.
function Animator.start()
	if running then
		return
	end
	running = true

	RunService.Heartbeat:Connect(function(dt: number)
		-- dt tavanlanıyor: Studio duraklatilip devam ettirildiginde tek
		-- karede 10 saniyelik dt gelebiliyor ve hayvanlar duvarlardan
		-- gecip gidiyor.
		local step = math.min(dt, 0.1)
		for model, entry in entries do
			if model.Parent == nil then
				entries[model] = nil
				continue
			end
			local speed = stepMovement(entry, step)
			stepPose(entry, step, speed)
		end
	end)
end

--- Kac rig canli? SelfTest ve tanilama icin.
function Animator.count(): number
	local total = 0
	for _ in entries do
		total += 1
	end
	return total
end

return Animator
