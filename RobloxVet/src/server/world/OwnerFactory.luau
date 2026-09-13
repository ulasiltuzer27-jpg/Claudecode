--!strict
-- OwnerFactory.luau
-- =================
-- Hayvanin sahibi olan NPC'yi kurar.
--
-- Neden Humanoid degil, parcalardan?
-- ----------------------------------
-- Bir Humanoid karakter, animasyon asset'i olmadan T-poz durur; bu
-- ortamda Roblox'a erisim olmadigi icin hicbir animasyon ID'si
-- dogrulanamazdi. Buradaki NPC blok parcalardan kuruluyor ve OwnerFactory
-- kendi duruslarini (ayakta / oturur / kollari kavusturmus) CFrame ile
-- veriyor; ayrica bekleme odasinda nefes alip etrafina bakiyor.
--
-- Sahibin isi oynanista ne?
-- -------------------------
-- Iki sey: (1) basinin ustunde hastaligin `hintKey` ipucunu soyluyor -
-- gercek bir muayenede de sahibin anlattigi sey tani degil, sikayettir;
-- (2) MEMNUNIYETI var. Bekledikce duser, dogru teshis ve saglikli
-- taburcu ile yukselir; yeterince memnunsa ucretin uzerine bahsis birakir.

local RunService = game:GetService("RunService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")

local Build = require(Shared.Build)
local Loc = require(Shared.Loc)

local OwnerFactory = {}

export type Owner = {
	model: Model,
	root: Part,
	joints: { [string]: Motor6D },
	bubble: BillboardGui,
	bubbleText: TextLabel,
	meter: Frame,
	phase: number,
	-- Jest katmani: tek seferlik kol/bas hareketi. Bosken "".
	gesture: string,
	gestureTime: number,
	-- Son uygulanan poz: yumusatma bunun uzerinden gidiyor.
	pose: { [string]: CFrame },
	-- Bakis hedefi (hayvani ya da veteriner).
	lookTarget: Vector3?,
}

-- Sahipler rastgele gorunsun diye kucuk palet havuzlari. Oynanisi
-- etkilemiyor, o yuzden veri dosyasinda degil burada.
local SKIN = {
	Color3.fromRGB(242, 208, 176),
	Color3.fromRGB(218, 172, 132),
	Color3.fromRGB(176, 126, 88),
	Color3.fromRGB(126, 88, 62),
	Color3.fromRGB(86, 58, 42),
}
local SHIRT = {
	Color3.fromRGB(78, 132, 186),
	Color3.fromRGB(196, 92, 86),
	Color3.fromRGB(96, 156, 112),
	Color3.fromRGB(206, 160, 72),
	Color3.fromRGB(132, 108, 176),
	Color3.fromRGB(64, 72, 92),
}
local TROUSERS = {
	Color3.fromRGB(58, 64, 78),
	Color3.fromRGB(46, 52, 62),
	Color3.fromRGB(92, 80, 70),
}
local HAIR = {
	Color3.fromRGB(44, 36, 32),
	Color3.fromRGB(92, 62, 40),
	Color3.fromRGB(186, 154, 96),
	Color3.fromRGB(150, 148, 152),
}

