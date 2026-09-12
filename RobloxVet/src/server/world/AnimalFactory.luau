--!strict
-- AnimalFactory.luau
-- ==================
-- Hasta hayvanlarin rigini kurar. Butun oranlar data/animals.json'dan
-- geliyor; yeni bir tur eklemek icin KOD DEGISTIRMEK GEREKMIYOR, veri
-- tablosuna bir satir yetiyor (rig alani bilinen bir iskelet adi olmak
-- kaydiyla: quadruped, bird, shelled).
--
-- Neden Humanoid yok?
-- -------------------
-- Humanoid saglik cubugu, isim etiketi ve kendi fizigini getiriyor;
-- hastanin sagligi bizim oyun kuralimiz, Humanoid'in degil. Bunun
-- yerine: capasi atilmis gorunmez bir Root parcasi, ustune Motor6D
-- zinciri. Hareket Root'un CFrame'i ile, animasyon eklem Transform'lari
-- ile yapiliyor (AnimalAnimator).
--
-- Motor6D pivot matematigi
-- ------------------------
-- Parcalar once son konumlarina kuruluyor, sonra eklem SU ANKI duruma
-- gore hesaplaniyor: C0 ve C1, ortak bir dunya pivotunun iki parcanin
-- yerel uzayindaki karsiligi. Boylece Transform = identity iken model
-- kuruldugu gibi duruyor, Transform dondurulunce parca PIVOT ETRAFINDA
-- donuyor (kendi merkezi etrafinda degil) - bacak kalcadan, kulak
-- kokunden.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Assets = require(Shared.Assets)
local Build = require(Shared.Build)
local Loc = require(Shared.Loc)

local animalData = require(Data.animals)

local AnimalFactory = {}

export type Rig = {
	model: Model,
	root: Part,
	parts: { [string]: BasePart },
	joints: { [string]: Motor6D },
	spec: any,
	nameplate: BillboardGui,
}

local byId: { [string]: any } = {}
for _, animal in animalData.animals do
	byId[animal.id] = animal
end

--- Tur tanimini getirir.
function AnimalFactory.spec(id: string): any?
	return byId[id]
end

--- Butun tur kimlikleri (SelfTest ve PatientFlow icin).
function AnimalFactory.allIds(): { string }
	local ids = {}
	for _, animal in animalData.animals do
		table.insert(ids, animal.id)
	end
	return ids
end

local KNOWN_RIGS = { quadruped = true, bird = true, shelled = true, serpentine = true }

--- Iskelet adi taniniyor mu? verify_data.py ve SelfTest bunu soruyor.
function AnimalFactory.knownRig(rig: string): boolean
	return KNOWN_RIGS[rig] == true
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

--- Hayvanin ustundeki isim + saglik/stres cubugu.
local function buildNameplate(root: BasePart, nameKey: string): BillboardGui
	local billboard = Instance.new("BillboardGui")
	billboard.Name = "Etiket"
	billboard.Size = UDim2.fromOffset(190, 62)
	billboard.StudsOffsetWorldSpace = Vector3.new(0, 3.6, 0)
	billboard.AlwaysOnTop = true
	billboard.MaxDistance = 90
	billboard.Parent = root

	local frame = Instance.new("Frame")
	frame.Size = UDim2.fromScale(1, 1)
	frame.BackgroundColor3 = Color3.fromRGB(24, 28, 36)
	frame.BackgroundTransparency = 0.25
	frame.BorderSizePixel = 0
	frame.Parent = billboard
	local corner = Instance.new("UICorner")
	corner.CornerRadius = UDim.new(0, 8)
	corner.Parent = frame

	local title = Instance.new("TextLabel")
	title.Name = "Baslik"
	title.Size = UDim2.new(1, -12, 0, 22)
	title.Position = UDim2.fromOffset(6, 4)
	title.BackgroundTransparency = 1
	title.Font = Enum.Font.GothamBold
	title.TextSize = 15
	title.TextColor3 = Color3.fromRGB(242, 246, 250)
	title.Text = Loc.get(nameKey)
	title.Parent = frame

	local function bar(name: string, y: number, color: Color3): Frame
		local track = Instance.new("Frame")
		track.Name = name
		track.Size = UDim2.new(1, -12, 0, 10)
		track.Position = UDim2.fromOffset(6, y)
		track.BackgroundColor3 = Color3.fromRGB(52, 58, 70)
		track.BorderSizePixel = 0
		track.Parent = frame
		local trackCorner = Instance.new("UICorner")
		trackCorner.CornerRadius = UDim.new(0, 5)
		trackCorner.Parent = track

		local fill = Instance.new("Frame")
		fill.Name = "Dolgu"
		fill.Size = UDim2.fromScale(1, 1)
		fill.BackgroundColor3 = color
		fill.BorderSizePixel = 0
		fill.Parent = track
		local fillCorner = Instance.new("UICorner")
		fillCorner.CornerRadius = UDim.new(0, 5)
		fillCorner.Parent = fill
		return track
	end

	bar("Saglik", 28, Color3.fromRGB(96, 196, 120))
	bar("Stres", 42, Color3.fromRGB(226, 148, 76))

	return billboard
