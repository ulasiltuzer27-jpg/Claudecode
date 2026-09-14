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
local Walls = require(Shared.Walls)
local Detail = require(Shared.Detail)
local Loc = require(Shared.Loc)
local Props = require(script.Parent.Props)
local Decor = require(script.Parent.Decor)
local Doors = require(script.Parent.Doors)
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
	ownerPoints: { CFrame },
	-- Kogus yataklari: yatan hastanin duracagi yerler (Ward.luau).
	bedPoints: { CFrame },
	-- Adli noktalar: OYUNCUNUN durdugu yerler (hastanin degil). Su an
	-- yalnizca laboratuvar tezgahi. Istasyon olarak tanimlanamazdi:
	-- `callNext` butun istasyonlari tariyor ve laboratuvara hasta
	-- yatirilmasi anlamsiz olurdu.
	spots: { [string]: CFrame },
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

--- Duvarlar `src/shared/Walls.luau` icinde: lobi de AYNI duvarlari
--- kuruyor ve ikinci bir kurucu yazmak, bir duvar hatasini iki yerde
--- birden duzeltmek demek olurdu.
---
--- Parca ADLARI onemli: dekorasyon temasi parcalari adina gore buluyor
--- (Decor.luau). Duvar govdesi ile supurgelik ayni adi tasirsa tema
--- ikisini ayni renge boyardi - bu yuzden Walls.luau onlari ayri
--- adlandiriyor.
local function buildWall(parent: Instance, wall: any, color: Color3, trim: Color3)
	Walls.build(plan :: any, parent, wall, color, trim)
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
	local ownerPoints: { CFrame } = {}
	local bedPoints: { CFrame } = {}
	local spots: { [string]: CFrame } = {}
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
		-- Sahipler hayvanlarinin ARKASINDA bekliyor; yerleri veride ayri
		-- duruyor cunku 6,4 stud'luk hayvanlarin arasinda NPC'ye yer yok.
		for _, point in room.ownerPoints or {} do
			table.insert(ownerPoints, CFrame.new(point[1], plan.floorY, point[2]))
		end

		for _, point in room.bedPoints or {} do
			table.insert(bedPoints, CFrame.new(point[1], plan.floorY, point[2]))
		end

		for name, point in room.spots or {} do
			spots[name] = CFrame.new(point[1], plan.floorY, point[2])
		end

		for _, spec in room.props do
			local origin = CFrame.new(spec.at[1], plan.floorY, spec.at[2])
				* CFrame.Angles(0, math.rad(spec.rot or 0), 0)
			local model = Props.build(spec.id, origin, furniture)
			if model ~= nil and spec.tag ~= nil then
				-- Yonlendirme tabelasi: yazisi VERIDEN geliyor
				-- (`tag: "sign:room.exam1"`). Esya kurucusunun oda
				-- adlarini bilmesi gerekmiyor; tabela genel, yazi ozel.
				local signKey = string.match(spec.tag, "^sign:(.+)$")
				if signKey ~= nil then
					for _, item in model:GetDescendants() do
						if item:IsA("TextLabel") then
							item.Text = Loc.get(signKey)
						end
					end
				end
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

	-- Kapi kanatlari: yapinin kapi bosluklarina oturuyor. Dekorasyondan
	-- ONCE kuruluyor ki kanatlar da temaya gore boyanabilsin degil -
	-- kanatlar kendi rengini tasiyor; sira yalnizca okunabilirlik icin.
	Doors.build(structure)

	-- Dekorasyon en sonda baglaniyor: boyanacak parcalar ve sus
	-- esyalarinin yerleri ancak yapi bittikten sonra belli.
	Decor.attach(structure, furniture, Props.build)

	return {
		model = root,
		stations = stations,
		queuePoints = queuePoints,
		ownerPoints = ownerPoints,
		bedPoints = bedPoints,
		spots = spots,
		entranceOutside = Vector3.new(plan.entrance.outside[1], plan.floorY, plan.entrance.outside[2]),
		entranceInside = Vector3.new(plan.entrance.inside[1], plan.floorY, plan.entrance.inside[2]),
		roomOf = roomOf,
	}
end

return Clinic