local function pick<T>(list: { T }): T
	return list[math.random(1, #list)]
end

local function rigJoint(name: string, part0: BasePart, part1: BasePart, pivot: Vector3): Motor6D
	local pivotCF = CFrame.new(pivot)
	local motor = Instance.new("Motor6D")
	motor.Name = name
	motor.Part0 = part0
	motor.Part1 = part1
	motor.C0 = part0.CFrame:ToObjectSpace(pivotCF)
	motor.C1 = part1.CFrame:ToObjectSpace(pivotCF)
	motor.Parent = part0
	return motor
end

local function rigidWeld(part0: BasePart, part1: BasePart)
	local weld = Instance.new("WeldConstraint")
	weld.Part0 = part0
	weld.Part1 = part1
	weld.Parent = part0
end

--- Konusma balonu + memnuniyet cubugu.
local function buildBubble(root: BasePart): (BillboardGui, TextLabel, Frame)
	local billboard = Instance.new("BillboardGui")
	billboard.Name = "Balon"
	billboard.Size = UDim2.fromOffset(250, 92)
	billboard.StudsOffsetWorldSpace = Vector3.new(0, 4.4, 0)
	billboard.AlwaysOnTop = true
	billboard.MaxDistance = 80
	billboard.Parent = root

	local frame = Instance.new("Frame")
	frame.Size = UDim2.fromScale(1, 1)
	frame.BackgroundColor3 = Color3.fromRGB(250, 250, 252)
	frame.BackgroundTransparency = 0.06
	frame.BorderSizePixel = 0
	frame.Parent = billboard
	local corner = Instance.new("UICorner")
	corner.CornerRadius = UDim.new(0, 10)
	corner.Parent = frame

	local text = Instance.new("TextLabel")
	text.Name = "Metin"
	text.Size = UDim2.new(1, -16, 1, -26)
	text.Position = UDim2.fromOffset(8, 6)
	text.BackgroundTransparency = 1
	text.Font = Enum.Font.GothamMedium
	text.TextSize = 14
	text.TextWrapped = true
	text.TextColor3 = Color3.fromRGB(38, 44, 56)
	text.Text = ""
	text.Parent = frame

	local track = Instance.new("Frame")
	track.Name = "Memnuniyet"
	track.Size = UDim2.new(1, -16, 0, 9)
	track.Position = UDim2.new(0, 8, 1, -15)
	track.BackgroundColor3 = Color3.fromRGB(214, 218, 224)
	track.BorderSizePixel = 0
	track.Parent = frame
	local trackCorner = Instance.new("UICorner")
	trackCorner.CornerRadius = UDim.new(0, 5)
	trackCorner.Parent = track

	local fill = Instance.new("Frame")
	fill.Name = "Dolgu"
	fill.Size = UDim2.fromScale(0.75, 1)
	fill.BackgroundColor3 = Color3.fromRGB(96, 196, 120)
	fill.BorderSizePixel = 0
	fill.Parent = track
	local fillCorner = Instance.new("UICorner")
	fillCorner.CornerRadius = UDim.new(0, 5)
	fillCorner.Parent = fill

	return billboard, text, fill
end

--- Sahibi kurar. `cframe` ayaklarinin basacagi yer.
--- Insan rigi kurar.
---
--- `coat` verilirse ustu o renge boyaniyor: PERSONEL de ayni rigi
--- kullaniyor (StaffFactory), yalnizca onlugunun rengi sabit. Ikinci
--- bir insan rigi yazmak, ayni 40 satiri iki yerde tutmak olurdu.
function OwnerFactory.create(cframe: CFrame, hintKey: string, parent: Instance, coat: Color3?): Owner
	local model = Build.model("Sahip", parent)
	local skin = pick(SKIN)
	local shirt = if coat ~= nil then coat else pick(SHIRT)
	local trousers = pick(TROUSERS)
	local hair = pick(HAIR)

	local hipHeight = 3.0
	local torsoHeight = 2.4
	local torsoY = hipHeight + torsoHeight * 0.5

	local root = Build.part({
		name = "Root",
		size = Vector3.new(2, 0.4, 1),
		cf = CFrame.new(0, torsoY, 0),
		transparency = 1,
		canCollide = false,
		canQuery = false,
		castShadow = false,
		anchored = true,
		parent = model,
	})

	local torso = Build.part({
		name = "Govde",
		size = Vector3.new(2, torsoHeight, 1),
		cf = CFrame.new(0, torsoY, 0),
		color = shirt,
		material = Enum.Material.Fabric,
		canCollide = false,
		anchored = false,
		parent = model,
	})
	local joints: { [string]: Motor6D } = {}
	joints.rootJoint = rigJoint("RootJoint", root, torso, Vector3.new(0, torsoY, 0))

	local head = Build.part({
		name = "Kafa",
		size = Vector3.new(1.15, 1.25, 1.15),
		cf = CFrame.new(0, torsoY + torsoHeight * 0.5 + 0.7, 0),
		color = skin,
		canCollide = false,
		anchored = false,
		parent = model,
	})
	joints.neck = rigJoint("Neck", torso, head, Vector3.new(0, torsoY + torsoHeight * 0.5, 0))

	local hairPart = Build.part({
		name = "Sac",
		size = Vector3.new(1.25, 0.45, 1.25),
		cf = head.CFrame * CFrame.new(0, 0.5, -0.04),
		color = hair,
		canCollide = false,
		anchored = false,
		parent = model,
	})
	rigidWeld(head, hairPart)

	for _, side in { -1, 1 } do
		local eye = Build.ball({
			name = "Goz",
			size = Vector3.new(0.17, 0.17, 0.17),
			cf = head.CFrame * CFrame.new(side * 0.26, 0.1, 0.56),
			color = Color3.fromRGB(32, 30, 30),
			canCollide = false,
			anchored = false,
			parent = model,
		})
		rigidWeld(head, eye)
	end

	for index, side in { -1, 1 } do
		local shoulder = Vector3.new(side * 1.25, torsoY + torsoHeight * 0.42, 0)
		local arm = Build.part({
			name = "Kol" .. index,
			size = Vector3.new(0.6, 2.1, 0.6),
			cf = CFrame.new(shoulder - Vector3.new(0, 1.05, 0)),
			color = shirt,
			material = Enum.Material.Fabric,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		local hand = Build.part({
			name = "El" .. index,
			size = Vector3.new(0.58, 0.5, 0.58),
			cf = CFrame.new(shoulder - Vector3.new(0, 2.1, 0)),
			color = skin,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		rigidWeld(arm, hand)
		joints["arm" .. index] = rigJoint("Arm" .. index, torso, arm, shoulder)

		local hip = Vector3.new(side * 0.5, hipHeight, 0)
		local leg = Build.part({
			name = "Bacak" .. index,
			size = Vector3.new(0.8, hipHeight, 0.8),
			cf = CFrame.new(hip - Vector3.new(0, hipHeight * 0.5, 0)),
			color = trousers,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		joints["leg" .. index] = rigJoint("Leg" .. index, torso, leg, hip)
	end

	local bubble, bubbleText, meter = buildBubble(root)
	bubbleText.Text = Loc.get(hintKey)

	model.PrimaryPart = root
	root.CFrame = cframe * CFrame.new(0, torsoY, 0)

	return {
		model = model,
		root = root,
		joints = joints,
		bubble = bubble,
		bubbleText = bubbleText,
		meter = meter,
		phase = math.random() * math.pi * 2,
		gesture = "",
		gestureTime = 0,
		pose = {},
		lookTarget = nil,
	}
end

--- Memnuniyet cubugunu gunceller (0-100). Renk de duruma gore degisiyor:
--- oyuncu bakip "bu adam sinirlenmis" diyebilmeli.
function OwnerFactory.setSatisfaction(owner: Owner, value: number)
	local ratio = math.clamp(value / 100, 0, 1)
	owner.meter.Size = UDim2.fromScale(ratio, 1)
	if ratio >= 0.6 then
		owner.meter.BackgroundColor3 = Color3.fromRGB(96, 196, 120)
	elseif ratio >= 0.3 then
		owner.meter.BackgroundColor3 = Color3.fromRGB(232, 176, 72)
	else
		owner.meter.BackgroundColor3 = Color3.fromRGB(228, 96, 92)
	end
end

--- Balondaki cumleyi degistirir.
function OwnerFactory.say(owner: Owner, key: string, ...: any)
	owner.bubbleText.Text = Loc.get(key, ...)
end

-- Jest sureleri (saniye). Hayvan tepkileriyle ayni kalip: sinus
-- katmaninin USTUNE binen, kendi kendini temizleyen tek seferlik
-- hareket.
local GESTURE_SECONDS = {
	point = 1.4,
	relief = 1.8,
	worry = 1.6,
}

--- Sahibin jesti: hayvanini GOSTERME, teshis dogruyken RAHATLAMA,
--- acil vakada ENDISE. Konusma balonu tek basina yetmiyordu - uzaktan
--- bakan oyuncu balonu okumadan once kolu goruyor.
function OwnerFactory.gesture(owner: Owner, kind: string)
	if GESTURE_SECONDS[kind] == nil then
		warn(("[OwnerFactory] bilinmeyen jest: '%s'"):format(kind))
		return
	end
	owner.gesture = kind
	owner.gestureTime = 0
end

--- Tanimli jestler (SelfTest icin).
function OwnerFactory.gestureKinds(): { string }
	local kinds = {}
	for kind in GESTURE_SECONDS do
		table.insert(kinds, kind)
	end
	table.sort(kinds)
	return kinds
end

--- Durusu gunceller. Sahip bekleme odasinda durup nefes aliyor, arada
--- etrafina bakiyor; sinirliyse kollarini kavusturuyor.
-- Yumusatma hizlari. Bacaklar hizli: yavas yumusatma adimin salinim
-- fazini eziyor ve yuruyus yeniden "kayar" gorunuyor.
local SMOOTH_BODY = 12
local SMOOTH_LIMB = 26

-- Adim dongusunun DURUS fazi. AnimalAnimator ile ayni oran: ayak yerde
-- sabit hizla geriye gidiyor, govde ustunden geciyor.
local STANCE = 0.62

--- Bir eklemi hedefe yumusatarak surer.
local function drive(owner: Owner, name: string, joint: Motor6D, target: CFrame, rate: number, dt: number)
	local previous = owner.pose[name]
	local blended = if previous == nil
		then target
		else previous:Lerp(target, math.clamp(rate * dt, 0, 1))
	owner.pose[name] = blended
	joint.Transform = blended
end

--- Sahibin basini bir noktaya cevirir. `nil` bakisi birakir.
function OwnerFactory.lookAt(owner: Owner, target: Vector3?)
	owner.lookTarget = target
end

function OwnerFactory.step(owner: Owner, dt: number, satisfaction: number, walking: boolean)
	owner.phase += dt * (if walking then 8.5 else 1.3)
	local phase = owner.phase
	local annoyed = satisfaction < 40

	-- Jest zamanlayicisi: suresi dolunca kendini temizliyor.
	local gesture = owner.gesture
	local gestureStrength = 0
	if gesture ~= "" then
		local total = GESTURE_SECONDS[gesture]
		if total == nil then
			owner.gesture = ""
			gesture = ""
		else
			owner.gestureTime += dt
			if owner.gestureTime >= total then
				owner.gesture = ""
				gesture = ""
			else
				gestureStrength = math.sin(owner.gestureTime / total * math.pi)
			end
		end
	end

	if owner.joints.rootJoint ~= nil then
		-- Dururken agirlik bir ayaktan digerine geciyor; tas gibi duran
		-- bir NPC cansiz gorunuyor.
		local shift = if walking then 0 else math.sin(phase * 0.5) * 0.04
		drive(
			owner,
			"rootJoint",
			owner.joints.rootJoint,
			CFrame.new(0, math.sin(phase) * 0.03, 0) * CFrame.Angles(0, 0, shift),
			SMOOTH_BODY,
			dt
		)
	end

	if owner.joints.neck ~= nil then
		local look = if walking then 0 else math.sin(phase * 0.4) * 0.35
		local nod = math.sin(phase * 0.7) * 0.04
		if gesture == "point" then
			-- Gosterirken basini hayvana dogru egiyor.
			nod += gestureStrength * 0.22
		elseif gesture == "relief" then
			-- Rahatlarken basini geriye atiyor.
			nod -= gestureStrength * 0.3
		elseif gesture == "worry" then
			nod += gestureStrength * 0.34
			look += math.sin(owner.gestureTime * 9) * gestureStrength * 0.4
		end

		-- BAKIS: hayvanina ya da veterinere doner. Aci sinirli; govdesi
		-- donmeden basini 180 derece cevirmiyor.
		local target = owner.lookTarget
		if target ~= nil then
			local local3 = owner.root.CFrame:PointToObjectSpace(target)
			local flat = math.sqrt(local3.X * local3.X + local3.Z * local3.Z)
			if flat > 0.5 then
				look += math.clamp(math.atan2(-local3.X, -local3.Z), -0.7, 0.7)
				nod -= math.clamp(math.atan2(local3.Y, flat), -0.3, 0.4)
			end
		end

		drive(owner, "neck", owner.joints.neck, CFrame.Angles(nod, look, 0), SMOOTH_BODY, dt)
	end

	for index = 1, 2 do
		local side = if index == 1 then -1 else 1
		local arm = owner.joints["arm" .. index]
		if arm ~= nil then
			local target
			if gesture == "point" and index == 2 then
				-- Sag kol one uzanip hayvani gosteriyor.
				target = CFrame.Angles(math.rad(-95 * gestureStrength), 0, math.rad(side * -18 * gestureStrength))
			elseif gesture == "relief" then
				-- Iki kol da hafifce aciliyor: "cok sukur" durusu.
				target = CFrame.Angles(math.rad(-28 * gestureStrength), 0, math.rad(side * -46 * gestureStrength))
			elseif gesture == "worry" then
				-- Eller yuze yakin: endise.
				target = CFrame.Angles(math.rad(-118 * gestureStrength), 0, math.rad(side * -26 * gestureStrength))
			elseif annoyed and not walking then
				-- Kollari kavusturmus: gorsel olarak "bekliyorum" demek.
				target = CFrame.Angles(math.rad(-72), 0, math.rad(side * -34))
			elseif walking then
				target = CFrame.Angles(math.sin(phase + (if index == 1 then 0 else math.pi)) * 0.6, 0, 0)
			else
				target = CFrame.Angles(math.sin(phase * 0.5) * 0.05, 0, 0)
			end
			drive(owner, "arm" .. index, arm, target, SMOOTH_LIMB, dt)
		end

		local leg = owner.joints["leg" .. index]
		if leg ~= nil then
			local target = CFrame.identity
			if walking then
				-- Ayak yerde SABIT HIZLA geriye gidiyor (durus fazi),
				-- sonra hizla one savruluyor (salinim fazi). Duz bir
				-- sinus, ayagi yerde yavaslatip kaydiriyordu.
				local p = ((phase / (math.pi * 2)) + (if index == 1 then 0.5 else 0)) % 1
				local swing
				if p < STANCE then
					swing = -0.5 + (p / STANCE)
				else
					swing = 0.5 - ((p - STANCE) / (1 - STANCE))
				end
				target = CFrame.Angles(swing * 0.55, 0, 0)
			end
			drive(owner, "leg" .. index, leg, target, SMOOTH_LIMB, dt)
		end
	end
end

-- ── Hareket ve dongu ──────────────────────────────────────────────────
-- Sahipler hayvanla birlikte yurur, bekleme odasinda durur. Kendi
-- Heartbeat'ini tutuyor ki PatientFlow her kare NPC pozu hesaplamak
-- zorunda kalmasin.

type Entry = {
	owner: Owner,
	waypoints: { Vector3 },
	facing: Vector3,
	satisfaction: number,
	onArrive: (() -> ())?,
}

local entries: { [Model]: Entry } = {}
local running = false
local ARRIVE_DISTANCE = 1.6
local WALK_SPEED = 13
local TURN_RATE = 8

function OwnerFactory.register(owner: Owner)
	entries[owner.model] = {
		owner = owner,
		waypoints = {},
		facing = owner.root.CFrame.LookVector,
		satisfaction = 75,
		onArrive = nil,
	}
end

function OwnerFactory.unregister(owner: Owner)
	entries[owner.model] = nil
end

function OwnerFactory.walkPath(owner: Owner, points: { Vector3 }, onArrive: (() -> ())?)
	local entry = entries[owner.model]
	if entry == nil then
		return
	end
	entry.waypoints = table.clone(points)
	entry.onArrive = onArrive
end

--- Memnuniyeti hem cubukta hem de duruslarda kullanabilmek icin dongu
--- de biliyor olmali.
function OwnerFactory.updateSatisfaction(owner: Owner, value: number)
	local entry = entries[owner.model]
	if entry ~= nil then
		entry.satisfaction = value
	end
	OwnerFactory.setSatisfaction(owner, value)
end

--- Yuzunu bir noktaya donduru (masadaki hayvanina bakmasi icin).
function OwnerFactory.faceTowards(owner: Owner, target: Vector3)
	local entry = entries[owner.model]
	if entry == nil or #entry.waypoints > 0 then
		return
	end
	local here = owner.root.Position
	local flat = Vector3.new(target.X, here.Y, target.Z)
	if (flat - here).Magnitude < 0.1 then
		return
	end
	entry.facing = (flat - here).Unit
	owner.root.CFrame = CFrame.lookAt(here, here + entry.facing)
end

function OwnerFactory.start()
	if running then
		return
	end
	running = true

	RunService.Heartbeat:Connect(function(dt: number)
		-- dt tavanlanıyor: Studio duraklatilip devam ettirildiginde tek
		-- karede cok buyuk bir dt gelip NPC'leri duvardan gecirebiliyor.
		local step = math.min(dt, 0.1)
		for model, entry in entries do
			if model.Parent == nil then
				entries[model] = nil
				continue
			end

			local walking = false
			if #entry.waypoints > 0 then
				local root = entry.owner.root
				local target = entry.waypoints[1]
				local here = root.Position
				local flatTarget = Vector3.new(target.X, here.Y, target.Z)
				local delta = flatTarget - here
				local distance = delta.Magnitude

				if distance <= ARRIVE_DISTANCE then
					table.remove(entry.waypoints, 1)
					if #entry.waypoints == 0 then
						local callback = entry.onArrive
						entry.onArrive = nil
						if callback ~= nil then
							task.spawn(callback)
						end
					end
				else
					walking = true
					local direction = delta.Unit
					entry.facing = entry.facing:Lerp(direction, math.clamp(TURN_RATE * step, 0, 1))
					if entry.facing.Magnitude < 1e-3 then
						entry.facing = direction
					end
					local move = math.min(WALK_SPEED * step, distance)
					local newPosition = here + direction * move
					root.CFrame = CFrame.lookAt(newPosition, newPosition + entry.facing.Unit)
				end
			end

			OwnerFactory.step(entry.owner, step, entry.satisfaction, walking)
		end
	end)
end

return OwnerFactory
