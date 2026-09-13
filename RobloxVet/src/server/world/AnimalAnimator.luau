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
	coiled: boolean,
	-- Tepki katmani: sinus katmaninin USTUNE binen, tek seferlik
	-- hareket. Bosken "" ve hicbir maliyeti yok.
	reaction: string,
	reactionTime: number,
	-- Son uygulanan poz. Yumusatma bunun uzerinden gidiyor: hedefe
	-- aninda atlamak yerine her kare biraz yaklasiyor.
	pose: { [string]: CFrame },
	-- Goz kirpma: kalan sure, suren kirpma, son yazilan saydamlik.
	blinkIn: number,
	blinkTime: number,
	lidTransparency: number,
	-- Ikincil hareket: gecen karedeki konum ve gecikmeli hiz.
	lastPosition: Vector3,
	lagVelocity: Vector3,
	-- Bakis: nereye bakiyor ve ne kadar guclu.
	lookTarget: Vector3?,
	lookWeight: number,
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
		coiled = false,
		reaction = "",
		reactionTime = 0,
		pose = {},
		blinkIn = 1 + math.random() * 3,
		blinkTime = -1,
		lidTransparency = 1,
		lastPosition = rig.root.Position,
		lagVelocity = Vector3.zero,
		lookTarget = nil,
		lookWeight = 0,
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

--- Yilani kivirir. Masaya konan bir yilan duz uzanmaz: 7 stud'luk
--- govdesi 3 stud'luk bir daireye toplanir. `data/animals.json`'daki
--- `tableFootprint` bu duruma gore yazili.
function Animator.setCoiled(rig: AnimalFactory.Rig, coiled: boolean)
	local entry = entryOf(rig)
	if entry ~= nil then
		entry.coiled = coiled
	end
end

-- Tepki suresi (saniye). Anahtar kumesi burada tek yerde; PatientFlow
-- bilinmeyen bir tepki yollarsa sessizce yutulmuyor, uyariliyor.
local REACTION_SECONDS = {
	flinch = 0.55,
	relax = 1.8,
	wag = 1.9,
	breathe = 3.2,
	shake = 0.8,
	perk = 0.9,
}

--- Tek seferlik tepki oynatir: igne yiyince IRKILME, yikanirken
--- GEVSEME, dogru teshiste KUYRUK SALLAMA, ameliyatta DERIN NEFES.
--- Mevcut sinus katmanini durdurmuyor, uzerine biniyor - hayvan hem
--- nefes almaya devam ediyor hem irkiliyor.
function Animator.react(rig: AnimalFactory.Rig, kind: string)
	local entry = entryOf(rig)
	if entry == nil then
		return
	end
	if REACTION_SECONDS[kind] == nil then
		warn(("[AnimalAnimator] bilinmeyen tepki: '%s'"):format(kind))
		return
	end
	entry.reaction = kind
	entry.reactionTime = 0
end

--- Tanimli tepkiler (SelfTest ve verify_data karsilastirmasi icin).
function Animator.reactionKinds(): { string }
	local kinds = {}
	for kind in REACTION_SECONDS do
		table.insert(kinds, kind)
	end
	table.sort(kinds)
	return kinds
end

--- Hayvanin basini bir noktaya cevirir. `nil` bakisi birakir.
---
--- Neden ayri bir katman? Muayene masasindaki hayvanin veterinere
--- BAKMASI, oyuncunun "bu hayvan beni goruyor" hissini veren tek sey.
--- Tepki katmani tek seferlik; bakis ise surekli ve yumusak.
function Animator.lookAt(rig: AnimalFactory.Rig, target: Vector3?, weight: number?)
	local entry = entryOf(rig)
	if entry == nil then
		return
	end
	entry.lookTarget = target
	entry.lookWeight = math.clamp(weight or 1, 0, 1)
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
	-- Yurumeye baslayan yilan kivrilmasini cozer; durunca yeniden
	-- kiviriliyor (asagidaki dongu). Beklerken duz uzanan bir yilan hem
	-- gercekci degil hem de 7,2 stud'luk govdesiyle komsusuna giriyordu.
	entry.coiled = false
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

