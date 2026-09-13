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
local Detail = require(Shared.Detail)
local Loc = require(Shared.Loc)
local Props = require(script.Parent.Props)
local SkyLighting = require(script.Parent.Lighting)

local plan = require(Data.clinic)
local propData = require(Data.props)

local Clinic = {}

-- surface = masanin uzerine hayvan konabilen yuzeyi [uzunluk, genislik].
-- PatientFlow buyuk bir hayvani kucuk bir masaya koymayi reddediyor.
export type Stations = { [string]: { cframe: CFrame, model: Model, surface: { number } } }

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

--- Cam bosluğu: kapi ile AYNI mantik, farki alttan ve ustten duvarin
--- devam etmesi. Icine cam ve cerceve konuyor.
local function windowGlass(
	parent: Instance,
	horizontal: boolean,
	fixed: number,
	at: number,
	width: number,
	sill: number,
	height: number,
	frameColor: Color3
)
	local centerY = sill + height * 0.5
	local function piece(name: string, axisCenter: number, axisSize: number, y: number, ySize: number, thickness: number, color: Color3, material: Enum.Material, transparency: number)
		local size, position
		if horizontal then
			size = Vector3.new(axisSize, ySize, thickness)
			position = Vector3.new(axisCenter, y, fixed)
		else
			size = Vector3.new(thickness, ySize, axisSize)
			position = Vector3.new(fixed, y, axisCenter)
		end
		Build.part({
			name = name,
			size = size,
			cf = CFrame.new(position),
			color = color,
			material = material,
			transparency = transparency,
			canCollide = transparency < 0.5,
			castShadow = false,
			parent = parent,
		})
	end

	local glassColor = Build.color(plan.colors.glass)
	piece("Cam", at, width - 0.5, centerY, height - 0.5, plan.wallThickness * 0.45, glassColor, Enum.Material.Glass, 0.72)
	-- Cerceve: dort kenar + orta dikme
	piece("PencereCercevesi", at, width, sill + 0.12, 0.28, plan.wallThickness + 0.1, frameColor, Enum.Material.SmoothPlastic, 0)
	piece("PencereCercevesi", at, width, sill + height - 0.12, 0.28, plan.wallThickness + 0.1, frameColor, Enum.Material.SmoothPlastic, 0)
	piece("PencereCercevesi", at - width * 0.5 + 0.14, 0.28, centerY, height, plan.wallThickness + 0.1, frameColor, Enum.Material.SmoothPlastic, 0)
	piece("PencereCercevesi", at + width * 0.5 - 0.14, 0.28, centerY, height, plan.wallThickness + 0.1, frameColor, Enum.Material.SmoothPlastic, 0)
	piece("PencereKayidi", at, 0.18, centerY, height - 0.4, plan.wallThickness + 0.06, frameColor, Enum.Material.SmoothPlastic, 0)
	-- Denizlik: disariya tasan ince raf
	piece("Denizlik", at, width + 0.6, sill - 0.1, 0.22, plan.wallThickness + 0.8, frameColor, Enum.Material.SmoothPlastic, 0)
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

	-- Kapi ve pencere ayni eksende siralaniyor; duvar ikisinin arasinda
	-- kalan parcalardan kuruluyor.
	local openings = {}
	for _, door in wall.doors or {} do
		table.insert(openings, { at = door.at, width = door.width, kind = "door" })
	end
	for _, window in wall.windows or {} do
		table.insert(openings, {
			at = window.at,
			width = window.width,
			kind = "window",
			sill = window.sill,
			height = window.height,
		})
	end
	table.sort(openings, function(a, b)
		return a.at < b.at
	end)

	local frameColor = Build.color(plan.colors.windowFrame)
	local cursor = axisFrom
	for _, opening in openings do
		local left = opening.at - opening.width * 0.5
		local right = opening.at + opening.width * 0.5
		wallSegment(parent, horizontal, fixed, cursor, left, 0, plan.wallHeight, color)

		if opening.kind == "door" then
			wallSegment(parent, horizontal, fixed, left, right, plan.doorHeight, plan.wallHeight, color)
			doorFrame(parent, horizontal, fixed, opening.at, opening.width, trim)
		else
			-- Pencerenin alti ve ustu duvar olarak devam ediyor.
			wallSegment(parent, horizontal, fixed, left, right, 0, opening.sill, color)
			wallSegment(parent, horizontal, fixed, left, right, opening.sill + opening.height, plan.wallHeight, color)
			windowGlass(parent, horizontal, fixed, opening.at, opening.width, opening.sill, opening.height, frameColor)
		end
		cursor = right
	end
	wallSegment(parent, horizontal, fixed, cursor, axisTo, 0, plan.wallHeight, color)

	-- Supurgelik: duvarin dibinde ince bir serit. Tek renkli duvarlari
	-- kirar, mekan daha bitmis gorunur.
	wallSegment(parent, horizontal, fixed, axisFrom, axisTo, 0, 0.6, trim)
	-- Tavan kornisi: ust kenarda ikinci serit.
	wallSegment(parent, horizontal, fixed, axisFrom, axisTo, plan.wallHeight - 0.7, plan.wallHeight, trim)
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

	-- Lamba ayari bir kez okunuyor: her oda icin yeniden aramanin anlami yok.
	local ceilingLamp = SkyLighting.lamp("ceiling")

	local surfaceOf: { [string]: { number } } = {}
	for _, entry in propData.props do
		if entry.surface ~= nil then
			surfaceOf[entry.id] = entry.surface
		end
	end

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

		-- Dama deseni: tek renkli genis bir doseme en cok "bitmemis" duran
		-- sey; iki tonlu kareler odayi bitiriyor.
		if (room.floorTile or 0) > 0 then
			Detail.tiles(
				structure,
				room.min,
				room.max,
				plan.floorY + 0.03,
				room.floorTile,
				Build.color(room.floorColor),
				Build.color(room.floorTileColor),
				materialOf(room.floorMaterial)
			)
		end

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
				structure,
				ceilingLamp
			)
		end

		-- Oda adi tabelasi: odanin orta noktasinin ustunde asili.
		local centerX = (room.min[1] + room.max[1]) * 0.5
		local centerZ = (room.min[2] + room.max[2]) * 0.5
		local signCF = CFrame.new(centerX, plan.wallHeight - 2.6, centerZ)
		Detail.plate(structure, signCF, 8.4, 1.6, Build.color(plan.colors.roof), trimColor, Loc.get(room.nameKey))
		Detail.plate(structure, signCF * CFrame.Angles(0, math.pi, 0), 8.4, 1.6, Build.color(plan.colors.roof), trimColor, Loc.get(room.nameKey))
		-- Tabelayi tavana asan ince cubuklar
		for _, side in { -1, 1 } do
			Build.part({
				name = "TabelaAskisi",
				size = Vector3.new(0.14, 2.2, 0.14),
				cf = CFrame.new(centerX + side * 3.4, plan.wallHeight - 1.4, centerZ),
				color = trimColor,
				material = Enum.Material.Metal,
				canCollide = false,
				castShadow = false,
				parent = structure,
			})
		end

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
					local surface = surfaceOf[spec.id]
					if surface == nil then
						warn(("[Clinic] '%s' istasyonu yuzey bildirmiyor (data/props.json)"):format(spec.id))
						surface = { 6, 4 }
					end
					stations[stationId] = { cframe = origin, model = model, surface = surface }
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