end

--- Kafa: kafatasi + burun + gozler + kulaklar.
local function buildHead(
	model: Model,
	torso: BasePart,
	spec: any,
	palette: { [string]: Color3 },
	joints: { [string]: Motor6D },
	parts: { [string]: BasePart }
): BasePart
	local head = spec.head
	local headPos = torso.Position + Vector3.new(0, head.offsetY, head.offsetZ)

	local skull = Build.part({
		name = "Kafa",
		size = Vector3.new(head.size, head.size, head.size),
		cf = CFrame.new(headPos),
		color = palette.body,
		material = Enum.Material.SmoothPlastic,
		shape = Enum.PartType.Ball,
		canCollide = false,
		anchored = false,
		parent = model,
	})
	parts.head = skull

	-- Boyun/kafa eklemi torso ile kafa arasindaki orta noktada.
	joints.neck = rigJoint("Neck", torso, skull, headPos - Vector3.new(0, head.size * 0.35, head.size * 0.3))

	if head.snoutLength > 0 then
		local snout = Build.part({
			name = "Burun",
			size = Vector3.new(head.size * 0.55, head.snoutHeight, head.snoutLength),
			cf = CFrame.new(headPos + Vector3.new(0, -head.size * 0.14, head.size * 0.42 + head.snoutLength * 0.5)),
			color = palette.belly,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		rigidWeld(skull, snout)
		parts.snout = snout

		local nose = Build.ball({
			name = "BurunUcu",
			size = Vector3.new(head.snoutHeight * 0.55, head.snoutHeight * 0.55, head.snoutHeight * 0.55),
			cf = CFrame.new(headPos + Vector3.new(0, -head.size * 0.1, head.size * 0.42 + head.snoutLength)),
			color = palette.nose,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		rigidWeld(skull, nose)
	end

	for _, side in { -1, 1 } do
		local eye = Build.ball({
			name = "Goz",
			size = Vector3.new(head.size * 0.2, head.size * 0.2, head.size * 0.2),
			cf = CFrame.new(headPos + Vector3.new(side * head.size * 0.26, head.size * 0.14, head.size * 0.4)),
			color = palette.eye,
			material = Enum.Material.Glass,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		rigidWeld(skull, eye)
	end

	local ears = spec.ears
	if ears.type ~= "none" and ears.length > 0 then
		for index, side in { -1, 1 } do
			local rootPos = headPos + Vector3.new(side * head.size * 0.32, head.size * 0.4, -head.size * 0.05)
			local ear: BasePart
			if ears.type == "pointy" then
				ear = Build.wedge({
					name = "Kulak",
					size = Vector3.new(ears.width, ears.length, ears.width * 0.5),
					cf = CFrame.new(rootPos + Vector3.new(0, ears.length * 0.5, 0)),
					color = palette.ear,
					canCollide = false,
					anchored = false,
					parent = model,
				})
			elseif ears.type == "floppy" then
				ear = Build.part({
					name = "Kulak",
					size = Vector3.new(ears.width * 0.4, ears.length, ears.width),
					cf = CFrame.new(rootPos + Vector3.new(side * 0.1, -ears.length * 0.3, 0))
						* CFrame.Angles(0, 0, math.rad(side * -14)),
					color = palette.ear,
					canCollide = false,
					anchored = false,
					parent = model,
				})
			elseif ears.type == "round" then
				ear = Build.ball({
					name = "Kulak",
					size = Vector3.new(ears.length, ears.length, ears.width * 0.4),
					cf = CFrame.new(rootPos),
					color = palette.ear,
					canCollide = false,
					anchored = false,
					parent = model,
				})
			else -- "long" (tavsan)
				ear = Build.part({
					name = "Kulak",
					size = Vector3.new(ears.width, ears.length, ears.width * 0.55),
					cf = CFrame.new(rootPos + Vector3.new(0, ears.length * 0.5, 0))
						* CFrame.Angles(math.rad(-8), 0, math.rad(side * 7)),
					color = palette.ear,
					canCollide = false,
					anchored = false,
					parent = model,
				})
			end
			joints["ear" .. index] = rigJoint("Ear" .. index, skull, ear, rootPos)
			parts["ear" .. index] = ear
		end
	end

	return skull
end

local function buildLegs(
	model: Model,
	torso: BasePart,
	spec: any,
	palette: { [string]: Color3 },
	joints: { [string]: Motor6D },
	parts: { [string]: BasePart }
)
	local legs = spec.legs
	if legs.count <= 0 or legs.length <= 0 then
		return
	end

	local zOffsets = if legs.count == 2 then { 0 } else { legs.spreadZ, -legs.spreadZ }
	local index = 0
	for _, z in zOffsets do
		for _, side in { -1, 1 } do
			index += 1
			local hip = torso.Position + Vector3.new(side * legs.spreadX, -spec.body.height * 0.4, z)
			local leg = Build.part({
				name = "Bacak" .. index,
				size = Vector3.new(legs.thickness, legs.length, legs.thickness),
				cf = CFrame.new(hip - Vector3.new(0, legs.length * 0.5, 0)),
				color = palette.accent,
				canCollide = false,
				anchored = false,
				parent = model,
			})
			-- Patiler: bacagin ucundaki kucuk kure, gozle fark ediliyor.
			local paw = Build.ball({
				name = "Pati" .. index,
				size = Vector3.new(legs.thickness * 1.25, legs.thickness * 0.9, legs.thickness * 1.35),
				cf = CFrame.new(hip - Vector3.new(0, legs.length - legs.thickness * 0.3, 0)),
				color = palette.belly,
				canCollide = false,
				anchored = false,
				parent = model,
			})
			rigidWeld(leg, paw)

			joints["leg" .. index] = rigJoint("Leg" .. index, torso, leg, hip)
			parts["leg" .. index] = leg
		end
	end
end

local function buildTail(
	model: Model,
	torso: BasePart,
	spec: any,
	palette: { [string]: Color3 },
	joints: { [string]: Motor6D },
	parts: { [string]: BasePart }
)
	local tail = spec.tail
	if tail.type == "none" or tail.length <= 0 then
		return
	end

	local rootPos = torso.Position + Vector3.new(0, spec.body.height * 0.25, -spec.body.length * 0.5)
	local part: BasePart

	if tail.type == "puff" then
		part = Build.ball({
			name = "Kuyruk",
			size = Vector3.new(tail.thickness * 2, tail.thickness * 2, tail.thickness * 2),
			cf = CFrame.new(rootPos - Vector3.new(0, 0, tail.length)),
			color = palette.belly,
			canCollide = false,
			anchored = false,
			parent = model,
		})
	elseif tail.type == "feather" then
		part = Build.part({
			name = "Kuyruk",
			size = Vector3.new(tail.thickness, 0.2, tail.length),
			cf = CFrame.new(rootPos - Vector3.new(0, tail.length * 0.25, tail.length * 0.5))
				* CFrame.Angles(math.rad(-20), 0, 0),
			color = palette.accent,
			canCollide = false,
			anchored = false,
			parent = model,
		})
	else -- "wag"
		part = Build.part({
			name = "Kuyruk",
			size = Vector3.new(tail.thickness, tail.thickness, tail.length),
			cf = CFrame.new(rootPos - Vector3.new(0, -tail.length * 0.2, tail.length * 0.5))
				* CFrame.Angles(math.rad(22), 0, 0),
			color = palette.body,
			canCollide = false,
			anchored = false,
			parent = model,
		})
	end

	joints.tail = rigJoint("Tail", torso, part, rootPos)
	parts.tail = part
end

--- Yilan govdesi: birbirine Motor6D ile bagli halkalar. Kuyruga dogru
--- incelir. Ilk halka govde yerine geciyor, kafa ona takiliyor.
---
--- Neden ayri bir iskelet? Dortayakli govdesi tek bir kutu; yilanin
--- kivrilmasi icin govdenin KENDISININ eklemli olmasi gerekiyor.
local function buildSerpentine(
	model: Model,
	root: BasePart,
	spec: any,
	palette: { [string]: Color3 },
	joints: { [string]: Motor6D },
	parts: { [string]: BasePart },
	torsoY: number
): BasePart
	local count = spec.segments.count
	local taper = spec.segments.taper
	local segmentLength = spec.body.length / count

	local previous: BasePart? = nil
	local firstSegment: BasePart? = nil

	for index = 1, count do
		-- Genislik bastan sona dogrusal olarak `taper` oranina iniyor.
		local shrink = 1 - (1 - taper) * ((index - 1) / math.max(count - 1, 1))
		local width = spec.body.width * shrink
		local z = spec.body.length * 0.5 - segmentLength * (index - 0.5)

		local segment = Build.part({
			name = "Halka" .. index,
			size = Vector3.new(width, spec.body.height * shrink, segmentLength),
			cf = CFrame.new(0, torsoY, z),
			color = if index % 2 == 0 then palette.accent else palette.body,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		parts["segment" .. index] = segment

		if previous == nil then
			firstSegment = segment
			joints.rootJoint = rigJoint("RootJoint", root, segment, Vector3.new(0, torsoY, z))
		else
			-- Eklem iki halkanin ARASINDA: burasi kivrilma noktasi.
			local pivot = Vector3.new(0, torsoY, z + segmentLength * 0.5)
			joints["segment" .. index] = rigJoint("Segment" .. index, previous, segment, pivot)
		end
		previous = segment
	end

	return firstSegment :: BasePart
end

--- Kirpi dikenleri: govdenin ustune yayilmis kucuk koniler (kama).
local function buildSpines(model: Model, torso: BasePart, spec: any, palette: { [string]: Color3 })
	local config = spec.spines
	local rows = 4
	local perRow = math.max(1, math.floor(config.count / rows))

	for row = 1, rows do
		for index = 1, perRow do
			-- Govdenin ust yarisina, boyuna ve enine yayiliyor.
			local along = (index - 0.5) / perRow - 0.5
			local across = (row - 0.5) / rows - 0.5
			local spine = Build.part({
				name = "Diken",
				size = Vector3.new(config.thickness, config.length, config.thickness),
				cf = torso.CFrame
					* CFrame.new(
						across * spec.body.width * 0.85,
						spec.body.height * 0.45 + config.length * 0.4,
						along * spec.body.length * 0.85
					)
					* CFrame.Angles(math.rad(across * -34), 0, math.rad(across * 34)),
				color = palette.accent,
				canCollide = false,
				anchored = false,
				parent = model,
			})
			rigidWeld(torso, spine)
		end
	end
end

--- Iguana sirt yelesi: govdenin uzerinde sirtboyu dizilmis ince plakalar.
local function buildCrest(model: Model, torso: BasePart, spec: any, palette: { [string]: Color3 })
	local config = spec.crest
	for index = 1, config.count do
		local along = (index - 0.5) / config.count - 0.5
		-- Ortadaki plakalar en uzun: hayvan siluetine bir tepe veriyor.
		local scale = 0.55 + 0.45 * math.cos(along * math.pi)
		local plate = Build.part({
			name = "Yele",
			size = Vector3.new(config.thickness, config.height * scale, spec.body.length / config.count * 0.6),
			cf = torso.CFrame
				* CFrame.new(0, spec.body.height * 0.5 + config.height * scale * 0.5, along * spec.body.length * 0.9),
			color = palette.accent,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		rigidWeld(torso, plate)
	end
end

--- At yelesi: boyun boyunca kafaya dogru inen tutamlar.
local function buildMane(model: Model, torso: BasePart, head: BasePart, spec: any, palette: { [string]: Color3 })
	local config = spec.mane
	for index = 1, config.count do
		local ratio = (index - 0.5) / config.count
		local position = torso.Position:Lerp(head.Position, ratio)
		local tuft = Build.part({
			name = "Yele",
			size = Vector3.new(config.thickness, config.height, spec.body.length / config.count * 0.5),
			cf = CFrame.new(position + Vector3.new(0, spec.body.height * 0.45, 0)),
			color = palette.accent,
			material = Enum.Material.Fabric,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		rigidWeld(torso, tuft)
	end
end

--- Tam rigi kurar. `cframe` hayvanin ayaklarinin basacagi yer.
function AnimalFactory.create(animalId: string, cframe: CFrame, parent: Instance): Rig?
	local spec = byId[animalId]
	if spec == nil then
		warn(("[AnimalFactory] bilinmeyen tur: '%s'"):format(animalId))
		return nil
	end
	if not AnimalFactory.knownRig(spec.rig) then
		warn(("[AnimalFactory] '%s' bilinmeyen iskelet istiyor: '%s'"):format(animalId, spec.rig))
		return nil
	end

	local palette = {}
	for key, value in spec.palette do
		palette[key] = Build.color(value)
	end

	local model = Build.model(animalId, parent)
	local parts: { [string]: BasePart } = {}
	local joints: { [string]: Motor6D } = {}

	local body = spec.body
	-- Govde merkezinin yerden yuksekligi. Yilanda bacak yok, govde
	-- dogrudan zeminde: legs.length = 0 oldugu icin formul ayni kaliyor.
	local groundClearance = spec.legs.length
	local torsoY = groundClearance + body.height * 0.5

	-- Model ONCE dunya merkezinde kuruluyor; hesaplar boylece duz kaliyor.
	-- Sonunda Root'un CFrame'i istenen yere tasiniyor ve zincir onu izliyor.
	local root = Build.part({
		name = "Root",
		size = Vector3.new(body.width, 0.4, body.length),
		cf = CFrame.new(0, torsoY, 0),
		transparency = 1,
		canCollide = false,
		canQuery = false,
		castShadow = false,
		anchored = true,
		parent = model,
	})
	parts.root = root

	local torso: BasePart
	if spec.rig == "serpentine" then
		torso = buildSerpentine(model, root, spec, palette, joints, parts, torsoY)
		parts.torso = torso
	else
		torso = Build.part({
			name = "Govde",
			size = Vector3.new(body.width, body.height, body.length),
			cf = CFrame.new(0, torsoY, 0),
			color = palette.body,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		parts.torso = torso
		joints.rootJoint = rigJoint("RootJoint", root, torso, Vector3.new(0, torsoY, 0))

		-- Karin: govdenin altinda acik renk serit. Tek renkli bir kutu yerine
		-- iki tonlu govde hayvani gozle ayirt edilir kiliyor.
		local belly = Build.part({
			name = "Karin",
			size = Vector3.new(body.width * 0.72, body.height * 0.45, body.length * 0.82),
			cf = CFrame.new(0, torsoY - body.height * 0.32, body.length * 0.04),
			color = palette.belly,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		rigidWeld(torso, belly)
	end

	buildHead(model, torso, spec, palette, joints, parts)
	buildLegs(model, torso, spec, palette, joints, parts)
	buildTail(model, torso, spec, palette, joints, parts)

	-- Istege bagli suslemeler: yalnizca veri tablosunda tanimliysa kuruluyor.
	if spec.spines ~= nil then
		buildSpines(model, torso, spec, palette)
	end
	if spec.crest ~= nil then
		buildCrest(model, torso, spec, palette)
	end
	if spec.mane ~= nil and parts.head ~= nil then
		buildMane(model, torso, parts.head, spec, palette)
	end

	if spec.rig == "bird" and spec.wings ~= nil then
		for index, side in { -1, 1 } do
			local pivot = torso.Position + Vector3.new(side * body.width * 0.45, body.height * 0.15, 0)
			local wing = Build.part({
				name = "Kanat" .. index,
				size = Vector3.new(spec.wings.thickness, spec.wings.height, spec.wings.length),
				cf = CFrame.new(pivot + Vector3.new(side * 0.15, -spec.wings.height * 0.2, 0)),
				color = palette.accent,
				canCollide = false,
				anchored = false,
				parent = model,
			})
			joints["wing" .. index] = rigJoint("Wing" .. index, torso, wing, pivot)
			parts["wing" .. index] = wing
		end
	end

	if spec.rig == "shelled" and spec.shell ~= nil then
		local shell = Build.ball({
			name = "Kabuk",
			size = Vector3.new(
				body.width + spec.shell.overhang * 2,
				spec.shell.height * 2,
				body.length + spec.shell.overhang * 2
			),
			cf = CFrame.new(0, torsoY + body.height * 0.1, 0),
			color = palette.accent,
			material = Enum.Material.Slate,
			canCollide = false,
			anchored = false,
			parent = model,
		})
		rigidWeld(torso, shell)
		parts.shell = shell
	end

	local nameplate = buildNameplate(root, spec.nameKey)

	model.PrimaryPart = root
	root.CFrame = cframe * CFrame.new(0, torsoY, 0)

	-- Kullanici bir mesh ID yazdiysa prosedurel govdeyi gizleyip mesh'i
	-- koyuyoruz; tablo bos oldugunda (varsayilan) hicbir sey degismez.
	local meshId = Assets.mesh(animalId)
	if meshId ~= nil then
		local swap = Instance.new("MeshPart")
		swap.Name = "CreatorStoreMesh"
		swap.Anchored = false
		swap.CanCollide = false
		swap.CFrame = root.CFrame
		swap.Parent = model
		local ok = pcall(function()
			(swap :: any).MeshId = meshId
		end)
		if ok then
			rigidWeld(root, swap)
			for key, part in parts do
				if key ~= "root" then
					part.Transparency = 1
				end
			end
		else
			warn(("[AnimalFactory] '%s' icin mesh yuklenemedi, prosedurel modele donuldu"):format(animalId))
			swap:Destroy()
		end
	end

	return {
		model = model,
		root = root,
		parts = parts,
		joints = joints,
		spec = spec,
		nameplate = nameplate,
	}
end

return AnimalFactory