--- Tepkinin o andaki gucu (0..1 arasi, basta ve sonda 0) ve adi.
--- Suresi dolan tepki kendiliginden temizleniyor.
local function stepReaction(entry: Entry, dt: number): (string, number)
	if entry.reaction == "" then
		return "", 0
	end
	local total = REACTION_SECONDS[entry.reaction]
	if total == nil then
		entry.reaction = ""
		return "", 0
	end
	entry.reactionTime += dt
	if entry.reactionTime >= total then
		entry.reaction = ""
		return "", 0
	end
	local progress = entry.reactionTime / total
	-- Irkilme HIZLI baslayip sonuyor; digerleri yumusak bir yay.
	local strength = if entry.reaction == "flinch"
		then (1 - progress) * (1 - progress)
		else math.sin(progress * math.pi)
	return entry.reaction, strength
end

-- ── Poz motoru ────────────────────────────────────────────────────────
--
-- Poz DORT KATMANDAN olusuyor; her katman bir oncekinin uzerine biniyor:
--
--   1. TABAN      idle / walk / rest / distress — nefes, adim, kuyruk
--   2. TEPKI      tek seferlik hareketler (irkilme, gevseme, kuyruk...)
--   3. BAKIS      hayvanin basini veterinere/sahibine cevirmesi
--   4. IKINCIL    kulak ve kuyrugun govdeyi GECIKMELI takip etmesi
--
-- Ustune bir de YUMUSATMA geliyor: hicbir eklem hedefine aninda
-- atlamiyor, `drive()` uzerinden Lerp ile gidiyor. Eskiden durum
-- degisince (yuruyusten durusa) poz tek karede ziplayordu.

-- Yumusatma hizlari. Bacaklar hizli olmak zorunda: yavas yumusatma
-- adimin salinim fazini ezip yuruyusu yeniden "kayar" hale getiriyor.
local SMOOTH_BODY = 14
local SMOOTH_LEG = 30
local SMOOTH_HEAD = 10
local SMOOTH_SECONDARY = 8

-- Adim dongusunun DURUS fazi (ayagin yerde oldugu bolum). Gercek
-- dortayaklilarda tirisya yakin bir yuruyuste bu oran ~0.6.
local STANCE = 0.6

--- Bir eklemi hedefe yumusatarak surer ve pozu hatirlar.
local function drive(entry: Entry, name: string, joint: Motor6D, target: CFrame, rate: number, dt: number)
	local previous = entry.pose[name]
	local blended = if previous == nil
		then target
		else previous:Lerp(target, math.clamp(rate * dt, 0, 1))
	entry.pose[name] = blended
	joint.Transform = blended
end

--- Adim dongusu. `p` 0..1 arasi faz. Doner: kalca acisi, diz bukulmesi.
---
--- DURUS fazinda ayak yerde: kalca sabit hizla geriye doner, diz
--- neredeyse duz. Govde ustunden gecerken ayak dunya uzayinda YERINDE
--- duruyor - "foot sliding"i bitiren sey bu.
---
--- SALINIM fazinda kalca hizla one doner ve diz bukulup ayagi
--- kaldirir; ayak havada oldugu icin kaymasi zaten gorunmez.
local function gait(p: number, amplitude: number, knee: number): (number, number)
	if p < STANCE then
		local k = p / STANCE
		return -amplitude + 2 * amplitude * k, knee * 0.08
	end
	local k = (p - STANCE) / (1 - STANCE)
	return amplitude - 2 * amplitude * k, knee * math.sin(k * math.pi)
end

