--!strict
-- Clinic.luau
-- ===========
-- Klinik binasini kurar: zemin, duvarlar (kapi bosluklariyla), tavan,
-- cati, bahce ve yol. Butun olculer data/clinic.json'dan geliyor.
--
-- Kapi bosluklari
-- ---------------
-- Bir duvar tek bir kutu degil; kapilarin iki yanindaki parcalara ve
-- kapinin ustundeki kirise (lento) bolunuyor. Boylece plan dosyasina
-- kapi eklemek/tasimak icin kod degistirmek gerekmiyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Build = require(Shared.Build)
local Loc = require(Shared.Loc)
local Props = require(script.Parent.Props)

local plan = require(Data.clinic)

local Clinic = {}

export type Stations = { [string]: { cframe: CFrame, model: Model } }

export type Built = {
	model: Model,
	stations: Stations,
	queuePoints: { CFrame },
	restPoints: { CFrame },
	entranceOutside: Vector3,
	entranceInside: Vector3,
	roomOf: { [string]: { min: { number }, max: { number } } },
}

local EPSILON = 1e-3

local function colorOf(name: string): Color3
	return Build.color(plan.colors[name])
end

--- Malzeme adini Enum'a cevirir. Veri dosyasinda metin tutuluyor ki
--- plan dosyasi kod bilmeden duzenlenebilsin; bilinmeyen ad sessizce
--- yutulmaz, SmoothPlastic'e duser ve uyari basar.
local function materialOf(name: string?): Enum.Material
	if name == nil then
		return Enum.Material.SmoothPlastic
	end
	local material = (Enum.Material :: any)[name]
	if material == nil then
		warn(("[Clinic] bilinmeyen malzeme '%s' - SmoothPlastic kullanildi"):format(name))
		return Enum.Material.SmoothPlastic
	end
	return material
end

--- Tek bir duvar parcasi. `horizontal` ise eksen X, degilse Z.
local function wallSegment(
	parent: Instance,
	horizontal: boolean,
	fixed: number,
	axisFrom: number,
	axisTo: number,
	yFrom: number,
	yTo: number,
	color: Color3
)
	local length = axisTo - axisFrom
	if length <= EPSILON then
		return
	end
	local height = yTo - yFrom
	if height <= EPSILON then
		return
	end

	local center = (axisFrom + axisTo) * 0.5
	local y = (yFrom + yTo) * 0.5
	local thickness = plan.wallThickness

	local size, position
	if horizontal then
		size = Vector3.new(length, height, thickness)
		position = Vector3.new(center, y, fixed)
	else
		size = Vector3.new(thickness, height, length)
		position = Vector3.new(fixed, y, center)
	end

	Build.part({
		name = "Duvar",
		size = size,
		cf = CFrame.new(position),
		color = color,
		material = Enum.Material.SmoothPlastic,
		parent = parent,
	})
end

--- Kapi cercevesi: iki dikme + ust kiris. Bos bir delik yerine cerceve
--- koymak binayi bitmis gosteriyor.
local function doorFrame(
	parent: Instance,
	horizontal: boolean,
	fixed: number,
	at: number,
	width: number,
	color: Color3
)
	local thickness = plan.wallThickness + 0.15
	local postWidth = 0.5
	local top = plan.doorHeight

	local function piece(axisCenter: number, axisSize: number, y: number, ySize: number)
		local size, position
		if horizontal then
			size = Vector3.new(axisSize, ySize, thickness)
			position = Vector3.new(axisCenter, y, fixed)
		else
			size = Vector3.new(thickness, ySize, axisSize)
			position = Vector3.new(fixed, y, axisCenter)
		end
		Build.part({
			name = "KapiCercevesi",
			size = size,
			cf = CFrame.new(position),
			color = color,
			material = Enum.Material.SmoothPlastic,
			canCollide = false,
			parent = parent,
		})
	end

	piece(at - width * 0.5 - postWidth * 0.5, postWidth, top * 0.5, top)
	piece(at + width * 0.5 + postWidth * 0.5, postWidth, top * 0.5, top)
	piece(at, width + postWidth * 2, top + 0.25, 0.5)
end