--- Goz kirpma. Kapaklar eklemle degil SAYDAMLIKLA calisiyor
--- (AnimalFactory'deki gerekce), bu yuzden deger yalnizca DEGISTIGINDE
--- yaziliyor: her kare Transparency yazmak bosuna replikasyon olurdu.
local function stepBlink(entry: Entry, dt: number, stress: number)
	local rig = entry.rig
	entry.blinkIn -= dt
	if entry.blinkIn <= 0 and entry.blinkTime < 0 then
		entry.blinkTime = 0
		-- Stresli hayvan daha sik kirpiyor.
		entry.blinkIn = (2.2 + math.random() * 3.4) * (1 - stress * 0.55)
	end

	local closed = 0
	if entry.blinkTime >= 0 then
		entry.blinkTime += dt
		local total = 0.14
		if entry.blinkTime >= total then
			entry.blinkTime = -1
		else
			closed = math.sin(entry.blinkTime / total * math.pi)
		end
	end

	-- Olmek uzere olan hayvanin gozleri yari kapali duruyor: saglik
	-- cubuguna bakmadan once bunu goruyorsun.
	closed = math.max(closed, if entry.state == "distress" then 0.45 else 0)

	local transparency = 1 - closed
	if math.abs(transparency - entry.lidTransparency) < 0.03 then
		return
	end
	entry.lidTransparency = transparency
	for index = 1, 2 do
		local lid = rig.parts["lid" .. index]
		if lid ~= nil then
			lid.Transparency = transparency
		end
	end
end

--- Ikincil hareket: gövdenin IVMESI. Kulak ve kuyruk bunu gecikmeli
--- takip ediyor, yani hayvan durunca kulaklari one savruluyor.
local function stepSecondary(entry: Entry, dt: number): Vector3
	local position = entry.rig.root.Position
	local velocity = (position - entry.lastPosition) / math.max(dt, 1e-4)
	entry.lastPosition = position
	entry.lagVelocity = entry.lagVelocity:Lerp(velocity, math.clamp(6 * dt, 0, 1))
	local swing = velocity - entry.lagVelocity
	-- Govdenin kendi eksenine cevriliyor: saga donuste kulaklar sola
	-- savrulsun.
	return entry.rig.root.CFrame:VectorToObjectSpace(swing)
end

local function stepPose(entry: Entry, dt: number, movingSpeed: number)
	local rig = entry.rig
	local joints = rig.joints
	local walking = entry.state == "walk" and movingSpeed > 0
	local reaction, reactStrength = stepReaction(entry, dt)

	local stress = entry.stressLevel / 100
	local breathRate = 1.4 + stress * 2.2 + (if entry.state == "distress" then 1.6 else 0)
	local strideRate = if walking then math.max(movingSpeed, 1) * 1.15 else 0

	entry.phase += dt * (if walking then strideRate else breathRate)
	local phase = entry.phase

	stepBlink(entry, dt, stress)
	local secondary = stepSecondary(entry, dt)

	-- ── 1. Govde ──────────────────────────────────────────────────────
	if joints.rootJoint ~= nil then
		local breath = math.sin(phase * (if walking then 0.5 else 1)) * (0.035 + stress * 0.03)
		local sway = if walking then math.sin(phase * 2) * 0.03 else 0
		local tilt = entry.limpStrength * math.sin(phase) * 0.05

		-- Agirlik kaydirma: dururken agirlik bir bacaktan digerine
		-- geciyor. Tas gibi duran bir hayvan cansiz gorunuyor.
		if not walking then
			tilt += math.sin(phase * 0.42) * 0.045
		end

		local lift, pitch = 0, 0
		if reaction == "flinch" then
			lift = reactStrength * 0.32
			pitch = -reactStrength * 0.26
		elseif reaction == "relax" then
			lift = -reactStrength * 0.18
		elseif reaction == "wag" then
			lift = math.abs(math.sin(entry.reactionTime * 11)) * reactStrength * 0.16
		elseif reaction == "breathe" then
			lift = math.sin(entry.reactionTime * 2.1) * reactStrength * 0.22
		elseif reaction == "shake" then
			sway += math.sin(entry.reactionTime * 26) * reactStrength * 0.08
		end

		drive(
			entry,
			"rootJoint",
			joints.rootJoint,
			CFrame.new(0, breath + lift, 0) * CFrame.Angles(sway + pitch, 0, tilt),
			SMOOTH_BODY,
			dt
		)
	end

	-- ── 2. Bacaklar: capraz adim dongusu ──────────────────────────────
	-- Capraz cift: 1+4 birlikte, 2+3 birlikte (tiris). Topallayan
	-- bacagin genligi dusuk, durus fazi kisa ve dizi yarim bukuk -
	-- gozle "aksiyor" diye okunuyor.
	local amplitude = if walking then 0.52 else 0
	local kneeFlex = if walking then 0.85 else 0
	for index = 1, 4 do
		local hipJoint = joints["leg" .. index]
		if hipJoint == nil then
			continue
		end
		local shinJoint = joints["leg" .. index .. "Lower"]

		local offset = if index == 2 or index == 3 then 0.5 else 0
		local legAmplitude = amplitude
		local legKnee = kneeFlex
		local injured = 0
		if index == entry.limpLeg then
			legAmplitude *= 1 - entry.limpStrength * 0.7
			legKnee *= 1 - entry.limpStrength * 0.4
			-- Sakat bacak yerde de yarim bukuk duruyor.
			injured = entry.limpStrength * 0.3
		end

		local p = ((phase / (math.pi * 2)) + offset) % 1
		local hip, knee = gait(p, legAmplitude, legKnee)

		if not walking then
			-- Dururken bacaklar duz; yalnizca nefesle cok hafif oynuyor.
			hip = math.sin(phase * 0.5 + index) * 0.02
			knee = injured
		else
			knee += injured
		end

		drive(entry, "leg" .. index, hipJoint, CFrame.Angles(hip, 0, 0), SMOOTH_LEG, dt)
		if shinJoint ~= nil then
			drive(entry, "shin" .. index, shinJoint, CFrame.Angles(knee, 0, 0), SMOOTH_LEG, dt)
		end
	end

	-- ── 3. Kafa: nod + bakis ──────────────────────────────────────────
	if joints.neck ~= nil then
		local nod = if walking then math.sin(phase * 2) * 0.08 else math.sin(phase * 0.55) * 0.05
		local turn = if walking then 0 else math.sin(phase * 0.32) * 0.22
		local droop = (if entry.state == "distress" then 0.2 else 0) + stress * 0.12

		if reaction == "flinch" then
			nod += reactStrength * 0.38
			turn += reactStrength * 0.2
		elseif reaction == "relax" then
			droop += reactStrength * 0.26
		elseif reaction == "shake" then
			turn += math.sin(entry.reactionTime * 24) * reactStrength * 0.55
		elseif reaction == "perk" then
			nod += reactStrength * 0.22
		elseif reaction == "wag" then
			nod += reactStrength * 0.14
		end

		-- BAKIS katmani: hedefe donuyor. Aci sinirli - hayvan basini
		-- 180 derece cevirmiyor, govdesiyle birlikte doner gibi
		-- gorunmesi icin sinir dar tutuldu.
		local target = entry.lookTarget
		if target ~= nil then
			local local3 = rig.root.CFrame:PointToObjectSpace(target)
			local flat = math.sqrt(local3.X * local3.X + local3.Z * local3.Z)
			if flat > 0.5 then
				local yaw = math.clamp(math.atan2(-local3.X, -local3.Z), -0.65, 0.65)
				local pitch = math.clamp(math.atan2(local3.Y, flat), -0.35, 0.45)
				turn += yaw * entry.lookWeight
				nod -= pitch * entry.lookWeight
			end
		end

		drive(entry, "neck", joints.neck, CFrame.Angles(nod - droop, turn, 0), SMOOTH_HEAD, dt)
	end

	-- ── Cene: soluma ──────────────────────────────────────────────────
	-- Stresli ve hasta hayvan agzi acik soluyor; sakin hayvanin agzi
	-- kapali. Bu, saglik cubuguna bakmadan once fark edilen bir isaret.
	if joints.jaw ~= nil then
		local pantRate = 4 + stress * 7
		local pantDepth = (0.02 + stress * 0.16) * (if entry.state == "distress" then 1.6 else 1)
		local open = (math.sin(phase * pantRate) * 0.5 + 0.5) * pantDepth
		if reaction == "flinch" then
			open += reactStrength * 0.3
		elseif reaction == "breathe" then
			open += (math.sin(entry.reactionTime * 2.1) * 0.5 + 0.5) * reactStrength * 0.26
		end
		drive(entry, "jaw", joints.jaw, CFrame.Angles(open, 0, 0), SMOOTH_HEAD, dt)
	end

	-- ── 4. Kuyruk: taban + tepki + IKINCIL ────────────────────────────
	if joints.tail ~= nil then
		local wagAmplitude = (0.45 - stress * 0.35) * (if walking then 1.3 else 1)
		local wag = math.sin(phase * 2.4) * math.max(wagAmplitude, 0.05)
		local lift = -stress * 0.5
		if reaction == "wag" then
			wag = math.sin(entry.reactionTime * 16) * (0.5 + reactStrength * 0.7)
			lift += reactStrength * 0.45
		elseif reaction == "flinch" then
			wag *= 0.2
			lift -= reactStrength * 0.5
		elseif reaction == "relax" then
			wag = math.sin(entry.reactionTime * 3) * 0.3 * reactStrength
		end
		-- Ikincil: govde one atilinca kuyruk geride kaliyor.
		lift += math.clamp(secondary.Z * 0.06, -0.4, 0.4)
		wag += math.clamp(-secondary.X * 0.05, -0.3, 0.3)
		drive(entry, "tail", joints.tail, CFrame.Angles(lift, wag, 0), SMOOTH_SECONDARY, dt)
	end

	-- ── Kulaklar: stres + seyirme + IKINCIL ───────────────────────────
	for index = 1, 2 do
		local joint = joints["ear" .. index]
		if joint == nil then
			continue
		end
		local side = if index == 1 then -1 else 1
		local twitch = math.sin(phase * 3.1 + index) * 0.05
		local flatten = stress * 0.55
		if reaction == "flinch" then
			flatten += reactStrength * 0.5
		elseif reaction == "perk" or reaction == "wag" then
			flatten -= reactStrength * 0.45
		elseif reaction == "shake" then
			twitch += math.sin(entry.reactionTime * 22 + index) * reactStrength * 0.3
		end
		-- Ikincil: ani hizlanma ve donuste kulaklar savruluyor.
		flatten -= math.clamp(secondary.Z * 0.05, -0.35, 0.35)
		local sway = math.clamp(-secondary.X * 0.07, -0.4, 0.4)
		drive(
			entry,
			"ear" .. index,
			joint,
			CFrame.Angles(-flatten, 0, side * (twitch + stress * 0.25) + sway),
			SMOOTH_SECONDARY,
			dt
		)
	end

	-- ── Yilan govdesi ─────────────────────────────────────────────────
	-- Halkalar boyunca ilerleyen bir dalga: faz kaymasi halkadan halkaya
	-- sabit, dalga bastan kuyruga akiyor - hayvan kendi uzerinde
	-- SURUNUYOR gibi gorunuyor. Dururken de kisik bir genlikle devam
	-- ediyor: canli bir yilan tas gibi durmaz.
	do
		local index = 2
		local waveAmplitude = if walking then 0.42 else 0.12
		while true do
			local joint = joints["segment" .. index]
			if joint == nil then
				break
			end
			local target
			if entry.coiled then
				target = CFrame.Angles(0, 0.62 + math.sin(phase * 0.8 - index * 0.4) * 0.04, 0)
			else
				local offset = (index - 1) * 0.7
				target = CFrame.Angles(0, math.sin(phase * 1.6 - offset) * waveAmplitude, 0)
			end
			drive(entry, "segment" .. index, joint, target, SMOOTH_BODY, dt)
			index += 1
		end
	end

	-- ── Kanatlar ──────────────────────────────────────────────────────
	for index = 1, 2 do
		local joint = joints["wing" .. index]
		if joint ~= nil then
			local side = if index == 1 then -1 else 1
			local flap = if walking then math.abs(math.sin(phase * 3)) * 0.6 else math.sin(phase) * 0.06
			-- Ikincil: kanat ucu govdenin ivmesini gecikmeli takip ediyor.
			flap += math.clamp(secondary.Z * 0.04, -0.25, 0.25)
			drive(entry, "wing" .. index, joint, CFrame.Angles(0, 0, side * -flap), SMOOTH_SECONDARY, dt)
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
			-- Duran serpentine rig kendiliginden kiviriliyor: bekleme
			-- ayak izi 7,2 degil ~3 stud oluyor (data: tableFootprint).
			if entry.rig.spec.rig == "serpentine" and #entry.waypoints == 0 then
				entry.coiled = true
			end
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