local function buildWall(parent: Instance, wall: any, color: Color3, trim: Color3)
	local from, to = wall.from, wall.to
	local horizontal = math.abs(to[2] - from[2]) < EPSILON

	local axisFrom, axisTo, fixed
	if horizontal then
		axisFrom = math.min(from[1], to[1])
		axisTo = math.max(from[1], to[1])
		fixed = from[2]
	else
		axisFrom = math.min(from[2], to[2])
		axisTo = math.max(from[2], to[2])
		fixed = from[1]
	end

	local doors = table.clone(wall.doors or {})
	table.sort(doors, function(a, b)
		return a.at < b.at
	end)

	local cursor = axisFrom
	for _, door in doors do
		local left = door.at - door.width * 0.5
		local right = door.at + door.width * 0.5
		wallSegment(parent, horizontal, fixed, cursor, left, 0, plan.wallHeight, color)
		-- Kapinin ustundeki kiris
		wallSegment(parent, horizontal, fixed, left, right, plan.doorHeight, plan.wallHeight, color)
		doorFrame(parent, horizontal, fixed, door.at, door.width, trim)
		cursor = right
	end
	wallSegment(parent, horizontal, fixed, cursor, axisTo, 0, plan.wallHeight, color)

	-- Supurgelik: duvarin dibinde ince bir serit. Tek renkli duvarlari
	-- kirar, mekan daha bitmis gorunur.
	wallSegment(parent, horizontal, fixed, axisFrom, axisTo, 0, 0.6, trim)
end

--- Bahce, yol ve disarisi.
local function buildGrounds(parent: Instance)
	local half = plan.groundSize * 0.5
	Build.slab(
		{ -half, -half },
		{ half, half },
		plan.floorY - 0.5,
		1,
		colorOf("ground"),
		Enum.Material.Grass,
		parent,
		"Zemin"
	)

	local path = plan.outdoor.path
	local halfWidth = path.width * 0.5
	local zMin = math.min(path.from[2], path.to[2])
	local zMax = math.max(path.from[2], path.to[2])
	Build.slab(
		{ path.from[1] - halfWidth, zMin },
		{ path.from[1] + halfWidth, zMax },
		plan.floorY + 0.02,
		0.3,
		colorOf("path"),
		Enum.Material.Concrete,
		parent,
		"Yol"
	)
end

--- Duz cati + korkuluk. Bina disaridan bitmis gorunsun diye.
local function buildRoof(parent: Instance)
	local min, max = plan.bounds.min, plan.bounds.max
	local overhang = 1.5
	Build.slab(
		{ min[1] - overhang, min[2] - overhang },
		{ max[1] + overhang, max[2] + overhang },
		plan.wallHeight + 0.5,
		1,
		colorOf("roof"),
		Enum.Material.Slate,
		parent,
		"Cati"
	)

	-- Korkuluk: catinin dort kenarinda alcak duvar.
	local parapet = 1.6
	local y = plan.wallHeight + 1 + parapet * 0.5
	local sides = {
		{ Vector3.new(max[1] - min[1] + overhang * 2, parapet, 0.8), Vector3.new((min[1] + max[1]) * 0.5, y, min[2] - overhang) },
		{ Vector3.new(max[1] - min[1] + overhang * 2, parapet, 0.8), Vector3.new((min[1] + max[1]) * 0.5, y, max[2] + overhang) },
		{ Vector3.new(0.8, parapet, max[2] - min[2] + overhang * 2), Vector3.new(min[1] - overhang, y, (min[2] + max[2]) * 0.5) },
		{ Vector3.new(0.8, parapet, max[2] - min[2] + overhang * 2), Vector3.new(max[1] + overhang, y, (min[2] + max[2]) * 0.5) },
	}
	for _, side in sides do
		Build.part({
			name = "Korkuluk",
			size = side[1],
			cf = CFrame.new(side[2]),
			color = colorOf("wallTrim"),
			material = Enum.Material.SmoothPlastic,
			parent = parent,
		})
	end
end

--- Oyuncunun dogacagi yer. Dunya kurulmadan kimse dogmuyor
--- (Players.CharacterAutoLoads yer dosyasinda false; init.server.luau
--- kurulum bitince true yapiyor).
local function buildSpawn(parent: Instance)
	local spawnPoint = Instance.new("SpawnLocation")
	spawnPoint.Name = "VeterinerSpawn"
	spawnPoint.Size = Vector3.new(12, 1, 12)
	spawnPoint.CFrame = CFrame.new(plan.spawn.at[1], plan.floorY + 0.5, plan.spawn.at[2])
	spawnPoint.Anchored = true
	spawnPoint.Color = Build.color(plan.colors.path)
	spawnPoint.Material = Enum.Material.Concrete
	spawnPoint.TopSurface = Enum.SurfaceType.Smooth
	spawnPoint.BottomSurface = Enum.SurfaceType.Smooth
	spawnPoint.Neutral = true
	spawnPoint.Duration = 0
	spawnPoint.Parent = parent
	return spawnPoint
end

--- Tam klinigi kurar ve oyun modullerinin ihtiyac duydugu noktalari doner.
function Clinic.build(parent: Instance): Built
	local root = Build.model("Klinik", parent)

	local structure = Build.model("Yapi", root)
	local furniture = Build.model("Esyalar", root)

	buildGrounds(structure)

	local wallColor = colorOf("wall")
	local trimColor = colorOf("wallTrim")

	local stations: Stations = {}
	local queuePoints: { CFrame } = {}
	local restPoints: { CFrame } = {}
	local roomOf = {}

	for _, room in plan.rooms do
		roomOf[room.id] = { min = room.min, max = room.max }

		Build.slab(
			room.min,
			room.max,
			plan.floorY - 0.25,
			0.5,
			Build.color(room.floorColor),
			materialOf(room.floorMaterial),
			structure,
			"Doseme_" .. room.id
		)

		if plan.ceiling then
			Build.slab(
				room.min,
				room.max,
				plan.wallHeight - 0.25,
				0.5,
				colorOf("ceiling"),
				Enum.Material.SmoothPlastic,
				structure,
				"Tavan_" .. room.id
			)
		end

		for _, position in room.lights or {} do
			Build.ceilingLight(
				Vector3.new(position[1], plan.wallHeight - 0.8, position[2]),
				structure
			)
		end

		-- Oda adi tabelasi: odanin orta noktasinin ustunde asili.
		local centerX = (room.min[1] + room.max[1]) * 0.5
		local centerZ = (room.min[2] + room.max[2]) * 0.5
		local plate = Build.part({
			name = "OdaTabelasi_" .. room.id,
			size = Vector3.new(9, 1.8, 0.25),
			cf = CFrame.new(centerX, plan.wallHeight - 2.6, centerZ),
			color = trimColor,
			material = Enum.Material.SmoothPlastic,
			canCollide = false,
			parent = structure,
		})
		Build.surfaceText(plate, Enum.NormalId.Front, Loc.get(room.nameKey), Color3.fromRGB(48, 54, 64))
		Build.surfaceText(plate, Enum.NormalId.Back, Loc.get(room.nameKey), Color3.fromRGB(48, 54, 64))

		for _, point in room.queuePoints or {} do
			table.insert(queuePoints, CFrame.new(point[1], plan.floorY, point[2]))
		end
		for _, point in room.restPoints or {} do
			table.insert(restPoints, CFrame.new(point[1], plan.floorY, point[2]))
		end

		for _, spec in room.props do
			local origin = CFrame.new(spec.at[1], plan.floorY, spec.at[2])
				* CFrame.Angles(0, math.rad(spec.rot or 0), 0)
			local model = Props.build(spec.id, origin, furniture)
			if model ~= nil and spec.tag ~= nil then
				local stationId = string.match(spec.tag, "^station:(.+)$")
				if stationId ~= nil then
					stations[stationId] = { cframe = origin, model = model }
				else
					model:AddTag(spec.tag)
				end
			end
		end
	end

	for _, wall in plan.walls do
		buildWall(structure, wall, wallColor, trimColor)
	end

	buildRoof(structure)

	for _, spec in plan.outdoor.props do
		local origin = CFrame.new(spec.at[1], plan.floorY, spec.at[2])
			* CFrame.Angles(0, math.rad(spec.rot or 0), 0)
		Props.build(spec.id, origin, furniture)
	end

	buildSpawn(root)

	return {
		model = root,
		stations = stations,
		queuePoints = queuePoints,
		restPoints = restPoints,
		entranceOutside = Vector3.new(plan.entrance.outside[1], plan.floorY, plan.entrance.outside[2]),
		entranceInside = Vector3.new(plan.entrance.inside[1], plan.floorY, plan.entrance.inside[2]),
		roomOf = roomOf,
	}
end

return Clinic
